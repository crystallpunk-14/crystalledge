using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Core.Actions;


public sealed partial class ApplyStatusEffect : CEAnimationActionEntry
{
    [DataField(required: true)]
    public EntProtoId StatusEffect;

    [DataField(required: true)]
    public TimeSpan Duration = TimeSpan.FromSeconds(1f);

    [DataField]
    public int Stack = 1;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        if (target is null)
            return;

        var effectSys = entManager.System<CEStatusEffectStackSystem>();
        effectSys.TryAddStack(target.Value, StatusEffect, Stack, Duration);
    }
}
