using Content.Shared._CE.Soul;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Adds souls to the resolved target's <see cref="Soul.Components.CESoulContainerComponent"/>.
/// </summary>
public sealed partial class AddSoul : CEEntityEffectBase<AddSoul>
{
    [DataField]
    public int Amount = 1;
}

public sealed partial class CEAddSoulEffectSystem : CEEntityEffectSystem<AddSoul>
{
    [Dependency] private CESharedSoulSystem _souls = default!;

    protected override void Effect(ref CEEntityEffectEvent<AddSoul> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        _souls.TryAddSouls(entity, args.Effect.Amount);
    }
}
