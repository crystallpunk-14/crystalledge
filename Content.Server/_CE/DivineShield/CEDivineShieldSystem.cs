using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.DivineShield;
using Robust.Shared.Map;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.DivineShield;

public sealed class CEDivineShieldSystem : CESharedDivineShieldSystem
{
    protected override void RaiseBreakEffect(EntityUid? ent, EntProtoId? breakVfx, EntityUid? source)
    {
        if (breakVfx is null || ent is null)
            return;

        var pos = Transform(ent.Value).Coordinates;
        var filter = source != null
            ? CEFilter.ZPvsExcept(source.Value, EntityManager)
            : CEFilter.ZPvs(ent.Value, EntityManager);

        RaiseNetworkEvent(
            new CEDivineShieldBreakEffectEvent(GetNetCoordinates(pos), breakVfx),
            filter);
    }
}

