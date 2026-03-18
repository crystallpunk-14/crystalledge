using Content.Server._CE.ZLevels.Core;
using Content.Shared._CE.Health;
using Content.Shared.Effects;
using Robust.Shared.Player;

namespace Content.Server._CE.Health;

public sealed class CEDamageableSystem : CESharedDamageableSystem
{
    [Dependency] private readonly SharedColorFlashEffectSystem _color = default!;

    protected override void RaiseDamageEffect(EntityUid target, EntityUid? source)
    {
        var filter = source != null
            ? CEFilter.ZPvsExcept(source.Value, EntityManager)
            : CEFilter.ZPvs(target, EntityManager);

        _color.RaiseEffect(Color.Red, new List<EntityUid> { target }, filter);
    }
}
