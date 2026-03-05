using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Actions2;

public abstract partial class CESharedAction2System : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly CESharedAnimationActionSystem _animation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TransformComponent, CEInstantActionAnimationEvent>(OnInstantAction);
        SubscribeLocalEvent<TransformComponent, CEWorldTargetActionAnimationEvent>(OnWorldTargetAction);
        SubscribeLocalEvent<TransformComponent, CEEntityTargetActionAnimationEvent>(OnEntityTargetAction);
    }

    private void OnInstantAction(Entity<TransformComponent> ent, ref CEInstantActionAnimationEvent args)
    {
        if (args.Handled)
            return;

        _animation.TryPlayAnimationToAngle(ent, args.Animation, null, args.Action.Comp.Container, args.Speed, args.CancelAnimation);
        args.Handled = true;
    }

    private void OnWorldTargetAction(Entity<TransformComponent> ent, ref CEWorldTargetActionAnimationEvent args)
    {
        if (args.Handled)
            return;

        _animation.TryPlayAnimationToCoordinates(ent, args.Animation, args.Target, args.Action.Comp.Container, args.Speed, args.CancelAnimation);
        args.Handled = true;
    }

    private void OnEntityTargetAction(Entity<TransformComponent> ent, ref CEEntityTargetActionAnimationEvent args)
    {
        if (args.Handled)
            return;

        _animation.TryPlayAnimationToEntity(ent, args.Animation, args.Target, args.Action.Comp.Container, args.Speed, args.CancelAnimation);
        args.Handled = true;
    }
}


public sealed partial class CEInstantActionAnimationEvent : InstantActionEvent
{
    [DataField(required: true)]
    public ProtoId<CEAnimationActionPrototype> Animation;

    [DataField]
    public float Speed = 1f;

    [DataField]
    public bool CancelAnimation;
}

public sealed partial class CEWorldTargetActionAnimationEvent : WorldTargetActionEvent
{
    [DataField(required: true)]
    public ProtoId<CEAnimationActionPrototype> Animation;

    [DataField]
    public float Speed = 1f;

    [DataField]
    public bool CancelAnimation;
}


public sealed partial class CEEntityTargetActionAnimationEvent : EntityTargetActionEvent
{
    [DataField(required: true)]
    public ProtoId<CEAnimationActionPrototype> Animation;

    [DataField]
    public float Speed = 1f;

    [DataField]
    public bool CancelAnimation;
}
