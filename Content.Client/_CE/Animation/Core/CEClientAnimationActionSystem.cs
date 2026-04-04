using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Robust.Client.Player;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Animation.Core;

public sealed partial class CEClientAnimationActionSystem : CESharedAnimationActionSystem
{
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CEEntityAnimationEvent>(OnEntityAnimation);
    }

    private void OnEntityAnimation(CEEntityAnimationEvent ev)
    {
        var entity = GetEntity(ev.Entity);
        var used = ev.Used.HasValue ? GetEntity(ev.Used.Value) : (EntityUid?) null;

        // Entity might not exist due to PVS
        if (!Exists(entity))
            return;

        if (!_proto.TryIndex(ev.AnimationId, out var animation))
            return;

        // Find and execute all EntityAnimation actions for the specific frame
        var speedMultiplier = 1f / ev.Speed;
        var realKeyFrame = ev.Frame * speedMultiplier;

        if (!animation.Events.TryGetValue(ev.Frame, out var actions))
            return;

        var targetEntity = ev.TargetEntity.HasValue ? GetEntity(ev.TargetEntity.Value) : (EntityUid?) null;
        var targetCoordinates = ev.TargetCoordinates.HasValue ? GetCoordinates(ev.TargetCoordinates.Value) : (EntityCoordinates?) null;

        foreach (var action in actions)
        {
            if (action is EntityAnimation)
            {
                var args = new CEEntityEffectArgs(
                    EntityManager,
                    entity,
                    used,
                    ev.Angle,
                    ev.Speed,
                    targetEntity,
                    targetCoordinates);
                action.Effect(args);
            }
        }
    }

    protected override bool IsLocallyPredicted(EntityUid uid)
    {
        return _player.LocalEntity == uid;
    }
}
