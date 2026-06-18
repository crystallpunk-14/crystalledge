using Content.Shared._CE.Currency.Components;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Currency;

public sealed partial class CECurrencySystem : EntitySystem
{
    public static readonly KeyValuePair<EntProtoId, int> CP = new("CECoinCopper1", 1);
    public static readonly KeyValuePair<EntProtoId, int> SP = new("CECoinSilver1", 10);
    public static readonly KeyValuePair<EntProtoId, int> GP = new("CECoinGold1", 100);
    public static readonly KeyValuePair<EntProtoId, int> PP = new("CECoinPlatinum1", 1000);

    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedStackSystem _stack = default!;
    [Dependency] private EntityQuery<ContainerManagerComponent> _containerQuery = default!;
    [Dependency] private EntityQuery<CECurrencyComponent> _currencyQuery = default!;
    [Dependency] private EntityQuery<CEStackPriceComponent> _stackPriceQuery = default!;
    [Dependency] private EntityQuery<CEStaticPriceComponent> _staticPriceQuery = default!;
    [Dependency] private EntityQuery<StackComponent> _stackQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CECurrencyExaminableComponent, ExaminedEvent>(OnExamine);
    }

    public int GetPrice(EntityUid ent)
    {
        if (_stackPriceQuery.TryGetComponent(ent, out var sp) && _stackQuery.TryGetComponent(ent, out var stack))
            return (int)(sp.Price * stack.Count);
        if (_staticPriceQuery.TryGetComponent(ent, out var fp))
            return (int)fp.Price;
        return 0;
    }

    public int GetPriceTotal(EntityUid ent)
    {
        if (!_containerQuery.TryGetComponent(ent, out var initial))
            return 0;

        var total = 0;
        var containerStack = new Stack<ContainerManagerComponent>();
        var current = initial;
        do
        {
            foreach (var container in current.Containers.Values)
            foreach (var contained in container.ContainedEntities)
            {
                if (_currencyQuery.HasComponent(contained))
                    total += GetPrice(contained);
                if (_containerQuery.TryGetComponent(contained, out var nested))
                    containerStack.Push(nested);
            }
        } while (containerStack.TryPop(out current));

        return total;
    }

    /// <summary>
    /// Spawns coins totalling <paramref name="amount"/> at <paramref name="coords"/> and returns the spawned entities.
    /// Uses greedy denomination to minimise the number of entities spawned.
    /// Returns an empty list on the client.
    /// </summary>
    public List<EntityUid> SpawnCurrency(int amount, EntityCoordinates coords)
    {
        if (_net.IsClient)
            return new List<EntityUid>();

        var counts = new Dictionary<EntProtoId, int>();
        foreach (var proto in GetCoinProtos(amount))
        {
            counts.TryGetValue(proto, out var existing);
            counts[proto] = existing + 1;
        }

        var spawned = new List<EntityUid>(counts.Count);
        foreach (var (proto, count) in counts)
        {
            if (count <= 0)
                continue;
            var coin = Spawn(proto, coords);
            _stack.SetCount(coin, count);
            spawned.Add(coin);
        }

        return spawned;
    }

    /// <summary>
    /// Returns the proto IDs of coins that make up <paramref name="amount"/>.
    /// Each yielded ID represents one coin entity to spawn.
    /// </summary>
    /// <param name="largeCoinChance">
    /// 0.0 = all smallest denomination; 1.0 = greedy (fewest coins possible).
    /// For change use 1.0; for treasure chests use ~0.3–0.6.
    /// </param>
    public static IEnumerable<EntProtoId> GetCoinProtos(int amount, float largeCoinChance = 1.0f, System.Random? rand = null)
    {
        rand ??= new System.Random();

        var cp = amount;
        var sp = 0;
        var gp = 0;
        var pp = 0;

        var spGroups = cp / SP.Value;
        for (var i = 0; i < spGroups; i++)
        {
            if (rand.NextDouble() < largeCoinChance)
            {
                cp -= SP.Value;
                sp++;
            }
        }

        var gpGroups = sp / (GP.Value / SP.Value);
        for (var i = 0; i < gpGroups; i++)
        {
            if (rand.NextDouble() < largeCoinChance)
            {
                sp -= GP.Value / SP.Value;
                gp++;
            }
        }

        var ppGroups = gp / (PP.Value / GP.Value);
        for (var i = 0; i < ppGroups; i++)
        {
            if (rand.NextDouble() < largeCoinChance)
            {
                gp -= PP.Value / GP.Value;
                pp++;
            }
        }

        for (var i = 0; i < pp; i++) yield return PP.Key;
        for (var i = 0; i < gp; i++) yield return GP.Key;
        for (var i = 0; i < sp; i++) yield return SP.Key;
        for (var i = 0; i < cp; i++) yield return CP.Key;
    }

    public string GetCurrencyPrettyString(int currency)
    {
        var total = currency;
        var result = string.Empty;

        var gp = total / 100;
        total %= 100;
        var sp = total / 10;
        total %= 10;
        var cp = total;

        if (gp > 0)
            result += " " + Loc.GetString("ce-currency-examine-gp", ("coin", gp));
        if (sp > 0)
            result += " " + Loc.GetString("ce-currency-examine-sp", ("coin", sp));
        if (cp > 0)
            result += " " + Loc.GetString("ce-currency-examine-cp", ("coin", cp));
        if (gp <= 0 && sp <= 0 && cp <= 0)
            result += " " + Loc.GetString("ce-trading-empty-price");

        return result;
    }

    private void OnExamine(Entity<CECurrencyExaminableComponent> currency, ref ExaminedEvent args)
    {
        var price = GetPrice(currency);
        var push = Loc.GetString("ce-currency-examine-title");
        push += GetCurrencyPrettyString(price);
        args.PushMarkup(push);
    }
}
