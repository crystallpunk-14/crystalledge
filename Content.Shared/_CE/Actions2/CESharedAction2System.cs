using Content.Shared._CE.Animation.Core;
using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared.Actions;
using Content.Shared.Actions.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Popups;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Actions2;

public abstract partial class CESharedAction2System : EntitySystem
{
    [Dependency] protected readonly SharedPopupSystem Popup = default!;
    [Dependency] private readonly CESharedAnimationActionSystem _animation = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedHandsSystem _hand = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private EntityQuery<ActionComponent> _actionQuery;

    public override void Initialize()
    {
        base.Initialize();

        _actionQuery = GetEntityQuery<ActionComponent>();

        InitializeAttempts();
        InitializeExamine();

        SubscribeLocalEvent<TransformComponent, CEInstantActionAnimationEvent>(OnInstantAction);
        SubscribeLocalEvent<TransformComponent, CEWorldTargetActionAnimationEvent>(OnWorldTargetAction);
        SubscribeLocalEvent<TransformComponent, CEAngleActionAnimationEvent>(OnAngleTargetAction);
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

    private void OnAngleTargetAction(Entity<TransformComponent> ent, ref CEAngleActionAnimationEvent args)
    {
        if (args.Handled)
            return;

        var playerPos = _transform.GetMapCoordinates(ent).Position;
        var targetPos = _transform.ToMapCoordinates(args.Target).Position;
        var direction = targetPos - playerPos;
        var angle = Angle.FromWorldVec(direction);

        _animation.TryPlayAnimationToAngle(ent, args.Animation, angle, args.Action.Comp.Container, args.Speed, args.CancelAnimation);
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

public sealed partial class CEAngleActionAnimationEvent : WorldTargetActionEvent
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
