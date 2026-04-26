using Content.Server._CE.Procedural.Instance.Components;
using Content.Server._CE.Procedural.Overview;
using Content.Server._CE.Procedural.Prototypes;
using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.ScreenPopup;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

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
        SubscribeLocalEvent<CEDungeonPlayerComponent, MapInitEvent>(OnPlayerMapInit);
        SubscribeLocalEvent<CEDungeonPlayerComponent, EntParentChangedMessage>(OnPlayerParentChanged);
        SubscribeLocalEvent<CEDungeonPlayerComponent, ComponentShutdown>(OnPlayerShutdown);
    }

    private void OnPlayerMapInit(Entity<CEDungeonPlayerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.SessionStartedAt = _timing.CurTime;
    }

    private void OnPlayerShutdown(Entity<CEDungeonPlayerComponent> ent, ref ComponentShutdown args)
    {
        _visitedByPlayer.Remove(ent);
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

        // Broadcast a level-change event so any open dungeon overview UIs can refresh.
        // Done before popup gating because the broadcast has to fire on every transition.
        ProtoId<CEDungeonLevelPrototype>? fromLevelId = null;
        if (oldMapUid is { } oldUid && TryResolveInstance(oldUid, out var oldInst))
            fromLevelId = oldInst.PrototypeId;

        if (fromLevelId != instance.PrototypeId)
        {
            var changedEv = new CEDungeonPlayerLevelChangedEvent(ent.Owner, fromLevelId, instance.PrototypeId);
            // Broadcast in addition to targeting the entity, so the dungeon-overview systems
            // (which subscribe globally without a component filter) still receive the event.
            RaiseLocalEvent(ent.Owner, ref changedEv, broadcast: true);
        }

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

        TrackLevelReached(ent, proto.ID);

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
