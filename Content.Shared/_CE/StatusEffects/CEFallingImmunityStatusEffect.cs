using Content.Shared._CE.ZLevels.Damage;
using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEFallingImmunityStatusEffectComponent : Component
{
    [DataField, AutoNetworkedField]
    public float DamageMultiplier = 1f;

    [DataField, AutoNetworkedField]
    public float StunMultiplier = 1f;
}


public sealed class CEFallingImmunityStatusEffectSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFallingImmunityStatusEffectComponent, StatusEffectRelayedEvent<CEZFallingDamageCalculateEvent>>(OnFall);
    }

    private void OnFall(Entity<CEFallingImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEZFallingDamageCalculateEvent> args)
    {
        args.Args.DamageMultiplier *= ent.Comp.DamageMultiplier;
        args.Args.StunMultiplier *= ent.Comp.StunMultiplier;
    }
}
