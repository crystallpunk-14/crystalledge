using System.Linq;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Skill.Core.Effects;

public sealed partial class AddStatusEffect : CESkillEffect
{
    [DataField(required: true)]
    public EntProtoId Effect;

    public override LocId SkillType => "ce-skill-effect-passive";

    public override void AddSkill(IEntityManager entManager, EntityUid target)
    {
        var statusEffectSystem = entManager.System<StatusEffectsSystem>();
        statusEffectSystem.TrySetStatusEffectDuration(target, Effect);
    }

    public override void RemoveSkill(IEntityManager entManager, EntityUid target)
    {
        var statusEffectSystem = entManager.System<StatusEffectsSystem>();
        statusEffectSystem.TryRemoveStatusEffect(target, Effect);
    }

    public override string? GetName(IEntityManager entManager, IPrototypeManager protoManager)
    {
        return !protoManager.TryIndex(Effect, out var indexedAction) ? string.Empty : indexedAction.Name;
    }

    public override string? GetDescription(IEntityManager entManager, IPrototypeManager protoManager, ProtoId<CESkillPrototype> skill)
    {
        return !protoManager.TryIndex(Effect, out var indexedAction) ? string.Empty : indexedAction.Description;
    }

    public override SpriteSpecifier? GetIcon(IEntityManager entManager, IPrototypeManager protoManager)
    {
        if (!protoManager.Resolve(Effect, out var effectProto))
            return null;

        var compFactory = entManager.ComponentFactory;

        if (!effectProto.TryGetComponent<StatusEffectAlertComponent>(out var effectAlertComp, compFactory))
            return null;

        if (!protoManager.Resolve(effectAlertComp.Alert, out var alertProto))
            return null;

        return alertProto.Icons.First();
    }
}
