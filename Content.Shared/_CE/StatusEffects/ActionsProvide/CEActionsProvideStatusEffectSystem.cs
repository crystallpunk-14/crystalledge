using Content.Shared.Actions;
using Content.Shared.StatusEffectNew;

namespace Content.Shared._CE.StatusEffects.ActionsProvide;

/// <summary>
/// Handles <see cref="CEActionsProvideStatusEffectComponent"/>:
/// grants configured actions when the status effect is applied,
/// and removes them when the status effect ends.
/// </summary>
public sealed partial class CEActionsProvideStatusEffectSystem : EntitySystem
{
    [Dependency] private SharedActionsSystem _actions = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEActionsProvideStatusEffectComponent, StatusEffectAppliedEvent>(OnApplied);
        SubscribeLocalEvent<CEActionsProvideStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnApplied(Entity<CEActionsProvideStatusEffectComponent> ent, ref StatusEffectAppliedEvent args)
    {
        foreach (var actionProto in ent.Comp.Actions)
        {
            var actionEnt = _actions.AddAction(args.Target, actionProto);
            if (actionEnt != null)
                ent.Comp.ActionEntities.Add(actionEnt.Value);
        }
    }

    private void OnRemoved(Entity<CEActionsProvideStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        foreach (var actionEnt in ent.Comp.ActionEntities)
        {
            _actions.RemoveAction(actionEnt);
        }

        ent.Comp.ActionEntities.Clear();
    }
}
