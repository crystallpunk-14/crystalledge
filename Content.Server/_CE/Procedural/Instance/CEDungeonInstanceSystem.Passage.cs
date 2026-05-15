using System.Linq;
using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Shared._CE.Procedural.Components;
using Content.Shared.Examine;
using Content.Shared.Flash;
using Content.Shared.Interaction;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    [Dependency] private readonly SharedFlashSystem _flash = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    // Pending generation tasks started for active passages (maps to the active passage entity).
    // We store the proto id alongside the task so the result can be processed safely on the main thread.
    private readonly Dictionary<EntityUid, (Task<CEDungeonGenerateResult> Task, Robust.Shared.Prototypes.ProtoId<CEDungeonLevelPrototype> ProtoId)> _pendingGenerations = new();

    /// <summary>
    /// Sound played on each player when they arrive at the destination.
    /// </summary>
    private static readonly SoundCollectionSpecifier TransitionSound = new("CEDemiplaneIntro");

    /// <summary>
    /// Duration of the white-flash blind applied to teleported players.
    /// </summary>
    private static readonly TimeSpan FlashDuration = TimeSpan.FromSeconds(2);

    private void InitializePassage()
    {
        SubscribeLocalEvent<CEDungeonPassageComponent, ActivateInWorldEvent>(OnPassageInWorldActivated);
        SubscribeLocalEvent<CEDungeonPassageComponent, ExaminedEvent>(OnPassageExamined);
    }

    private void OnPassageExamined(Entity<CEDungeonPassageComponent> ent, ref ExaminedEvent args)
    {
        // Show the destination level name when the portal is examined. The destination is
        // resolved on demand from the owning dungeon instance's Exits dictionary, keyed by
        // the passage's TargetLevel slot.
        if (TryResolveExitTarget(ent, out var proto) && proto.Name is { } nameKey)
        {
            args.PushMarkup(Loc.GetString("ce-dungeon-passage-examine-target", ("level", Loc.GetString(nameKey))));
            return;
        }

        args.PushMarkup(Loc.GetString("ce-dungeon-passage-examine-target-unknown"));
    }

    /// <summary>
    /// Resolves the target dungeon level prototype for a passage by looking up the owning
    /// dungeon instance and reading <see cref="CEDungeonLevelPrototype.Exits"/> with the
    /// passage's slot key. Returns false if the passage isn't on a registered dungeon
    /// instance, the prototype is unknown, or the slot has no mapping.
    /// </summary>
    private bool TryResolveExitTarget(Entity<CEDungeonPassageComponent> ent, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out CEDungeonLevelPrototype? targetProto)
    {
        targetProto = null;

        var xform = Transform(ent);
        if (!TryResolveInstance(xform.MapUid, out var instance))
            return false;

        if (!_proto.TryIndex(instance.PrototypeId, out var ownerProto))
            return false;

        if (!ownerProto.Exits.TryGetValue(ent.Comp.TargetLevel, out var targetId))
            return false;

        return _proto.TryIndex(targetId, out targetProto);
    }

    private void UpdatePassage()
    {
        if (_pendingGenerations.Count > 0)
        {
            var pendingList = _pendingGenerations.ToList();
            foreach (var (passageUid, tuple) in pendingList)
            {
                var task = tuple.Task;
                var protoId = tuple.ProtoId;

                if (!task.IsCompleted)
                    continue;

                _pendingGenerations.Remove(passageUid);

                if (task.IsFaulted || !task.IsCompletedSuccessfully)
                {
                    Log.Error($"Generation failed for '{protoId}'.");
                    continue;
                }

                var result = task.GetAwaiter().GetResult();
                if (!result.Success || result.MapUid == null)
                {
                    Log.Error($"Generation failed for '{protoId}'.");
                    continue;
                }

                if (!_proto.TryIndex(protoId, out var proto))
                {
                    Log.Error($"Generated instance has unknown prototype id '{protoId}'.");
                    continue;
                }

                RegisterInstance(result.MapUid.Value, proto);

                if (TryFindEnterPoint(proto, out var entry))
                {
                    var activeComp2 = EnsureComp<CEDungeonActivePassageComponent>(passageUid);
                    activeComp2.TargetPosition = Transform(entry.Value).Coordinates;
                }
            }
        }

        var query = EntityQueryEnumerator<CEDungeonActivePassageComponent>();
        while (query.MoveNext(out var uid, out var passage))
        {
            if (passage.NextTransitionTime > _timing.CurTime)
                continue;

            passage.NextTransitionTime = _timing.CurTime + passage.TransitionDelay;


            if (passage.TargetPosition is null && _proto.Resolve(passage.TargetLevel, out var resolvedTarget))
            {
                if (!TryFindEnterPoint(resolvedTarget, out var targetEntry))
                    continue;

                passage.TargetPosition = Transform(targetEntry.Value).Coordinates;
            }

            var candidates = GatherNearbyPlayers(uid, passage.SearchRadius, passage.Throughput);
            if (passage.TargetPosition == null)
            {
                Log.Error("Active passage has no target position.");
                continue;
            }

            foreach (var player in candidates)
            {
                if (!Exists(player) || Deleted(player))
                    continue;

                _transform.SetMapCoordinates(player, _transform.ToMapCoordinates(passage.TargetPosition.Value));
                _flash.Flash(player, null, null, FlashDuration, 0.8f);
                _audio.PlayGlobal(TransitionSound, player, AudioParams.Default.WithVolume(-4));
            }
            QueueDel(uid);
        }
    }

    private void OnPassageInWorldActivated(Entity<CEDungeonPassageComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (Exists(ent.Comp.ActivePassage))
            return;

        args.Handled = true;

        if (!TryResolveExitTarget(ent, out var proto))
        {
            Log.Error($"exit on map {Transform(ent).MapID} cannot resolve target for slot {ent.Comp.TargetLevel}.");
            QueueDel(ent);
            return;
        }

        var activePassage = SpawnAtPosition(ent.Comp.ActivePassageProto, Transform(ent).Coordinates);
        ent.Comp.ActivePassage = activePassage;

        var activeComp = EnsureComp<CEDungeonActivePassageComponent>(activePassage);
        activeComp.NextTransitionTime = _timing.CurTime + activeComp.TransitionInitialDelay;
        activeComp.TargetLevel = proto.ID;

        if (TryFindEnterPoint(proto, out var targetEntry))
            activeComp.TargetPosition = Transform(targetEntry.Value).Coordinates;
        else
        {
            // Trigger dungeon generation and store the task; result will be processed on the main thread in UpdatePassage.
            var genTask = _dungeon.GenerateLevelAsync(proto);
            _pendingGenerations[activePassage] = (genTask, proto.ID);
        }
    }

    private List<EntityUid> GatherNearbyPlayers(EntityUid origin, float radius, int maxCount)
    {
        var nearby = _lookup.GetEntitiesInRange<CEDungeonPlayerComponent>(_transform.GetMapCoordinates(origin), radius);
        var candidates = nearby.Select(e => e.Owner).ToList();

        if (candidates.Count > maxCount)
        {
            _random.Shuffle(candidates);
            candidates = candidates.Take(maxCount).ToList();
        }

        return candidates;
    }
}
