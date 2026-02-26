using System.Numerics;
using Content.Client.Animations;
using Content.Shared._CE.Animation.Core.Actions;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._CE.Animation.Core.Actions;

public sealed partial class ItemVisualEffect : SharedItemVisualEffect
{
    private const string OffsetAnimationKey = "ce-item-visual-offset";
    private const string RotationAnimationKey = "ce-item-visual-rotation";
    private const string ColorAnimationKey = "ce-item-visual-color";
    private const string FlickAnimationKey = "ce-item-visual-flick";

    public override void Play(EntityManager entManager, EntityUid entity, EntityUid? used, Angle angle, TimeSpan frame)
    {
        var timing = IoCManager.Resolve<IGameTiming>();
        if (!timing.IsFirstTimePredicted)
            return;

        var transform = entManager.System<TransformSystem>();
        var spriteSystem = entManager.System<SpriteSystem>();
        var animationPlayer = entManager.System<AnimationPlayerSystem>();

        if (!entManager.TryGetComponent<TransformComponent>(entity, out var userXform)
            || userXform.MapID == MapId.Nullspace)
            return;

        // Spawn a client-side clone entity at the user's position
        var effectEntity = entManager.SpawnEntity("clientsideclone", userXform.Coordinates);

        if (!entManager.TryGetComponent<SpriteComponent>(effectEntity, out var effectSprite))
            return;

        // Set up the sprite: either override or copy from the used item
        if (SpriteOverride != null)
        {
            spriteSystem.LayerSetSprite((effectEntity, effectSprite), 0, SpriteOverride);
        }
        else if (used.HasValue && entManager.TryGetComponent<SpriteComponent>(used.Value, out var itemSprite))
        {
            spriteSystem.CopySprite((used.Value, itemSprite), (effectEntity, effectSprite));
        }

        spriteSystem.SetVisible((effectEntity, effectSprite), true);

        // Apply RSI override if specified
        if (AnimationRsi != null)
        {
            spriteSystem.LayerSetRsi((effectEntity, effectSprite), 0, AnimationRsi.Value);
        }

        // Set initial rotation
        var initialRotation = angle + Angle.FromDegrees(SpriteRotation);
        spriteSystem.SetRotation((effectEntity, effectSprite), initialRotation);

        // Get initial offset from first keyframe or use zero
        var initialOffset = Vector2.Zero;
        if (OffsetAnimation.Count > 0)
        {
            var firstKeyframe = OffsetAnimation[0];
            if (firstKeyframe.Time == 0)
                initialOffset = firstKeyframe.Offset;
        }

        // Set up to follow the user if enabled
        if (FollowUser)
        {
            var track = entManager.EnsureComponent<TrackUserComponent>(effectEntity);
            track.User = entity;
            // Use initial offset from animation keyframes
            track.Offset = angle.RotateVec(initialOffset);
        }
        else
        {
            // Position at the offset from the user if not following
            var worldPos = transform.GetWorldPosition(userXform) + angle.RotateVec(initialOffset);
            transform.SetWorldPosition(effectEntity, worldPos);
        }

        // Set up timed despawn
        var despawn = entManager.EnsureComponent<TimedDespawnComponent>(effectEntity);
        despawn.Lifetime = Duration + 0.1f;

        // Build and play offset animation if keyframes exist
        if (OffsetAnimation.Count > 0)
        {
            var offsetAnim = BuildOffsetAnimation(angle, initialRotation);
            animationPlayer.Play(effectEntity, offsetAnim, OffsetAnimationKey);
        }

        // Build and play rotation animation if keyframes exist
        if (RotationAnimation.Count > 0)
        {
            var rotationAnim = BuildRotationAnimation(initialRotation);
            animationPlayer.Play(effectEntity, rotationAnim, RotationAnimationKey);
        }

        // Build and play color animation if keyframes exist
        if (ColorAnimation.Count > 0)
        {
            var colorAnim = BuildColorAnimation(effectSprite);
            animationPlayer.Play(effectEntity, colorAnim, ColorAnimationKey);
        }

        // Play flick animation if a state is specified
        if (AnimationState != null)
        {
            var flickAnimation = new Robust.Client.Animations.Animation()
            {
                Length = TimeSpan.FromSeconds(Duration),
                AnimationTracks =
                {
                    new AnimationTrackSpriteFlick
                    {
                        LayerKey = 0,
                        KeyFrames =
                        {
                            new AnimationTrackSpriteFlick.KeyFrame(AnimationState, 0f),
                        },
                    },
                },
            };

            animationPlayer.Play(effectEntity, flickAnimation, FlickAnimationKey);
        }
    }

