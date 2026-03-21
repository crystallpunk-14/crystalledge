using System.Linq;
using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Prototypes;
using Content.Shared._CE.Procedural.Components;
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
    }

    private void UpdatePassage()
    {
        // Process completed generation tasks on the main thread to avoid thread-safety issues.
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
                continue; //Not ready for transition yet

            passage.NextTransitionTime = _timing.CurTime + passage.TransitionDelay;


            if (passage.TargetPosition is null && _proto.Resolve(passage.TargetLevel, out var resolvedTarget))
            {
                if (!TryFindEnterPoint(resolvedTarget, out var targetEntry))
                    continue;

                targetEntry.Value.Comp.Active = false;
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
                _audio.PlayEntity(TransitionSound, player, player);
            }
            QueueDel(uid);
        }
    }

    /// <summary>
    /// Player activates an exit portal:
    /// 1) Immediately determine or start generating the target instance.
    /// 2) Start a DoAfter (minimum wait time so players can't tell if it's a new or existing instance).
    /// </summary>
    private void OnPassageInWorldActivated(Entity<CEDungeonPassageComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        if (Exists(ent.Comp.ActivePassage))
            return;

        args.Handled = true;

        if (ent.Comp.TargetLevel == null || !_proto.TryIndex(ent.Comp.TargetLevel.Value, out var proto))
        {
            Log.Error($"exit has no target level or unknown prototype '{ent.Comp.TargetLevel}'.");
            QueueDel(ent);
            return;
        }

        var activePassage = SpawnAtPosition(ent.Comp.ActivePassageProto, Transform(ent).Coordinates);
        ent.Comp.ActivePassage = activePassage;

        var activeComp = EnsureComp<CEDungeonActivePassageComponent>(activePassage);
        activeComp.NextTransitionTime = _timing.CurTime + activeComp.TransitionInitialDelay;
        activeComp.TargetLevel = ent.Comp.TargetLevel;

        if (TryFindEnterPoint(proto, out var targetEntry))
        {
            targetEntry.Value.Comp.Active = false; //Disable that entry point
            activeComp.TargetPosition = Transform(targetEntry.Value).Coordinates; //Set target coordinates
        }
        else
        {
            // Trigger dungeon generation and store the task; result will be processed on the main thread in UpdatePassage.
            var genTask = _dungeon.GenerateLevelAsync(proto);
            _pendingGenerations[activePassage] = (genTask, proto.ID);
        }
    }

    /// <summary>
    /// Gathers player entities near the exit, limited by throughput.
    /// Uses the generic <see cref="EntityLookupSystem.GetEntitiesInRange{T}"/> overload.
    /// </summary>
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
