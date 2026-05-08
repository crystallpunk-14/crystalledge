using Content.Shared._CE.TileEffects.Core;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.StatusEffects.AdditionalTileEffect;

public sealed partial class CEAdditionalTileEffectStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly CETileEffectSystem _tileEffect = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEAdditionalTileEffectStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent>>(OnApplyTileEffect);
    }

    private void OnApplyTileEffect(
        Entity<CEAdditionalTileEffectStatusEffectComponent> ent,
        ref StatusEffectRelayedEvent<CEAttemptApplyTileEffectEvent> args)
    {
        if (args.Args.Cancelled || args.Args.TileEffect != ent.Comp.SourceTileEffect)
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var status))
            return;

        _tileEffect.TryApplyTileEffect(ent.Comp.AdditionalTileEffect, status.AppliedTo, args.Args.Coordinates, ent.Comp.AdditionalTileEffectAmount);
    }
}
