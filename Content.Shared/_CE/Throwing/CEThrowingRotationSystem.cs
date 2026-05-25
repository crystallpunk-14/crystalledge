using Content.Shared.Throwing;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CE.Throwing;

public sealed class CEThrowingRotationSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEThrowingRotationComponent, ThrownEvent>(OnThrown);
    }

    private void OnThrown(Entity<CEThrowingRotationComponent> ent, ref ThrownEvent args)
    {
        if (!TryComp<PhysicsComponent>(ent, out var body))
            return;

        // Derive throw direction from the linear velocity applied by ThrowingSystem.
        if (body.LinearVelocity == System.Numerics.Vector2.Zero)
            return;

        // Set the starting angle: throw direction in world space converted to local rotation,
        // plus the configured per-entity offset.
        var throwAngle = body.LinearVelocity.ToAngle();
        var gridRot = _transform.GetWorldRotation(Transform(ent).ParentUid);
        var localAngle = throwAngle - gridRot + ent.Comp.StartAngle;
        _transform.SetLocalRotation(ent, localAngle);

        // Override angular velocity only when explicitly configured.
        if (ent.Comp.RotationSpeed.HasValue)
            _physics.SetAngularVelocity(ent, ent.Comp.RotationSpeed.Value, body: body);
    }
}
