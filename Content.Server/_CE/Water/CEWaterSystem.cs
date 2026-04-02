using Content.Shared._CE.Fire;
using Content.Shared._CE.Water;
using Content.Shared.Conveyor;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.Water;

public sealed class CEWaterSystem : EntitySystem
{
    [Dependency] private readonly CEFireSystem _fire = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;

    private EntityQuery<CEWaterComponent> _waterQuery;

    public override void Initialize()
    {
        base.Initialize();

        _waterQuery = GetEntityQuery<CEWaterComponent>();

        // Conveyor activation for flowing water.
        SubscribeLocalEvent<CEWaterComponent, MapInitEvent>(OnMapInit);

        // Fire interaction: block tile fire on water.
        SubscribeLocalEvent<CEIgniteTileAttemptEvent>(OnIgniteTileAttempt);

        // Fire interaction: extinguish entities entering water.
        SubscribeLocalEvent<CEWaterComponent, StartCollideEvent>(OnCollide);
    }

    private void OnMapInit(EntityUid uid, CEWaterComponent component, MapInitEvent args)
    {
        if (!component.Flowing)
            return;

        var conveyor = EnsureComp<ConveyorComponent>(uid);
        conveyor.State = ConveyorState.Forward;
        conveyor.Powered = true;
        conveyor.Speed = component.Speed;
        Dirty(uid, conveyor);
    }

    #region Fire Interaction

    /// <summary>
    /// Block fire tiles from being placed on water. Unlike ice, water is NOT deleted.
    /// </summary>
    private void OnIgniteTileAttempt(ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_mapManager.TryFindGridAt(args.Coordinates, out var gridUid, out var grid))
            return;

        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), args.Coordinates);

        foreach (var ent in anchored)
        {
            if (!_waterQuery.HasComp(ent))
                continue;

            args.Cancelled = true;
            _fire.SpawnSteamEffect(args.Coordinates);
            return;
        }
    }

    /// <summary>
    /// Extinguish burning entities that touch water.
    /// </summary>
    private void OnCollide(Entity<CEWaterComponent> ent, ref StartCollideEvent args)
    {
        _fire.ExtinguishEntity(args.OtherEntity);
    }

    #endregion
}
