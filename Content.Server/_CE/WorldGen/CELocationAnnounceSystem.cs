using Content.Shared._CE.ScreenPopup;
using Content.Shared._CE.WorldGen.Components;
using Robust.Server.GameObjects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.WorldGen;

/// <summary>
/// Shows the full-screen cinematic location popup (and plays an optional entry sound) the first time
/// a player enters a chunk type that defines a title/description. Reuses the generic
/// <see cref="CEScreenPopupShowEvent"/> pipeline that the dungeon-instance system already drives.
/// </summary>
public sealed partial class CELocationAnnounceSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CELocationVisitorComponent, CEMoveToNewLocation>(OnMoveToNewLocation);
    }

    private void OnMoveToNewLocation(Entity<CELocationVisitorComponent> ent, ref CEMoveToNewLocation args)
    {
        if (!_proto.TryIndex(args.To, out var type))
            return;

        // The location is identified by its popup title (falling back to its description). Chunk types
        // without either never announce, and sub-biomes sharing a title only announce once.
        var key = type.Name ?? type.Desc;
        if (key is not { } locKey)
            return;

        if (!ent.Comp.Visited.Add(locKey))
            return;

        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var ev = new CEScreenPopupShowEvent
        {
            Title = type.Name,
            Desc = type.Desc,
            Sound = type.EntrySound,
        };
        RaiseNetworkEvent(ev, actor.PlayerSession);
    }
}
