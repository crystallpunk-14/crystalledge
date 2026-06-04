using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Robust.Shared.Physics;
using Robust.Shared.Physics.Components;
using Robust.Shared.Physics.Events;
using Robust.Shared.Physics.Systems;

namespace Content.Shared._CE.FloorOcclusion;

public abstract class CESharedFloorOcclusionSystem : EntitySystem
{
    [Dependency] private readonly SharedPhysicsSystem _physics = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEFloorOccluderComponent, StartCollideEvent>(OnStartCollide);
        SubscribeLocalEvent<CEFloorOccluderComponent, EndCollideEvent>(OnEndCollide);
        SubscribeLocalEvent<CEFloorOcclusionComponent, CEZBodyStatusChangedEvent>(OnBodyStatusChanged);
    }

    private void OnStartCollide(Entity<CEFloorOccluderComponent> ent, ref StartCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!TryComp<CEFloorOcclusionComponent>(other, out var occlusion) ||
            occlusion.Colliding.Contains(ent.Owner))
            return;

        if (TryComp<PhysicsComponent>(other, out var physics) && physics.BodyStatus == BodyStatus.InAir)
            return;

        occlusion.Colliding.Add(ent.Owner);
        Dirty(other, occlusion);
        SetEnabled((other, occlusion));
    }

    private void OnEndCollide(Entity<CEFloorOccluderComponent> ent, ref EndCollideEvent args)
    {
        var other = args.OtherEntity;

        if (!TryComp<CEFloorOcclusionComponent>(other, out var occlusion))
            return;

        if (!occlusion.Colliding.Remove(ent.Owner))
            return;

        Dirty(other, occlusion);
        SetEnabled((other, occlusion));
    }

    private void OnBodyStatusChanged(Entity<CEFloorOcclusionComponent> ent, ref CEZBodyStatusChangedEvent args)
    {
        if (args.NewStatus == BodyStatus.InAir)
        {
            if (ent.Comp.Colliding.Count == 0)
                return;

            ent.Comp.Colliding.Clear();
            Dirty(ent);
            SetEnabled(ent);
        }
        else
        {
            // Re-populate from current contacts in case entity landed back inside an occluder.
            var changed = false;
            foreach (var contacting in _physics.GetContactingEntities(ent))
            {
                if (!HasComp<CEFloorOccluderComponent>(contacting) || ent.Comp.Colliding.Contains(contacting))
                    continue;

                ent.Comp.Colliding.Add(contacting);
                changed = true;
            }

            if (!changed)
                return;

            Dirty(ent);
            SetEnabled(ent);
        }
    }

    protected virtual void SetEnabled(Entity<CEFloorOcclusionComponent> entity) { }
}
