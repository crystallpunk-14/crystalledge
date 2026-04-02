using Content.Shared._CE.Fire;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;

namespace Content.Shared._CE.Water;

public abstract class CESharedWaterSystem : EntitySystem
{
    [Dependency] protected readonly CEFireSystem Fire = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private EntityQuery<CEWaterComponent> _waterQuery;

    public override void Initialize()
    {
        base.Initialize();

        _waterQuery = GetEntityQuery<CEWaterComponent>();

        SubscribeLocalEvent<TransformComponent, CEIgniteEntityAttemptEvent>(OnIgniteEntityAttempt);
        SubscribeLocalEvent<CEWaterComponent, CEIgniteTileAttemptEvent>(OnIgniteTileAttempt);
    }

    private void OnIgniteEntityAttempt(Entity<TransformComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!IsOnWater(ent))
            return;

        args.Cancelled = true;
        Fire.SpawnSteamEffect(args.Target);
    }

    /// <summary>
    /// Water blocks fire tile placement. Water is NOT consumed.
    /// </summary>
    private void OnIgniteTileAttempt(Entity<CEWaterComponent> ent, ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        Fire.SpawnSteamEffect(args.Coordinates);
    }

    /// <summary>
    /// Checks if an entity is standing on a tile that contains a water entity.
    /// </summary>
    protected bool IsOnWater(Entity<TransformComponent> ent)
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
