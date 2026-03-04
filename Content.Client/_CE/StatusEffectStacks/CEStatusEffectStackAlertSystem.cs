using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Alert.Components;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Client._CE.StatusEffectStacks;

/// <summary>
/// Client-side system that provides the current stack count to <see cref="GenericCounterAlertComponent"/>
/// for status effects that use <see cref="CEStatusEffectStackComponent"/>.
///
/// When a <see cref="GetGenericAlertCounterAmountEvent"/> is raised on a mob,
/// we iterate its active status effects and look for one whose
/// <see cref="StatusEffectAlertComponent"/> alert matches the requested alert,
/// then return the stack count stored in <see cref="CEStatusEffectStackComponent"/>.
/// </summary>
public sealed class CEStatusEffectStackAlertSystem : EntitySystem
{
    private EntityQuery<StatusEffectAlertComponent> _alertCompQuery;
    private EntityQuery<CEStatusEffectStackComponent> _stackCompQuery;

    public override void Initialize()
    {
        base.Initialize();

        _alertCompQuery = GetEntityQuery<StatusEffectAlertComponent>();
        _stackCompQuery = GetEntityQuery<CEStatusEffectStackComponent>();

        SubscribeLocalEvent<StatusEffectContainerComponent, GetGenericAlertCounterAmountEvent>(OnGetCounterAmount);
    }

    private void OnGetCounterAmount(Entity<StatusEffectContainerComponent> ent, ref GetGenericAlertCounterAmountEvent args)
    {
        if (args.Handled)
            return;

        foreach (var effectEnt in ent.Comp.ActiveStatusEffects?.ContainedEntities ?? [])
        {
            if (!_alertCompQuery.TryComp(effectEnt, out var alertComp))
                continue;

            if (alertComp.Alert != args.Alert)
                continue;

            if (!_stackCompQuery.TryComp(effectEnt, out var stackComp))
                continue;

            args.Amount = stackComp.Stack;
            return;
        }
    }
}
