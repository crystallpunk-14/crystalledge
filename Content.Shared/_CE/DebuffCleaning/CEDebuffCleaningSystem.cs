using Content.Shared._CE.StatusEffects.Core.Components;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;

namespace Content.Shared._CE.DebuffCleaning;

public sealed class CEDebuffCleaningSystem : EntitySystem
{
    [Dependency] private readonly StatusEffectsSystem _statusEffect = default!;

    public void ClearDebuffs(EntityUid target, EntityUid? source)
    {
        if (!_statusEffect.TryEffectsWithComp<RejuvenateRemovedStatusEffectComponent>(target, out var debuffs))
            return;

        var counter = 0;
        foreach (var debuff in debuffs)
        {
            if (TryComp<CEStatusEffectStackComponent>(debuff, out var stack))
            {
                counter += stack.Stacks;
            }
            else
            {
                counter++;
            }

            QueueDel(debuff);
        }

        RaiseLocalEvent(target, new CECleanedDebuffsEvent(source, counter));

        if (source.HasValue && counter > 0)
            RaiseLocalEvent(source.Value, new CESourceCleanedDebuffsEvent(target, counter));
    }
}

/// <summary>
/// Raised on the entity whose debuffs were cleansed.
/// </summary>
public sealed class CECleanedDebuffsEvent(EntityUid? source, int stacksRemoved) : EntityEventArgs
{
    public EntityUid? Source = source;
    public int StacksRemoved = stacksRemoved;
}

/// <summary>
/// Raised on the entity that performed the debuff cleanse.
/// </summary>
public sealed class CESourceCleanedDebuffsEvent(EntityUid target, int stacksRemoved) : EntityEventArgs
{
    public EntityUid Target = target;
    public int StacksRemoved = stacksRemoved;
}
