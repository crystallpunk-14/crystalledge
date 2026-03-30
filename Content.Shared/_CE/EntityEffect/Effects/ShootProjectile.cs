using System.Numerics;
using Content.Shared.Weapons.Ranged.Systems;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Physics.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class ShootProjectile : CEEntityEffectBase<ShootProjectile>
{
    [DataField(required: true)]
    public EntProtoId Prototype;

    [DataField]
    public float ProjectileSpeed = 20f;

    [DataField]
    public float Spread;

    [DataField]
    public int ProjectileCount = 1;

    [DataField]
    public bool SaveVelocity;
}

public sealed partial class CEShootProjectileEffectSystem : CEEntityEffectSystem<ShootProjectile>
{
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedGunSystem _gun = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    protected override void Effect(ref CEEntityEffectEvent<ShootProjectile> args)
    {
        if (!_net.IsServer)
            return;

        var xform = Transform(args.Args.User);

        var fromCoords = xform.Coordinates;
        var userVelocity = _physics.GetMapLinearVelocity(args.Args.User);

        // If applicable, this ensures the projectile is parented to grid on spawn, instead of the map.
        var fromMap = _transform.ToMapCoordinates(fromCoords);

        var spawnCoords = _mapManager.TryFindGridAt(fromMap, out var gridUid, out _)
            ? _transform.WithEntityId(fromCoords, gridUid)
            : new(_mapManager.GetMapEntityId(fromMap.MapId), fromMap.Position);

        // Resolve direction: prefer target coordinates, fall back to angle.
        var baseDirection = Vector2.Zero;
        if (TryResolveTargetCoordinates(args.Args, out var targetPoint))
        {
            baseDirection = targetPoint.ToMapPos(EntityManager, _transform) -
                            spawnCoords.ToMapPos(EntityManager, _transform);
        }

        // Fall back to angle when no target or target is the user (zero direction).
        if (baseDirection == default)
        {
            baseDirection = args.Args.Angle.ToWorldVec();
        }

        for (var i = 0; i < args.Effect.ProjectileCount; i++)
        {
            var direction = baseDirection + new Vector2(
                (float)(_random.NextDouble() * 2 - 1) * args.Effect.Spread,
                (float)(_random.NextDouble() * 2 - 1) * args.Effect.Spread);

            if (direction == Vector2.Zero)
                continue;

            var ent = EntityManager.SpawnAtPosition(args.Effect.Prototype, spawnCoords);

            _gun.ShootProjectile(ent,
                direction,
                args.Effect.SaveVelocity ? userVelocity : new Vector2(),
                args.Args.User,
                args.Args.User,
                args.Effect.ProjectileSpeed);
        }
    }
}
