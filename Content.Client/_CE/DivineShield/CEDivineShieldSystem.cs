using Content.Shared._CE.DivineShield;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.DivineShield;

public sealed class CEDivineShieldSystem : CESharedDivineShieldSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<CEDivineShieldBreakEffectEvent>(OnBreakEffectEvent);
    }

    protected override void RaiseBreakEffect(EntityUid? ent, EntProtoId? breakVfx, EntityUid? source)
    {
        if (!_timing.IsFirstTimePredicted || breakVfx == null || ent is null)
            return;

        SpawnAtPosition(breakVfx, Transform(ent.Value).Coordinates);
    }

    private void OnBreakEffectEvent(CEDivineShieldBreakEffectEvent args)
    {
        if (args.BreakVfx == null)
            return;

        var pos = GetCoordinates(args.Coordinates);
        SpawnAtPosition(args.BreakVfx, pos);
    }
}
