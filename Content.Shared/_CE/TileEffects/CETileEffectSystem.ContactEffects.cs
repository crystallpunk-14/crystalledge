using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.Whitelist;

namespace Content.Shared._CE.TileEffects;

public sealed partial class CETileEffectSystem
{
    [Dependency] private readonly EntityWhitelistSystem _whitelist = default!;

    private void InitializeContactEffects()
    {
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

            var current = _stack.GetStack(ent, effectId);
            stacks = Math.Min(stacks, stacks * 2 - current); //TODO: dehardcode max stacks

            _stack.TryAddStack(other, effectId, out _, stacks, source: tileComp.Applier);
        }
    }
}
