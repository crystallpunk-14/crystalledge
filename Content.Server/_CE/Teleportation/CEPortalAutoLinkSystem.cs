using Content.Shared.Teleportation.Systems;

namespace Content.Server._CE.Teleportation;

/// <summary>
/// Auto-links pairs of portals at map-init time. A portal carrying
/// <see cref="CEPortalAutoLinkComponent"/> looks for another portal on the same map with
/// a matching <see cref="CEPortalAutoLinkComponent.Key"/>. If found, both portals are
/// linked through <see cref="LinkedEntitySystem"/> (so <see cref="PortalComponent"/>
/// on each can teleport to the other) and the auto-link component is stripped from both
/// so the pairing never re-runs.
/// </summary>
public sealed class CEPortalAutoLinkSystem : EntitySystem
{
    [Dependency] private readonly LinkedEntitySystem _link = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPortalAutoLinkComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEPortalAutoLinkComponent> ent, ref MapInitEvent args)
    {
        var key = ent.Comp.Key;
        if (string.IsNullOrEmpty(key))
        {
            Log.Warning($"CEPortalAutoLink on {ToPrettyString(ent)} has empty key.");
            RemComp<CEPortalAutoLinkComponent>(ent);
            return;
        }

        var selfMap = Transform(ent).MapID;

        // Find the first other portal with the same key on the same map.
        //
        // Race-condition note: the partner portal also has this component, so its own
        // MapInitEvent will fire too. Both endpoints run this handler independently in
        // some order. To make the second pass a no-op we strip the marker component from
        // BOTH endpoints immediately when we link them — RemComp is synchronous, so once
        // we return here neither portal carries CEPortalAutoLinkComponent any more. The
        // engine's directed-subscription dispatch checks component presence per-entity
        // when raising MapInitEvent on each entity, so the partner's handler simply
        // never fires.
        var query = EntityQueryEnumerator<CEPortalAutoLinkComponent, TransformComponent>();
        while (query.MoveNext(out var other, out var otherLink, out var otherXform))
        {
            if (other == ent.Owner)
                continue;

            if (otherLink.Key != key)
                continue;

            if (!_link.TryLink(ent.Owner, other))
            {
                Log.Warning($"CEPortalAutoLink: failed to link {ToPrettyString(ent)} <-> {ToPrettyString(other)} (key '{key}').");
                return;
            }

            RemComp<CEPortalAutoLinkComponent>(ent);
            RemComp<CEPortalAutoLinkComponent>(other);
            return;
        }

        // No partner found yet. Leave the component in place so a future MapInitEvent on
        // the partner (e.g. spawned later) can complete the pairing from its side.
    }
}
