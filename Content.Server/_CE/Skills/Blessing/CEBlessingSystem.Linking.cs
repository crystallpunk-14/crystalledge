using Content.Server._CE.Skills.Blessing.Components;
using Robust.Shared.GameObjects;

namespace Content.Server._CE.Skills.Blessing;

/// <summary>
/// Handles linking statues to triggers and tables on MapInit via EntityLookup.
/// </summary>
public sealed partial class CEBlessingSystem
{
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    private void InitializeLinking()
    {
        SubscribeLocalEvent<CEBlessingStatueComponent, MapInitEvent>(OnStatueMapInit);
    }

    private void OnStatueMapInit(Entity<CEBlessingStatueComponent> ent, ref MapInitEvent args)
    {
        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.LinkRadius, entities);

        foreach (var uid in entities)
        {
            // Link the first available trigger
            if (ent.Comp.LinkedTrigger is null
                && TryComp<CEBlessingTriggerComponent>(uid, out var triggerComp)
                && triggerComp.LinkedStatue is null)
            {
                ent.Comp.LinkedTrigger = uid;
                triggerComp.LinkedStatue = ent.Owner;
            }

            // Link all tables in range
            if (HasComp<CEBlessingTableComponent>(uid))
            {
                ent.Comp.LinkedTables.Add(uid);
            }
        }
    }
}
