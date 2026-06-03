using Content.Shared.StatusEffectNew;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

/// <summary>
/// Adds permanent status effects to the entity when the component is initialized.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEPermanentStatusEffectsComponent : Component
{
    [DataField(required: true)]
    public HashSet<EntProtoId> Effects = new();
}


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
