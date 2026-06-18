using Content.Shared._CE.Rarity.Components;
using Content.Shared._CE.Rarity.Prototypes;
using Content.Shared.Examine;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Rarity;

public sealed partial class CESharedRaritySystem : EntitySystem
{
    [Dependency] private IPrototypeManager _prototype = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CERarityComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<CERarityComponent> ent, ref ExaminedEvent args)
    {
        if (!_prototype.TryIndex(ent.Comp.Rarity, out var rarity))
            return;

        args.PushMarkup(Loc.GetString("ce-rarity-examine",
            ("name", Loc.GetString(rarity.Name)),
            ("color", rarity.Color.ToHexNoAlpha())));
    }
}
