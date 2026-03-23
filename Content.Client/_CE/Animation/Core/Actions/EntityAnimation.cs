using System.Linq;
using System.Numerics;
using Content.Client.Animations;
using Content.Shared._CE.Animation.Core.Actions;
using Content.Shared._CE.Animation.Item.Components;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.IoC;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._CE.Animation.Core.Actions;

public sealed partial class EntityAnimation : SharedEntityAnimation
{
    private const string OffsetAnimationKey = "ce-item-visual-offset";
    private const string RotationAnimationKey = "ce-item-visual-rotation";
    private const string ColorAnimationKey = "ce-item-visual-color";
    private const string ScaleAnimationKey = "ce-item-visual-scale";

    private float _animationSpeedMultiplier = 1f;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        var timing = IoCManager.Resolve<IGameTiming>();
        if (!timing.IsFirstTimePredicted)
            return;

        var transform = entManager.System<TransformSystem>();
        var spriteSystem = entManager.System<SpriteSystem>();
        var animationPlayer = entManager.System<AnimationPlayerSystem>();

        _animationSpeedMultiplier = 1f / speed;

        if (!entManager.TryGetComponent<TransformComponent>(user, out var userXform)
            || userXform.MapID == MapId.Nullspace)
            return;

        // Spawn a client-side clone entity at the user's position
        var effectEntity = entManager.SpawnEntity("clientsideclone", userXform.Coordinates);

        if (!entManager.TryGetComponent<SpriteComponent>(effectEntity, out var effectSprite))
            return;

        // Set up the sprite: either override or copy from the used item
        if (DummyEntity != null)
        {
            var dummy = entManager.Spawn(DummyEntity);
            if (entManager.TryGetComponent<SpriteComponent>(dummy, out var dummySprite))
                spriteSystem.CopySprite((dummy, dummySprite), (effectEntity, effectSprite));
            entManager.DeleteEntity(dummy);

            var proto = IoCManager.Resolve<IPrototypeManager>().Index(DummyEntity.Value);
            entManager.AddComponents(effectEntity, proto, false);
        }
        else
        {
            if (used is not null && entManager.TryGetComponent<SpriteComponent>(used.Value, out var itemSprite))
                spriteSystem.CopySprite((used.Value, itemSprite), (effectEntity, effectSprite));
        }

        spriteSystem.SetVisible((effectEntity, effectSprite), true);

        // Set initial rotation
        var initialRotation = angle;
        if (entManager.TryGetComponent<CEWeaponComponent>(used, out var itemAnim))
            initialRotation += Angle.FromDegrees(itemAnim.SpriteRotation);

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
            track.User = user;
        }
        else
        {
            // Position at the offset from the user if not following
            var worldPos = transform.GetWorldPosition(userXform) + angle.RotateVec(initialOffset);
            transform.SetWorldPosition(effectEntity, worldPos);
        }

        // Set up timed despawn
        var despawn = entManager.EnsureComponent<TimedDespawnComponent>(effectEntity);
        despawn.Lifetime = CalculateDuration() + 0.1f;

        // Build and play offset animation if keyframes exist
        if (OffsetAnimation.Count > 0)
        {
            var offsetAnim = BuildOffsetAnimation(angle);
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
            var colorAnim = BuildColorAnimation();
            animationPlayer.Play(effectEntity, colorAnim, ColorAnimationKey);
        }

        // Build and play scale animation if keyframes exist
        if (ScaleAnimation.Count > 0)
        {
            var scaleAnim = BuildScaleAnimation();
            animationPlayer.Play(effectEntity, scaleAnim, ScaleAnimationKey);
        }
    }

    /// <summary>
    /// Builds an animation for sprite offset from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildOffsetAnimation(Angle angle)
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(CalculateDuration()),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Offset),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { },
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        var prevTime = 0f;
        foreach (var keyframe in OffsetAnimation)
        {
            // Calculate relative offset from the base position
            var relativeOffset = keyframe.Offset;

            // Rotate the relative offset by the animation angle
            var rotatedOffset = angle.RotateVec(relativeOffset);

            var deltaTime = (keyframe.Time - prevTime) * _animationSpeedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(rotatedOffset, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds an animation for sprite rotation from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildRotationAnimation(Angle angle)
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(CalculateDuration()),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Rotation),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { },
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        var prevTime = 0f;
        foreach (var keyframe in RotationAnimation)
        {
            // Add keyframe rotation to base rotation
            var totalRotation = angle + Angle.FromDegrees(keyframe.Rotation);
            var deltaTime = (keyframe.Time - prevTime) * _animationSpeedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(totalRotation, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds an animation for sprite color/alpha from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildColorAnimation()
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(CalculateDuration()),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Color),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { },
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        var prevTime = 0f;
        foreach (var keyframe in ColorAnimation)
        {
            var deltaTime = (keyframe.Time - prevTime) * _animationSpeedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(keyframe.Color, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds an animation for sprite scale from keyframes.
    /// </summary>
    private Robust.Client.Animations.Animation BuildScaleAnimation()
    {
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(CalculateDuration()),
            AnimationTracks =
            {
                new AnimationTrackComponentProperty
                {
                    ComponentType = typeof(SpriteComponent),
                    Property = nameof(SpriteComponent.Scale),
                    InterpolationMode = AnimationInterpolationMode.Linear,
                    KeyFrames = { },
                }
            }
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];

        var prevTime = 0f;
        foreach (var keyframe in ScaleAnimation)
        {
            var deltaTime = (keyframe.Time - prevTime) * _animationSpeedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(keyframe.Scale, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Calculates the adaptive duration by finding the maximum time from all keyframe animations.
    /// If no keyframes are present, returns a default duration of 0.5 seconds.
    /// </summary>
    private float CalculateDuration()
    {
        var maxTime = 0f;

        // Check offset animation keyframes
        if (OffsetAnimation.Count > 0)
        {
            var maxOffsetTime = OffsetAnimation.Max(k => k.Time);
            maxTime = Math.Max(maxTime, maxOffsetTime);
        }

        // Check rotation animation keyframes
        if (RotationAnimation.Count > 0)
        {
            var maxRotationTime = RotationAnimation.Max(k => k.Time);
            maxTime = Math.Max(maxTime, maxRotationTime);
        }

        // Check color animation keyframes
        if (ColorAnimation.Count > 0)
        {
            var maxColorTime = ColorAnimation.Max(k => k.Time);
            maxTime = Math.Max(maxTime, maxColorTime);
        }

        maxTime *= _animationSpeedMultiplier;

        // If no keyframes found, use default duration
        return maxTime > 0f ? maxTime + 0.5f : 0.5f;
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
