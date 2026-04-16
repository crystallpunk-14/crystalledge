using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.StatusEffectVFX;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.StatusEffectVFX;

public sealed class CEStatusEffectVFXSystem : CESharedStatusEffectVFXSystem
{
    protected override void PlayEffect(EntityUid target, EntityUid? source, EntProtoId? vfx, EntityCoordinates pos)
    {
        if (vfx is null)
            return;

        var filter = source != null
            ? CEFilter.ZPvsExcept(source.Value, EntityManager)
            : CEFilter.ZPvs(target, EntityManager);

        RaiseNetworkEvent(
            new CEStatusEffectVFXEvent(GetNetCoordinates(pos), vfx),
            filter);
    }
}
