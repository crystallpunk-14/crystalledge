using Content.Shared._CE.Fire;
using Content.Shared._CE.Frost;
using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ElementInteraction;

/// <summary>
/// Handles fire/frost mutual neutralization through ECS attempt events.
/// When fire is applied to a frosted entity (or ice tile), and vice versa,
/// the opposing elements cancel each other out with a steam effect.
/// Also handles direct spawns (e.g. admin panel) via MapInit subscriptions.
/// </summary>
public sealed class CEElementInteractionSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly EntProtoId _statusFire = "CEStatusEffectFire";
    private readonly EntProtoId _statusColdSlowdown = "CEStatusEffectColdSlowdown";
    private readonly EntProtoId _steamEffect = "CESteamEffect";
    private readonly SoundSpecifier _steamSound = new SoundPathSpecifier("/Audio/Effects/sizzle.ogg");

    private EntityQuery<CEFireComponent> _fireQuery;
    private EntityQuery<CEIceComponent> _iceQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();
        _iceQuery = GetEntityQuery<CEIceComponent>();

        // Entity attempt events (directed on target entity).
        SubscribeLocalEvent<TransformComponent, CEIgniteEntityAttemptEvent>(OnIgniteEntityAttempt);
        SubscribeLocalEvent<TransformComponent, CEFreezeEntityAttemptEvent>(OnFreezeEntityAttempt);

        // Tile attempt events (broadcast).
        SubscribeLocalEvent<CEIgniteTileAttemptEvent>(OnIgniteTileAttempt);
        SubscribeLocalEvent<CEFreezeTileAttemptEvent>(OnFreezeTileAttempt);
    }

    private void OnIgniteEntityAttempt(Entity<TransformComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var frostStacks = _stack.GetStack(args.Target, _statusColdSlowdown);
        if (frostStacks <= 0)
            return;

        var neutralized = Math.Min(frostStacks, args.Stacks);
        _stack.TryRemoveStack(args.Target, _statusColdSlowdown, neutralized);
        args.Stacks -= neutralized;

        PlaySteamEffect(args.Target);

        if (args.Stacks <= 0)
            args.Cancelled = true;
    }

    private void OnFreezeEntityAttempt(Entity<TransformComponent> ent, ref CEFreezeEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var fireStacks = _stack.GetStack(args.Target, _statusFire);
        if (fireStacks <= 0)
            return;

        var neutralized = Math.Min(fireStacks, args.Stacks);
        _stack.TryRemoveStack(args.Target, _statusFire, neutralized);
        args.Stacks -= neutralized;

        PlaySteamEffect(args.Target);

        if (args.Stacks <= 0)
            args.Cancelled = true;
    }

    private void OnIgniteTileAttempt(ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_mapManager.TryFindGridAt(args.Coordinates, out var gridUid, out var grid))
            return;

        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), args.Coordinates);

        foreach (var ent in anchored)
        {
            if (!_iceQuery.HasComp(ent))
                continue;

            // Melt the ice and cancel fire placement.
            if (!_net.IsClient)
                EntityManager.DeleteEntity(ent);

            args.Cancelled = true;
            PlaySteamEffectAt(args.Coordinates);
            return;
        }
    }

    private void OnFreezeTileAttempt(ref CEFreezeTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_mapManager.TryFindGridAt(args.Coordinates, out var gridUid, out var grid))
            return;

        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), args.Coordinates);

        foreach (var ent in anchored)
        {
            if (!_fireQuery.HasComp(ent))
                continue;

            // Extinguish the fire and cancel ice placement.
            if (!_net.IsClient)
                EntityManager.DeleteEntity(ent);

            args.Cancelled = true;
            PlaySteamEffectAt(args.Coordinates);
            return;
        }
    }

    private void PlaySteamEffect(EntityUid target)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(target).Coordinates;

        Spawn(_steamEffect, pos);
        _audio.PlayPvs(_steamSound, pos);
    }

    private void PlaySteamEffectAt(MapCoordinates coordinates)
    {
        if (_net.IsClient)
            return;

        var steam = _entManager.SpawnEntity(_steamEffect, coordinates);
        _audio.PlayPvs(_steamSound, Transform(steam).Coordinates);
    }
}
