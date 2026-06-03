using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.TileEffects.Core;
using Content.Shared.Whitelist;
using Robust.Shared.Network;

namespace Content.Shared._CE.TileEffects.ContactEffect;

public sealed partial class CETileEffectContactEffectsSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformComponent, CEAffectedByTileEffectEvent>(OnContactEffect);
    }

    private void OnContactEffect(Entity<TransformComponent> ent, ref CEAffectedByTileEffectEvent args)
    {
        if (_net.IsClient)
            return;

        if (!TryComp<CETileEffectContactEffectsComponent>(args.TileEffect.Owner, out var contactComp))
            return;

        var tileComp = args.TileEffect.Comp;
        var other = args.AffectedEntity;

        if (!_whitelist.CheckBoth(other, tileComp.Blacklist, tileComp.Whitelist))
            return;

        foreach (var (effectId, baseAmount) in contactComp.ContactEffects)
        {
            var stacks = baseAmount * tileComp.Stacks;
            if (stacks <= 0)
                continue;

            _stack.TryAddStack(other, effectId, out _, stacks, source: tileComp.Source, max: stacks);
        }
    }
}
