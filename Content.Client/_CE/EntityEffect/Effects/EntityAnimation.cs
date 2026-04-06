using System.Linq;
using System.Numerics;
using Content.Client.Animations;
using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.Hands.EntitySystems;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Spawners;
using Robust.Shared.Timing;

namespace Content.Client._CE.EntityEffect.Effects;

public sealed partial class CEEntityAnimationEffectSystem : CEEntityEffectSystem<EntityAnimation>
{
    private const string OffsetAnimationKey = "ce-item-visual-offset";
    private const string RotationAnimationKey = "ce-item-visual-rotation";
    private const string ColorAnimationKey = "ce-item-visual-color";
    private const string ScaleAnimationKey = "ce-item-visual-scale";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly AnimationPlayerSystem _animationPlayer = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;

    protected override void Effect(ref CEEntityEffectEvent<EntityAnimation> args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } entity)
            return;

        var effect = args.Effect;
        var angle = args.Args.Angle;
        var speedMultiplier = 1f / args.Args.Speed;

        var entityXform = Transform(entity);

        if (entityXform.MapID == MapId.Nullspace)
            return;

        // Spawn a client-side clone entity at the entity's position
        var effectEntity = Spawn("clientsideclone", entityXform.Coordinates);

        if (!TryComp<SpriteComponent>(effectEntity, out var effectSprite))
            return;

        // Resolve sprite source (in priority order):
        // 1. DummyEntity — explicit VFX entity prototype, always wins
        // 2. ActiveHandEntityAnimation — resolve from user's active hand
        // 3. UsedEntityAnimation — use the action's container item (args.Used)
        EntityUid? spriteSource = null;
        if (effect.DummyEntity != null)
        {
            var dummy = Spawn(effect.DummyEntity);
            if (TryComp<SpriteComponent>(dummy, out var dummySprite))
                _sprite.CopySprite((dummy, dummySprite), (effectEntity, effectSprite));
            Del(dummy);

            var proto = _protoManager.Index(effect.DummyEntity.Value);
            EntityManager.AddComponents(effectEntity, proto, false);
        }
        else if (effect is ActiveHandEntityAnimation
                 && _hands.TryGetActiveItem(entity, out var activeItem)
                 && TryComp<SpriteComponent>(activeItem.Value, out var handSprite))
        {
            spriteSource = activeItem.Value;
            _sprite.CopySprite((activeItem.Value, handSprite), (effectEntity, effectSprite));
        }
        else if (args.Args.Used is { } used
                 && TryComp<SpriteComponent>(used, out var itemSprite))
        {
            spriteSource = used;
            _sprite.CopySprite((used, itemSprite), (effectEntity, effectSprite));
        }

        _sprite.SetVisible((effectEntity, effectSprite), true);

        // Set initial rotation
        var initialRotation = angle;
        if (TryComp<CEWeaponComponent>(spriteSource, out var itemAnim))
            initialRotation += Angle.FromDegrees(itemAnim.SpriteRotation);

        _sprite.SetRotation((effectEntity, effectSprite), initialRotation);

        // Get initial offset from first keyframe or use zero
        var initialOffset = Vector2.Zero;
        if (effect.OffsetAnimation.Count > 0)
        {
            var firstKeyframe = effect.OffsetAnimation[0];
            if (firstKeyframe.Time == 0)
                initialOffset = firstKeyframe.Offset;
        }

        // Set up to follow the entity if enabled
        if (effect.FollowUser)
        {
            var track = EnsureComp<TrackUserComponent>(effectEntity);
            track.User = entity;
        }
        else
        {
            // Position at the offset from the entity if not following
            var worldPos = _transform.GetWorldPosition(entityXform) + angle.RotateVec(initialOffset);
            _transform.SetWorldPosition(effectEntity, worldPos);
        }

        // Set up timed despawn
        var despawn = EnsureComp<TimedDespawnComponent>(effectEntity);
        despawn.Lifetime = CalculateDuration(effect, speedMultiplier) + 0.1f;

        // Build and play offset animation if keyframes exist
        if (effect.OffsetAnimation.Count > 0)
        {
            var offsetAnim = BuildOffsetAnimation(effect, angle, speedMultiplier);
            _animationPlayer.Play(effectEntity, offsetAnim, OffsetAnimationKey);
        }

        // Build and play rotation animation if keyframes exist
        if (effect.RotationAnimation.Count > 0)
        {
            var rotationAnim = BuildRotationAnimation(effect, initialRotation, speedMultiplier);
            _animationPlayer.Play(effectEntity, rotationAnim, RotationAnimationKey);
        }

        // Build and play color animation if keyframes exist
        if (effect.ColorAnimation.Count > 0)
        {
            var colorAnim = BuildColorAnimation(effect, speedMultiplier);
            _animationPlayer.Play(effectEntity, colorAnim, ColorAnimationKey);
        }

        // Build and play scale animation if keyframes exist
        if (effect.ScaleAnimation.Count > 0)
        {
            var scaleAnim = BuildScaleAnimation(effect, speedMultiplier);
            _animationPlayer.Play(effectEntity, scaleAnim, ScaleAnimationKey);
        }
    }

    private static Robust.Client.Animations.Animation BuildOffsetAnimation(
        EntityAnimation effect, Angle angle, float speedMultiplier)
    {
        var duration = CalculateDuration(effect, speedMultiplier);
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(duration),
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
        foreach (var keyframe in effect.OffsetAnimation)
        {
            var rotatedOffset = angle.RotateVec(keyframe.Offset);
            var deltaTime = (keyframe.Time - prevTime) * speedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(rotatedOffset, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    private static Robust.Client.Animations.Animation BuildRotationAnimation(
        EntityAnimation effect, Angle angle, float speedMultiplier)
    {
        var duration = CalculateDuration(effect, speedMultiplier);
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(duration),
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
        foreach (var keyframe in effect.RotationAnimation)
        {
            var totalRotation = angle + Angle.FromDegrees(keyframe.Rotation);
            var deltaTime = (keyframe.Time - prevTime) * speedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(totalRotation, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    private static Robust.Client.Animations.Animation BuildColorAnimation(
        EntityAnimation effect, float speedMultiplier)
    {
        var duration = CalculateDuration(effect, speedMultiplier);
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(duration),
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
        foreach (var keyframe in effect.ColorAnimation)
        {
            var deltaTime = (keyframe.Time - prevTime) * speedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(keyframe.Color, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    private static Robust.Client.Animations.Animation BuildScaleAnimation(
        EntityAnimation effect, float speedMultiplier)
    {
        var duration = CalculateDuration(effect, speedMultiplier);
        var animation = new Robust.Client.Animations.Animation
        {
            Length = TimeSpan.FromSeconds(duration),
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
        foreach (var keyframe in effect.ScaleAnimation)
        {
            var deltaTime = (keyframe.Time - prevTime) * speedMultiplier;
            prevTime = keyframe.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(keyframe.Scale, deltaTime, GetEasingFunction(keyframe.Easing)));
        }

        return animation;
    }

    private static float CalculateDuration(EntityAnimation effect, float speedMultiplier)
    {
        var maxTime = 0f;

        if (effect.OffsetAnimation.Count > 0)
            maxTime = Math.Max(maxTime, effect.OffsetAnimation.Max(k => k.Time));

        if (effect.RotationAnimation.Count > 0)
            maxTime = Math.Max(maxTime, effect.RotationAnimation.Max(k => k.Time));

        if (effect.ColorAnimation.Count > 0)
            maxTime = Math.Max(maxTime, effect.ColorAnimation.Max(k => k.Time));

        maxTime *= speedMultiplier;

        return maxTime > 0f ? maxTime + 0.5f : 0.5f;
    }

    private static Func<float, float> GetEasingFunction(CEAnimationEasing easing)
    {
        return easing switch
        {
            CEAnimationEasing.Linear => (p) => p,
            CEAnimationEasing.QuadIn => Easings.InQuad,
            CEAnimationEasing.QuadOut => Easings.OutQuad,
            CEAnimationEasing.QuadInOut => Easings.InOutQuad,
            CEAnimationEasing.CubicIn => Easings.InCubic,
            CEAnimationEasing.CubicOut => Easings.OutCubic,
            CEAnimationEasing.CubicInOut => Easings.InOutCubic,
            CEAnimationEasing.QuartIn => Easings.InQuart,
            CEAnimationEasing.QuartOut => Easings.OutQuart,
            CEAnimationEasing.QuartInOut => Easings.InOutQuart,
            _ => (p) => p
        };
    }
}
