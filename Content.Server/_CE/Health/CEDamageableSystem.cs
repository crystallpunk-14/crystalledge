using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Camera;
using Content.Shared._CE.Health;
using Content.Shared.Effects;

namespace Content.Server._CE.Health;

public sealed class CEDamageableSystem : CESharedDamageableSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;
    [Dependency] private readonly CEScreenshakeSystem _shake = default!;

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source)
    {
        // Exclude the source's session — they already see the effect from client prediction.
        var filter = source != null
            ? CEFilter.ZPvsExcept(source.Value, EntityManager)
            : CEFilter.ZPvs(target, EntityManager);

        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);

        var shakeTranslation = new CEScreenshakeParameters() { Trauma = 0.4f, DecayRate = 3f, Frequency = 0.008f };
        _shake.Screenshake(target, shakeTranslation, null);
    }
}
