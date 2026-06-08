using Content.Shared._CE.DebuffCleaning;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Remove all debuffs from entity
/// </summary>
public sealed partial class CleanDebuffs : CEEntityEffectBase<CleanDebuffs>
{
}

public sealed partial class CECleanDebuffsSystem : CEEntityEffectSystem<CleanDebuffs>
{
    [Dependency] private CEDebuffCleaningSystem _debuffClean = default!;

    protected override void Effect(ref CEEntityEffectEvent<CleanDebuffs> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _debuffClean.ClearDebuffs(entity, args.Args.Source);
    }
}
