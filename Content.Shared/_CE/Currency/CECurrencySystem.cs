using Content.Shared._CE.Currency.Components;
using Content.Shared.Examine;
using Content.Shared.Stacks;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Currency;

public sealed partial class CECurrencySystem : EntitySystem
{
    public static readonly KeyValuePair<EntProtoId, int> CP = new("CECoinCopper1", 1);
    public static readonly KeyValuePair<EntProtoId, int> SP = new("CECoinSilver1", 10);
    public static readonly KeyValuePair<EntProtoId, int> GP = new("CECoinGold1", 100);
    public static readonly KeyValuePair<EntProtoId, int> PP = new("CECoinPlatinum1", 1000);

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
