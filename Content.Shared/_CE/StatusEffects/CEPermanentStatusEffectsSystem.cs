using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects;

public sealed partial class CEPermanentStatusEffectsSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffects = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEPermanentStatusEffectsComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CEPermanentStatusEffectsComponent> ent, ref MapInitEvent args)
    {
        foreach (var effect in ent.Comp.Effects)
        {
            _statusEffects.TrySetStatusEffectDuration(ent, effect);
        }
    }
}