    /// <summary>
    /// Builds an animation for sprite offset from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildOffsetAnimation(Angle angle, Angle baseRotation)
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(Duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { }
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        foreach (var keyframe in OffsetAnimation)
        {
            // Rotate the offset by the animation angle so it's relative to attack direction
            var rotatedOffset = angle.RotateVec(keyframe.Offset);

            var easingFunc = GetEasingFunction(keyframe.Easing);
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(rotatedOffset, keyframe.Time, easingFunc));
        }

        return animation;
    }

    /// <summary>
    /// Builds an animation for sprite rotation from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildRotationAnimation(Angle baseRotation)
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(Duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { }
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        foreach (var keyframe in RotationAnimation)
        {
            // Add keyframe rotation to base rotation
            var totalRotation = baseRotation + Angle.FromDegrees(keyframe.Rotation);
            var easingFunc = GetEasingFunction(keyframe.Easing);
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(totalRotation, keyframe.Time, easingFunc));
        }

        return animation;
    }

    /// <summary>
    /// Builds an animation for sprite color/alpha from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildColorAnimation(SpriteComponent sprite)
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(Duration),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { }
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        foreach (var keyframe in ColorAnimation)
        {
            var easingFunc = GetEasingFunction(keyframe.Easing);
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(keyframe.Color, keyframe.Time, easingFunc));
        }

        return animation;
    }

    /// <summary>
    /// Helper method to get rotation at a specific time point.
    /// </summary>
    private Angle GetRotationAtTime(float time, Angle baseRotation)
    {
        if (RotationAnimation.Count == 0)
            return baseRotation;

        // Find the keyframes around this time
        CERotationKeyframe? before = null;
        CERotationKeyframe? after = null;

        foreach (var keyframe in RotationAnimation)
        {
            if (keyframe.Time <= time)
                before = keyframe;
            else if (after == null)
            {
                after = keyframe;
                break;
            }
        }

        if (before == null)
            return baseRotation;
        if (after == null)
            return baseRotation + Angle.FromDegrees(before.Rotation);

        // Interpolate between keyframes
        var t = (time - before.Time) / (after.Time - before.Time);
        var interpolatedRotation = MathHelper.Lerp(before.Rotation, after.Rotation, t);
        return baseRotation + Angle.FromDegrees(interpolatedRotation);
    }

    /// <summary>
    /// Converts CEAnimationEasing enum to actual easing function.
    /// </summary>
    private static Func<float, float> GetEasingFunction(CEAnimationEasing easing)
    {
        return easing switch
        {
            CEAnimationEasing.Linear => (p) => p, // Identity function for linear interpolation
            CEAnimationEasing.QuadIn => Easings.InQuad,
            CEAnimationEasing.QuadOut => Easings.OutQuad,
            CEAnimationEasing.QuadInOut => Easings.InOutQuad,
            CEAnimationEasing.CubicIn => Easings.InCubic,
            CEAnimationEasing.CubicOut => Easings.OutCubic,
            CEAnimationEasing.CubicInOut => Easings.InOutCubic,
            CEAnimationEasing.QuartIn => Easings.InQuart,
            CEAnimationEasing.QuartOut => Easings.OutQuart,
            CEAnimationEasing.QuartInOut => Easings.InOutQuart,
            _ => (p) => p // Default to linear
        };
    }
}
