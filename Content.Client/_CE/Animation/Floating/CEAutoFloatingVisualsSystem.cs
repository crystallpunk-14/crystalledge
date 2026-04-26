using Content.Shared._CE.Animation.Floating;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._CE.Animation.Floating;

/// <summary>
/// Client-side system that plays the looped floating animation defined by
/// <see cref="CEAutoFloatingVisualsComponent"/>.
/// </summary>
public sealed class CEAutoFloatingVisualsSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEAutoFloatingVisualsComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEAutoFloatingVisualsComponent, AnimationCompletedEvent>(OnAnimationComplete);
    }

    private void OnStartup(Entity<CEAutoFloatingVisualsComponent> ent, ref ComponentStartup args)
    {
        Play(ent);
    }

    private void OnAnimationComplete(Entity<CEAutoFloatingVisualsComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != ent.Comp.AnimationKey)
            return;
        Play(ent);
    }

    private void Play(Entity<CEAutoFloatingVisualsComponent> ent)
    {
        if (!HasComp<SpriteComponent>(ent))
            return;

        if (_animation.HasRunningAnimation(ent, ent.Comp.AnimationKey))
            return;

        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(ent.Comp.AnimationTime * 2),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Cubic,
                    KeyFrames =
                    {
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingStartOffset, 0f),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingOffset, ent.Comp.AnimationTime),
                        new AnimationTrackProperty.KeyFrame(ent.Comp.FloatingStartOffset, ent.Comp.AnimationTime),
                    },
                },
            },
        };

        _animation.Play(ent, animation, ent.Comp.AnimationKey);
    }
}
