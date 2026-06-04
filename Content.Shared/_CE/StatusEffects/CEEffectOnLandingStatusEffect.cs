using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent]
public sealed partial class CEEffectOnLandingStatusEffectComponent : Component
{
    [DataField(required: true)]
    public List<CEEntityEffect> Effects = new();
}

public sealed class CEEffectOnLandingStatusEffectSystem : EntitySystem
{
    [Dependency] private readonly EntityQuery<StatusEffectComponent> _effectQuery = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEffectOnLandingStatusEffectComponent, StatusEffectRelayedEvent<CEZLevelHitEvent>>(OnFall);
    }

    private void OnFall(Entity<CEEffectOnLandingStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEZLevelHitEvent> args)
    {
        if (!_effectQuery.TryComp(ent, out var effectComp) || effectComp.AppliedTo is null)
            return;

        var effectArgs = new CEEntityEffectArgs(EntityManager,
            effectComp.AppliedTo.Value,
            null,
            Angle.Zero,
            1f,
            effectComp.AppliedTo.Value,
            null);

        foreach (var effect in ent.Comp.Effects)
        {
            effect.Effect(effectArgs);
        }
    }
}
