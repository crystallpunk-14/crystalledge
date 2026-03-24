using System.Numerics;
using Content.Server.NPC.Components;
using Content.Server.NPC.Systems;
using Content.Shared._CE.GOAP;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Actions;

/// <summary>
/// Navigates to the last-known position of a target and wanders around it
/// until the memorized position expires.
/// </summary>
public sealed partial class CEGOAPMoveToLastKnownPositionAction
    : CEGOAPActionBase<CEGOAPMoveToLastKnownPositionAction>
{
    /// <summary>
    /// The target key to look up in LastKnownPositions.
    /// </summary>
    [DataField(required: true)]
    public string PositionTargetKey = string.Empty;

    /// <summary>
    /// How close to get before considering arrival at a waypoint.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Radius around the last-known position to wander in.
    /// </summary>
    [DataField]
    public float SearchRadius = 6f;

    /// <summary>
    /// Number of random directions to sample when picking a wander point.
    /// </summary>
    [DataField]
    public int SampleDirections = 8;

    [DataField]
    public TimeSpan SearchTime = TimeSpan.FromSeconds(10f);

    [DataField(customTypeSerializer:typeof(TimeOffsetSerializer))]
    public TimeSpan EndSearchTime = TimeSpan.Zero;
}

public sealed partial class CEGOAPMoveToLastKnownPositionActionSystem
    : CEGOAPActionSystem<CEGOAPMoveToLastKnownPositionAction>
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly NPCSteeringSystem _steering = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    protected override void OnActionStartup(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionStartupEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        var coords = Goap.GetLastKnownPosition(ent, args.Action.PositionTargetKey);
        if (coords == null)
            return;

        var comp = _steering.Register(ent, coords.Value);
        comp.Range = args.Action.Range;
        args.Action.EndSearchTime = _timing.CurTime + args.Action.SearchTime;
    }

    protected override void OnActionUpdate(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionUpdateEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        if (_timing.CurTime > args.Action.EndSearchTime)
        {
            Goap.ClearLastKnownPosition(ent, args.Action.PositionTargetKey);
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        if (!TryComp<NPCSteeringComponent>(ent, out var steering))
        {
            args.Status = CEGOAPActionStatus.Failed;
            return;
        }

        switch (steering.Status)
        {
            case SteeringStatus.InRange:
                // Arrived at current waypoint — pick a new random point around the memorized position.
                // If no valid tile is found, stay and wait: sensors keep scanning,
                // and the timeout will eventually expire.
                if (TryPickSearchPoint(ent, args.Action, out var nextCoords))
                {
                    var comp = _steering.Register(ent, nextCoords);
                    comp.Range = args.Action.Range;
                }

                args.Status = CEGOAPActionStatus.Running;
                return;
            case SteeringStatus.NoPath:
                Goap.ClearLastKnownPosition(ent, args.Action.PositionTargetKey); //We cant reach target position, forget it and fail
                args.Status = CEGOAPActionStatus.Failed;
                return;
            default:
                args.Status = CEGOAPActionStatus.Running;
                return;
        }
    }

    protected override void OnActionShutdown(
        Entity<CEGOAPComponent> ent,
        ref CEGOAPActionShutdownEvent<CEGOAPMoveToLastKnownPositionAction> args)
    {
        args.Action.EndSearchTime = TimeSpan.Zero;
        _steering.Unregister(ent);
    }

    private bool TryPickSearchPoint(
        Entity<CEGOAPComponent> ent,
        CEGOAPMoveToLastKnownPositionAction action,
        out EntityCoordinates coords)
    {
        coords = default;

        var center = Goap.GetLastKnownPosition(ent, action.PositionTargetKey);
        if (center == null)
            return false;

        var worldCenter = _transform.ToMapCoordinates(center.Value);
        var baseAngle = (float) _random.NextAngle().Theta;
        var angleStep = MathF.PI * 2f / action.SampleDirections;

        for (var i = 0; i < action.SampleDirections; i++)
        {
            var angle = baseAngle + angleStep * i;
            var dist = _random.NextFloat(1f, action.SearchRadius);
            var dir = new Vector2(MathF.Cos(angle), MathF.Sin(angle));
            var candidatePos = worldCenter.Position + dir * dist;

            if (!_mapManager.TryFindGridAt(new MapCoordinates(candidatePos, worldCenter.MapId), out var gridUid, out var grid))
                continue;

            var tileIndices = _mapSystem.WorldToTile(gridUid, grid, candidatePos);
            if (!_mapSystem.TryGetTileRef(gridUid, grid, tileIndices, out var tileRef) || tileRef.Tile.IsEmpty)
                continue;

            var invMatrix = _transform.GetInvWorldMatrix(gridUid);
            var localPos = Vector2.Transform(candidatePos, invMatrix);
            coords = new EntityCoordinates(gridUid, localPos);
            return true;
        }

        return false;
    }
}
