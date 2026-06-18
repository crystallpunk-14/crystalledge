using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Shared._CE.MetaChest;
using Content.Shared.Interaction;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Verbs;
using Robust.Server.Player;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Map;
using Robust.Shared.Maths;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.MetaChest;

public sealed partial class CEMetaChestSystem : EntitySystem
{
    [Dependency] private IServerDbManager _db = default!;
    [Dependency] private MapLoaderSystem _mapLoader = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private SharedUserInterfaceSystem _ui = default!;
    [Dependency] private IPlayerManager _playerManager = default!;
    [Dependency] private SharedTransformSystem _transform = default!;

    [Dependency] private EntityQuery<ActorComponent> _actorQuery = default!;
    [Dependency] private EntityQuery<CEMetaChestPersonalComponent> _personalQuery = default!;
    [Dependency] private EntityQuery<StorageComponent> _storageQuery = default!;

    private static readonly EntProtoId ShadowProto = "CEMetaChestPersonal";

    // (TargetUserId, ChestSlot) → shadow entity currently active for that slot
    private readonly Dictionary<(Guid, int), EntityUid> _activeShadows = new();

    // ActorUserId → (TargetUserId, ChestSlot) of the shadow this actor currently has open
    private readonly Dictionary<Guid, (Guid, int)> _actorActiveKey = new();

    // Slots currently being opened asynchronously — prevents double-open race
    private readonly HashSet<(Guid, int)> _reservedSlots = new();

    // Pending: fires when a specific (targetId, slot) shadow closes (e.g. player waiting for admin to leave)
    private readonly Dictionary<(Guid, int), Action> _pendingSlotOpens = new();

