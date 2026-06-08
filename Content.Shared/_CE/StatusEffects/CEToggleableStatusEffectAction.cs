using Content.Shared._CE.StatusEffects.Core;
using Content.Shared.Actions;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.StatusEffects;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEToggleableActionComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active;
}

public sealed partial class CEToggleableStatusEffectActionSystem: EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;
    [Dependency] private CEStatusEffectStackSystem _stack = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<TransformComponent, CEToggleableStatusEffectEvent>(OnToggle);
    }

    private void OnToggle(Entity<TransformComponent> ent, ref CEToggleableStatusEffectEvent args)
    {
        if (args.Handled)
            return;

        var toggleableAction = EnsureComp<CEToggleableActionComponent>(args.Action);

        var active = !toggleableAction.Active;
        _actions.SetToggled(args.Action.Owner, active);
        toggleableAction.Active = active;
        Dirty(args.Action, toggleableAction);

        if (active)
        {
            foreach (var (effectProto, amount) in args.Effects)
            {
                _stack.TryAddStack(args.Performer, effectProto, out _, amount, source: args.Performer);
            }
        }
        else
        {
            foreach (var (effectProto, amount) in args.Effects)
            {
                _stack.TryRemoveStack(args.Performer, effectProto, amount);
            }
        }

        args.Handled = true;
    }
}

public sealed partial class CEToggleableStatusEffectEvent : InstantActionEvent
{
    [DataField(required: true)]
    public Dictionary<EntProtoId, int> Effects = new();
}
