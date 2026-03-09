using Content.Server._CE.Skills.Blessing.Components;

namespace Content.Server._CE.Skills.Blessing;

/// <summary>
/// Handles linking statues to pedestal tables on MapInit via EntityLookup.
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
            if (HasComp<CEBlessingTableComponent>(uid))
                ent.Comp.LinkedTables.Add(uid);
        }
    }
}
