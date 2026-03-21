using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Content.Server._CE.Procedural.Generators;
using Content.Server._CE.Procedural.Instance.Components;
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
    private readonly Dictionary<EntityUid, Task<CEDungeonGenerateResult>> _pendingGenerations = new();

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

                passage.TargetPosition = Transform(targetEntry.Value).Coordinates;
            }

            var candidates = GatherNearbyPlayers(uid, passage.SearchRadius, passage.Throughput);
            if (candidates.Count == 0)
            {
                Log.Warning("No players found near exit for transition.");
                return;
            }

            if (passage.TargetPosition == null)
            {
                Log.Error("Active passage has no target position.");
                return;
            }

            foreach (var player in candidates)
            {
                if (!Exists(player) || Deleted(player))
                    continue;

                _transform.SetMapCoordinates(player, _transform.ToMapCoordinates(passage.TargetPosition.Value));
                _flash.Flash(player, null, null, FlashDuration, 0.8f);
                _audio.PlayEntity(TransitionSound, player, player);
            }
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
            // Trigger dungeon generation and register the instance when the job completes.
            var genTask = _dungeon.GenerateLevelAsync(proto);
            _pendingGenerations[activePassage] = genTask;

            genTask.ContinueWith(t =>
                {
                    _pendingGenerations.Remove(activePassage);

                    if (t.IsFaulted || !t.IsCompletedSuccessfully)
                    {
                        Log.Error($"Generation failed for '{ent.Comp.TargetLevel}'.");
                        return;
                    }

                    var result = t.GetAwaiter().GetResult();
                    if (!result.Success || result.MapUid == null)
                    {
                        Log.Error($"Generation failed for '{ent.Comp.TargetLevel}'.");
                        return;
                    }

                    RegisterInstance(result.MapUid.Value, proto);

                    if (TryFindEnterPoint(proto, out var entry))
                    {
                        var activeComp2 = EnsureComp<CEDungeonActivePassageComponent>(activePassage);
                        activeComp2.TargetPosition = Transform(entry.Value).Coordinates;
                    }
                },
                TaskScheduler.FromCurrentSynchronizationContext());
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
