using Content.Shared._CE.StatusEffectVFX;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.StatusEffectVFX;

public sealed class CEStatusEffectVFXSystem : CESharedStatusEffectVFXSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<CEStatusEffectVFXEvent>(OnVFXEvent);
    }

    protected override void PlayEffect(EntityUid target, EntityUid? source, EntProtoId? vfx, EntityCoordinates pos)
    {
        if (!_timing.IsFirstTimePredicted || vfx == null)
            return;

        SpawnAtPosition(vfx, pos);
    }

    private void OnVFXEvent(CEStatusEffectVFXEvent args)
    {
        if (args.Vfx == null)
            return;

        SpawnAtPosition(args.Vfx, GetCoordinates(args.Coordinates));
    }
}
