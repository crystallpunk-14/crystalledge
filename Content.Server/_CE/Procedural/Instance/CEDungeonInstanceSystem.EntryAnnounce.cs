using Content.Server._CE.Procedural.Instance.Components;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.ScreenPopup;
using Robust.Shared.Player;

namespace Content.Server._CE.Procedural.Instance;

public sealed partial class CEDungeonInstanceSystem
{
    /// <summary>
    /// Tracks which dungeon level prototype IDs each player has already received an entry popup for this round.
    /// Keyed by the player entity; values are sets of prototype ID strings already announced.
    /// </summary>
    private readonly Dictionary<EntityUid, HashSet<string>> _visitedByPlayer = new();

    private void InitializeEntryAnnounce()
    {
        SubscribeLocalEvent<CEDungeonPlayerComponent, EntParentChangedMessage>(OnPlayerParentChanged);
        SubscribeLocalEvent<CEDungeonPlayerComponent, ComponentShutdown>(OnPlayerShutdown);
    }

    private void OnPlayerShutdown(Entity<CEDungeonPlayerComponent> ent, ref ComponentShutdown args)
    {
        _visitedByPlayer.Remove(ent);
        HandleMapEffectsShutdown(ent);
    }

    private void OnPlayerParentChanged(Entity<CEDungeonPlayerComponent> ent, ref EntParentChangedMessage args)
    {
        HandleMapEffectsParentChanged(ent, args);

        var newMapUid = args.Transform.MapUid;
        var oldMapUid = args.OldMapId;

        // Only care about actual map changes.
        if (newMapUid == oldMapUid || newMapUid == null)
            return;

        // Resolve the owning dungeon instance directly via z-network or map entity.
        if (!TryResolveInstance(newMapUid.Value, out var instance))
            return;

        // Look up the prototype.
        if (!_proto.TryIndex(instance.PrototypeId, out var proto))
            return;

        // Only show when at least a name or description localization key is configured.
        if (!proto.Name.HasValue && !proto.Desc.HasValue)
            return;

        // Ensure per-player visit tracking and skip if already announced.
        if (!_visitedByPlayer.TryGetValue(ent, out var visited))
        {
            visited = new HashSet<string>();
            _visitedByPlayer[ent] = visited;
        }

        if (!visited.Add(proto.ID))
            return;

        // Send the popup to the controlling player session.
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        var ev = new CEScreenPopupShowEvent
        {
            Title = proto.Name,
            Desc = proto.Desc,
            Sound = proto.EntrySound,
        };
        RaiseNetworkEvent(ev, actor.PlayerSession);
    }
}
