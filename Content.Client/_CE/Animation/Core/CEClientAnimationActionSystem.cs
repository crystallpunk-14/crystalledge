using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Actions;
using Content.Shared._CE.Animation.Core.Components;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.Animation.Core;

public sealed partial class CEClientAnimationActionSystem : CESharedAnimationActionSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<CEEntityAnimationEvent>(OnEntityAnimation);
    }

    private void OnEntityAnimation(CEEntityAnimationEvent ev)
    {
        var entity = GetEntity(ev.Entity);
        var used = GetEntity(ev.Used);

        // Entity might not exist due to PVS
        if (!Exists(entity))
            return;

        if (!TryComp<CEActiveAnimationActionComponent>(entity, out var comp))
            return;

        if (!_proto.Resolve(comp.ActiveAnimation, out var animation))
            return;

        // Find and execute all ItemVisualEffect actions for the specific frame
        if (!animation.Events.TryGetValue(ev.Frame, out var actions))
            return;

        foreach (var action in actions)
        {
            if (action is SharedEntityAnimation visualEffect)
            {
                visualEffect.Play(EntityManager, entity, used, ev.Angle, comp.AnimationSpeed, ev.Frame, comp.TargetEntity, comp.TargetCoordinates);
            }
        }
    }
}
