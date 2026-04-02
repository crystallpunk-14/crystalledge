using Content.Shared._CE.Fire;
using Content.Shared._CE.Frost;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared._CE.Water;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.ElementInteraction;

/// <summary>
/// Handles element interactions through ECS attempt events:
/// fire/frost mutual neutralization, and water blocking fire.
/// When fire is applied to a frosted entity (or ice tile), opposing elements cancel out with steam.
/// Water blocks entity ignition entirely (but is not consumed, unlike ice).
/// </summary>
public sealed class CEElementInteractionSystem : EntitySystem
{
    [Dependency] private readonly CEFireSystem _fire = default!;
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;

    private readonly EntProtoId _statusFire = "CEStatusEffectFire";
    private readonly EntProtoId _statusColdSlowdown = "CEStatusEffectColdSlowdown";

    private EntityQuery<CEFireComponent> _fireQuery;
    private EntityQuery<CEIceComponent> _iceQuery;
    private EntityQuery<CEWaterComponent> _waterQuery;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();
        _iceQuery = GetEntityQuery<CEIceComponent>();
        _waterQuery = GetEntityQuery<CEWaterComponent>();

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

        // Water blocks ignition entirely — entity standing on a water tile cannot be set on fire.
        if (IsOnWater(ent))
        {
            args.Cancelled = true;
            _fire.SpawnSteamEffect(args.Target);
            return;
        }

        // Frost neutralizes fire stacks.
        var frostStacks = _stack.GetStack(args.Target, _statusColdSlowdown);
        if (frostStacks <= 0)
            return;

        var neutralized = Math.Min(frostStacks, args.Stacks);
        _stack.TryRemoveStack(args.Target, _statusColdSlowdown, neutralized);
        args.Stacks -= neutralized;

        _fire.SpawnSteamEffect(args.Target);

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

        _fire.SpawnSteamEffect(args.Target);

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
            _fire.SpawnSteamEffect(args.Coordinates);
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
            _fire.SpawnSteamEffect(args.Coordinates);
            return;
        }
    }

    /// <summary>
    /// Checks if an entity is standing on a tile that contains a water entity.
    /// </summary>
    private bool IsOnWater(Entity<TransformComponent> ent)
    {
        var xform = ent.Comp;
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var coords = _transform.GetMapCoordinates(ent);
        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), coords);

        foreach (var anchEnt in anchored)
        {
            if (_waterQuery.HasComp(anchEnt))
                return true;
        }

        return false;
    }
}
