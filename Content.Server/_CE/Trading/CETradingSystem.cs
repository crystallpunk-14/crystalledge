using Content.Shared._CE.CollectOnContact;
using Content.Shared._CE.Currency;
using Content.Shared._CE.Currency.Components;
using Content.Shared._CE.Trading;
using Content.Shared._CE.Trading.Components;
using Content.Shared.Hands;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Content.Shared.Tag;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Trading;

public sealed partial class CETradingSystem : EntitySystem
{
    [Dependency] private CECurrencySystem _currency = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private SharedPopupSystem _popup = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private SharedStorageSystem _storage = default!;
    [Dependency] private TagSystem _tag = default!;
    [Dependency] private IPrototypeManager _proto = default!;
    [Dependency] private IRobustRandom _random = default!;

    [Dependency] private EntityQuery<CECurrencyComponent> _currencyQuery = default!;
    [Dependency] private EntityQuery<ContainerManagerComponent> _containerQuery = default!;
    [Dependency] private EntityQuery<StackComponent> _stackQuery = default!;
    [Dependency] private EntityQuery<CEStackPriceComponent> _stackPriceQuery = default!;
    [Dependency] private EntityQuery<CEStaticPriceComponent> _staticPriceQuery = default!;
    [Dependency] private EntityQuery<StorageComponent> _storageQuery = default!;
    [Dependency] private EntityQuery<CECollectOnContactTargetComponent> _targetQuery = default!;

    private readonly ProtoId<TagPrototype> _walletTag = "CEWallet";
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETradingSlotComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CETradingSlotComponent, InteractHandEvent>(OnInteractHand);
    }

    private void OnMapInit(Entity<CETradingSlotComponent> ent, ref MapInitEvent args)
    {
        RefreshSlot(ent);
    }

    private void OnInteractHand(Entity<CETradingSlotComponent> ent, ref InteractHandEvent args)
    {
        if (args.Handled)
            return;

        if (ent.Comp.ActiveOffer == null)
            return;

        var player = args.User;

        if (_currency.GetPriceTotal(player) < ent.Comp.Price)
        {
            _popup.PopupEntity(Loc.GetString("ce-trading-not-enough-money"), player, player, PopupType.SmallCaution);
            return;
        }

        if (!TryTakeCurrency(player, ent.Comp.Price))
            return;

        var slotCoords = Transform(ent).Coordinates;
        var playerCoords = Transform(player).Coordinates;

        var offerArgs = new CETradingOfferArgs(EntityManager, player, slotCoords);
        var pickedUp = ent.Comp.ActiveOffer.Effect(offerArgs);

        // PlayPickupAnimation on the server excludes the user from its filter,
        // so the buyer never sees it. Explicitly send the animation only to them.
        if (pickedUp is { } item)
        {
            RaiseNetworkEvent(
                new PickupAnimationEvent(
                    GetNetEntity(item),
                    GetNetCoordinates(slotCoords),
                    GetNetCoordinates(playerCoords),
                    Angle.Zero),
                Filter.Entities(player));
        }

        _audio.PlayPvs(new SoundPathSpecifier("/Audio/_CE/Effects/cash.ogg"), player);
        args.Handled = true;
        DeactivateSlot(ent);
    }

    private void DeactivateSlot(Entity<CETradingSlotComponent> slot)
    {
        slot.Comp.ActiveOffer = null;
        slot.Comp.ActivePreviewProto = null;
        Dirty(slot.Owner, slot.Comp);
    }

    public void RefreshSlot(Entity<CETradingSlotComponent> slot)
    {
        if (slot.Comp.Offers.Count == 0)
            return;

        var offer = _random.Pick(slot.Comp.Offers);
        slot.Comp.ActiveOffer = offer;
        offer.UpdateSlotVisuals(slot.Owner, EntityManager, _proto, _random);
        Dirty(slot.Owner, slot.Comp);
    }

    private bool TryTakeCurrency(EntityUid player, int amount)
    {
        if (amount <= 0)
            return true;

        if (!_containerQuery.TryGetComponent(player, out var initial))
            return false;

        var coins = new List<(EntityUid uid, int unitPrice, int count)>();
        var containerStack = new Stack<ContainerManagerComponent>();
        containerStack.Push(initial);

        do
        {
            var current = containerStack.Pop();
            foreach (var container in current.Containers.Values)
            foreach (var contained in container.ContainedEntities)
            {
                if (_currencyQuery.HasComponent(contained))
                {
                    var unitPrice = GetUnitPrice(contained);
                    var count = _stackQuery.TryGetComponent(contained, out var stack) ? stack.Count : 1;
                    if (unitPrice > 0)
                        coins.Add((contained, unitPrice, count));
                }

                if (_containerQuery.TryGetComponent(contained, out var nested))
                    containerStack.Push(nested);
            }
        } while (containerStack.Count > 0);

        // Greedy: highest denomination first
        coins.Sort((a, b) => b.unitPrice.CompareTo(a.unitPrice));

        var remaining = amount;
        var overpaid = 0;

        foreach (var (uid, unitPrice, count) in coins)
        {
            if (remaining <= 0)
                break;

            var totalValue = unitPrice * count;
            if (totalValue <= remaining)
            {
                remaining -= totalValue;
                QueueDel(uid);
            }
            else
            {
                var coinsNeeded = (remaining + unitPrice - 1) / unitPrice; // ceiling division
                overpaid = coinsNeeded * unitPrice - remaining;
                remaining = 0;

                if (coinsNeeded >= count)
                    QueueDel(uid);
                else
                    _stack.SetCount(uid, count - coinsNeeded);
            }
        }

        if (overpaid > 0)
            SpawnChange(player, overpaid);

        return true;
    }

    private int GetUnitPrice(EntityUid uid)
    {
        if (_stackPriceQuery.TryGetComponent(uid, out var sp))
            return (int) sp.Price;
        if (_staticPriceQuery.TryGetComponent(uid, out var fp))
            return (int) fp.Price;
        return 0;
    }

    private void SpawnChange(EntityUid player, int change)
    {
        var coords = Transform(player).Coordinates;
        var wallet = FindWallet(player);

        SpawnAndStore(CECurrencySystem.PP.Key, change / 1000, coords, wallet);
        change %= 1000;
        SpawnAndStore(CECurrencySystem.GP.Key, change / 100, coords, wallet);
        change %= 100;
        SpawnAndStore(CECurrencySystem.SP.Key, change / 10, coords, wallet);
        change %= 10;
        SpawnAndStore(CECurrencySystem.CP.Key, change, coords, wallet);
    }

    private void SpawnAndStore(EntProtoId proto, int count, EntityCoordinates coords, EntityUid? wallet)
    {
        if (count <= 0)
            return;

        var coin = Spawn(proto, coords);
        _stack.SetCount(coin, count);

        if (wallet.HasValue)
            _storage.Insert(wallet.Value, coin, out _, playSound: false);
    }

    private EntityUid? FindWallet(EntityUid player)
    {
        if (!_containerQuery.TryGetComponent(player, out var initial))
            return null;

        var containerStack = new Stack<ContainerManagerComponent>();
        containerStack.Push(initial);

        do
        {
            var current = containerStack.Pop();
            foreach (var container in current.Containers.Values)
            foreach (var item in container.ContainedEntities)
            {
                if (_targetQuery.HasComponent(item)
                    && _tag.HasTag(item, _walletTag)
                    && _storageQuery.HasComponent(item))
                    return item;

                if (_containerQuery.TryGetComponent(item, out var nested))
                    containerStack.Push(nested);
            }
        } while (containerStack.Count > 0);

        return null;
    }
}