    // Pending: fires when a specific actor's current shadow closes (e.g. actor switching to a different chest)
    private readonly Dictionary<Guid, Action> _pendingActorOpens = new();

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEMetaChestComponent, InteractHandEvent>(OnInteract);
        SubscribeLocalEvent<CEMetaChestComponent, GetVerbsEvent<InteractionVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CEMetaChestPersonalComponent, BoundUIClosedEvent>(OnPersonalClosed);
        SubscribeLocalEvent<CEMetaChestPersonalComponent, ComponentShutdown>(OnPersonalShutdown);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
    }

    // ── Public API ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Opens a target player's chest for an admin actor.
    /// Returns false only when the target player themselves currently has that slot open.
    /// </summary>
    public bool TryOpenAdminMetaChest(EntityUid adminMob, ICommonSession adminSession, Guid targetUserId, int chestSlot)
    {
        // Only block if target PLAYER (non-admin session) has the slot open
        if (_activeShadows.TryGetValue((targetUserId, chestSlot), out var existing) &&
            _personalQuery.TryGetComponent(existing, out var existingComp) &&
            !existingComp.IsAdminSession)
        {
            return false;
        }

        RequestOpen(
            actorUserId: adminSession.UserId,
            targetUserId: targetUserId,
            chestSlot: chestSlot,
            actorMob: adminMob,
            referenceEntity: adminMob);
        return true;
    }

    // ── Interaction handlers ─────────────────────────────────────────────────────

    private void OnInteract(Entity<CEMetaChestComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        TryOpenAsPlayer(args.User, ent.Owner, ent.Comp.ChestSlot);
    }

    private void OnGetVerbs(Entity<CEMetaChestComponent> ent, ref GetVerbsEvent<InteractionVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        var chest = ent.Owner;
        var slot = ent.Comp.ChestSlot;

        args.Verbs.Add(new InteractionVerb
        {
            Text = "Open",
            Act = () => TryOpenAsPlayer(user, chest, slot),
        });
    }

    // ── Core open logic ──────────────────────────────────────────────────────────

    private void TryOpenAsPlayer(EntityUid actor, EntityUid chestEntity, int chestSlot)
    {
        if (!_actorQuery.TryGetComponent(actor, out var actorComp))
            return;

        Guid userId = actorComp.PlayerSession.UserId;
        RequestOpen(
            actorUserId: userId,
            targetUserId: userId,
            chestSlot: chestSlot,
            actorMob: actor,
            referenceEntity: chestEntity);
    }

    /// <summary>
    /// Unified entry point for all open requests. Handles sequencing:
    /// if the actor already has a shadow, close it first then open the new one.
    /// If the target slot has an admin shadow, evict it first.
    /// </summary>
    private void RequestOpen(Guid actorUserId, Guid targetUserId, int chestSlot, EntityUid actorMob, EntityUid referenceEntity)
    {
        var targetKey = (targetUserId, chestSlot);
        Action openAction = () => SpawnShadow(actorUserId, targetUserId, chestSlot, actorMob, referenceEntity);

        // Actor already has a shadow open — close it first, then do the new open
        if (_actorActiveKey.TryGetValue(actorUserId, out var currentKey))
        {
            _pendingActorOpens[actorUserId] = openAction;
            if (_activeShadows.TryGetValue(currentKey, out var ownShadow))
                ForceCloseShadow(ownShadow, actorUserId);
            return;
        }

        // Target slot occupied by an admin shadow — evict it, then open
        if (_activeShadows.TryGetValue(targetKey, out var slotShadow) &&
            _personalQuery.TryGetComponent(slotShadow, out var slotComp) &&
            slotComp.IsAdminSession)
        {
            _pendingSlotOpens[targetKey] = openAction;
            ForceCloseShadow(slotShadow, slotComp.ActorUserId);
            return;
        }

        // Slot already taken (player session) or currently being opened — ignore
        if (_activeShadows.ContainsKey(targetKey) || _reservedSlots.Contains(targetKey))
            return;

        openAction();
    }

    private void SpawnShadow(Guid actorUserId, Guid targetUserId, int chestSlot, EntityUid actorMob, EntityUid referenceEntity)
    {
        _ = SpawnShadowAsync(actorUserId, targetUserId, chestSlot, actorMob, referenceEntity);
    }

    private async Task SpawnShadowAsync(Guid actorUserId, Guid targetUserId, int chestSlot, EntityUid actorMob, EntityUid referenceEntity)
    {
        var targetKey = (targetUserId, chestSlot);

        // Re-check: may have changed while a prior async was in flight
        if (_activeShadows.ContainsKey(targetKey) || _reservedSlots.Contains(targetKey))
            return;

        _reservedSlots.Add(targetKey); // reserve before first await to block concurrent opens
        try
        {
            if (!Exists(referenceEntity))
                return;

            var spawnCoords = Transform(referenceEntity).Coordinates;
            var shadow = Spawn(ShadowProto, spawnCoords);
            _transform.SetParent(shadow, referenceEntity);

            var shadowComp = EnsureComp<CEMetaChestPersonalComponent>(shadow);
            shadowComp.TargetUserId = targetUserId;
            shadowComp.ActorUserId = actorUserId;
            shadowComp.ChestSlot = chestSlot;
            shadowComp.IsAdminSession = actorUserId != targetUserId;

            _activeShadows[targetKey] = shadow;
            _actorActiveKey[actorUserId] = targetKey;
            _reservedSlots.Remove(targetKey);

            var items = await _db.GetMetaChestItems(targetUserId, chestSlot);

            if (!Exists(shadow))
                return;

            var fallbackCoords = Exists(referenceEntity)
                ? Transform(referenceEntity).Coordinates
                : Transform(shadow).Coordinates;

            foreach (var item in items)
                LoadAndInsertItem(item, shadow, fallbackCoords);

            if (!Exists(actorMob) || !_actorQuery.HasComponent(actorMob))
                return;

            _ui.TryOpenUi(shadow, StorageComponent.StorageUiKey.Key, actorMob);
        }
        catch (Exception e)
        {
            _reservedSlots.Remove(targetKey);
            Log.Error($"CEMetaChest: Failed to open ({targetUserId}, {chestSlot}) for actor {actorUserId}: {e}");
        }
    }

    private void ForceCloseShadow(EntityUid shadow, Guid actorUserId)
    {
        var actorMob = GetMobByUserId(actorUserId);
        if (actorMob != null)
            _ui.CloseUi(shadow, StorageComponent.StorageUiKey.Key, actorMob.Value);
        else
            _ui.CloseUi(shadow, StorageComponent.StorageUiKey.Key);
    }

    // ── Close / cleanup ──────────────────────────────────────────────────────────

    private void OnPersonalClosed(Entity<CEMetaChestPersonalComponent> ent, ref BoundUIClosedEvent args)
    {
        if (!Equals(args.UiKey, StorageComponent.StorageUiKey.Key))
            return;

        _ = SaveAndCleanupAsync(ent.Owner, ent.Comp);
    }

    private void OnPersonalShutdown(Entity<CEMetaChestPersonalComponent> ent, ref ComponentShutdown args)
    {
        var key = (ent.Comp.TargetUserId, ent.Comp.ChestSlot);

        // Guard: only act if THIS entity is the one tracked — orphan duplicates must not corrupt state
        if (!_activeShadows.TryGetValue(key, out var tracked) || tracked != ent.Owner)
            return;

        _activeShadows.Remove(key);
        _actorActiveKey.Remove(ent.Comp.ActorUserId);
        _reservedSlots.Remove(key);
        _pendingSlotOpens.Remove(key);
        _pendingActorOpens.Remove(ent.Comp.ActorUserId);

        _ = SaveItemsToDbAsync(ent.Owner, ent.Comp);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        if (ev.New == GameRunLevel.InRound)
            return;

        foreach (var (_, shadow) in _activeShadows.ToList())
        {
            if (_personalQuery.TryGetComponent(shadow, out var comp))
                _ = SaveItemsToDbAsync(shadow, comp);
        }
    }

    private async Task SaveAndCleanupAsync(EntityUid shadow, CEMetaChestPersonalComponent comp)
    {
        try
        {
            await SaveItemsToDbAsync(shadow, comp);

            if (Exists(shadow))
            {
                if (_storageQuery.TryGetComponent(shadow, out var storage))
                {
                    foreach (var entity in storage.StoredItems.Keys.ToList())
                        QueueDel(entity);
                }
                QueueDel(shadow);
            }

            var key = (comp.TargetUserId, comp.ChestSlot);
            _activeShadows.Remove(key);
            _actorActiveKey.Remove(comp.ActorUserId);
            _reservedSlots.Remove(key);

            // 1. Unblock anything waiting for this slot (e.g. player waiting for admin to leave)
            if (_pendingSlotOpens.Remove(key, out var slotCb))
                slotCb();

            // 2. Let the actor open their next chest (e.g. admin switching between players)
            if (_pendingActorOpens.Remove(comp.ActorUserId, out var actorCb))
                actorCb();
        }
        catch (Exception e)
        {
            Log.Error($"CEMetaChest: Save/cleanup failed for ({comp.TargetUserId}, {comp.ChestSlot}): {e}");
        }
    }

    private async Task SaveItemsToDbAsync(EntityUid shadow, CEMetaChestPersonalComponent comp)
    {
        var items = new List<PlayerMetaChestItem>();

        if (Exists(shadow) && _storageQuery.TryGetComponent(shadow, out var storage))
        {
            foreach (var (entity, location) in storage.StoredItems.ToList())
            {
                if (!Exists(entity))
                    continue;

                var writer = new StringWriter();
                if (!_mapLoader.TrySaveEntity(entity, writer))
                    continue;

                items.Add(new PlayerMetaChestItem
                {
                    PlayerUserId = comp.TargetUserId,
                    ChestSlot = comp.ChestSlot,
                    ItemYaml = writer.ToString(),
                    GridX = location.Position.X,
                    GridY = location.Position.Y,
                    GridRotation = (byte) location.Direction,
                });
            }
        }

        await _db.SaveMetaChestItems(comp.TargetUserId, comp.ChestSlot, items);
    }

    private void LoadAndInsertItem(PlayerMetaChestItem record, EntityUid shadow, EntityCoordinates fallbackCoords)
    {
        if (!_mapLoader.TryLoadGeneric(new StringReader(record.ItemYaml), "metachest", out var result))
            return;

        var candidates = result.NullspaceEntities.Count > 0
            ? (IEnumerable<EntityUid>) result.NullspaceEntities
            : result.Orphans;

        foreach (var entity in candidates)
        {
            var location = new ItemStorageLocation(default, new Vector2i(record.GridX, record.GridY))
            {
                Direction = (Direction) record.GridRotation
            };

            if (!_storage.InsertAt(shadow, entity, location, out _))
                _transform.SetCoordinates(entity, fallbackCoords);

            break; // One entity per DB record — bags carry their children recursively
        }
    }

    private EntityUid? GetMobByUserId(Guid userId)
    {
        foreach (var session in _playerManager.Sessions)
        {
            if ((Guid) session.UserId == userId)
                return session.AttachedEntity;
        }
        return null;
    }
}
