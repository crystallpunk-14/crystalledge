using Content.Shared._CE.EntityEffect.Effects;
using Robust.Client.Animations;
using Robust.Client.GameObjects;
using Robust.Shared.Animations;

namespace Content.Client._CE.EntityEffect.Effects;

/// <summary>
/// Shared static helpers for building Robust animation tracks from CE keyframe lists.
/// Used by both <see cref="CEEntityAnimationEffectSystem"/> and <see cref="CEUserAnimationEffectSystem"/>.
/// </summary>
internal static class CEAnimationTrackBuilders
{
    /// <summary>
    /// Builds an offset animation track.
    /// </summary>
    /// <param name="keyframes">Keyframe list from the effect data.</param>
    /// <param name="speedMult">Inverse of playback speed (1 / speed).</param>
    /// <param name="rotateBy">Optional world angle to rotate each offset vector by (used for weapon swing effects).</param>
    public static Robust.Client.Animations.Animation BuildOffsetAnimation(
        List<CEOffsetKeyframe> keyframes,
        float speedMult,
        Angle rotateBy = default)
    {
        var duration = CalculateDuration(keyframes, speedMult);
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
                },
            },
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];
        var prevTime = 0f;

        foreach (var kf in keyframes)
        {
            var offset = rotateBy != default ? rotateBy.RotateVec(kf.Offset) : kf.Offset;
            var delta = (kf.Time - prevTime) * speedMult;
            prevTime = kf.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(offset, delta, GetEasing(kf.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds a rotation animation track.
    /// </summary>
    /// <param name="keyframes">Keyframe list from the effect data.</param>
    /// <param name="speedMult">Inverse of playback speed.</param>
    /// <param name="baseAngle">Base angle added to every keyframe rotation (world orientation).</param>
    public static Robust.Client.Animations.Animation BuildRotationAnimation(
        List<CERotationKeyframe> keyframes,
        float speedMult,
        Angle baseAngle = default)
    {
        var duration = CalculateDuration(keyframes, speedMult);
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
                },
            },
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];
        var prevTime = 0f;

        foreach (var kf in keyframes)
        {
            var rotation = baseAngle + Angle.FromDegrees(kf.Rotation);
            var delta = (kf.Time - prevTime) * speedMult;
            prevTime = kf.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(rotation, delta, GetEasing(kf.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds a color / alpha animation track.
    /// </summary>
    public static Robust.Client.Animations.Animation BuildColorAnimation(List<CEColorKeyframe> keyframes, float speedMult)
    {
        var duration = CalculateDuration(keyframes, speedMult);
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
                },
            },
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];
        var prevTime = 0f;

        foreach (var kf in keyframes)
        {
            var delta = (kf.Time - prevTime) * speedMult;
            prevTime = kf.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(kf.Color, delta, GetEasing(kf.Easing)));
        }

        return animation;
    }

    /// <summary>
    /// Builds a scale animation track.
    /// </summary>
    public static Robust.Client.Animations.Animation BuildScaleAnimation(List<CEScaleKeyFrame> keyframes, float speedMult)
    {
        var duration = CalculateDuration(keyframes, speedMult);
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
                },
            },
        };

        var track = (AnimationTrackComponentProperty)animation.AnimationTracks[0];
        var prevTime = 0f;

        foreach (var kf in keyframes)
        {
            var delta = (kf.Time - prevTime) * speedMult;
            prevTime = kf.Time;
            track.KeyFrames.Add(new AnimationTrackProperty.KeyFrame(kf.Scale, delta, GetEasing(kf.Easing)));
        }

        return animation;
    }

    // ── Duration ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the total scaled duration for an <see cref="EntityAnimation"/> effect
    /// (maximum last-keyframe time across all channels).
    /// </summary>
    public static float CalculateDuration(EntityAnimation effect, float speedMult)
    {
        var max = 0f;

        if (effect.OffsetAnimation.Count > 0)
            max = Math.Max(max, effect.OffsetAnimation[^1].Time);

        if (effect.RotationAnimation.Count > 0)
            max = Math.Max(max, effect.RotationAnimation[^1].Time);

        if (effect.ColorAnimation.Count > 0)
            max = Math.Max(max, effect.ColorAnimation[^1].Time);

        if (effect.ScaleAnimation.Count > 0)
            max = Math.Max(max, effect.ScaleAnimation[^1].Time);

        return (max > 0f ? max + 0.5f : 0.5f) * speedMult;
    }

    /// <summary>
    /// Returns the total scaled duration for a <see cref="UserAnimation"/> effect.
    /// </summary>
    public static float CalculateDuration(UserAnimation effect, float speedMult)
    {
        var max = 0f;

        if (effect.OffsetAnimation.Count > 0)
            max = Math.Max(max, effect.OffsetAnimation[^1].Time);

        if (effect.RotationAnimation.Count > 0)
            max = Math.Max(max, effect.RotationAnimation[^1].Time);

        if (effect.ColorAnimation.Count > 0)
            max = Math.Max(max, effect.ColorAnimation[^1].Time);

        if (effect.ScaleAnimation.Count > 0)
            max = Math.Max(max, effect.ScaleAnimation[^1].Time);

        return (max > 0f ? max + 0.1f : 0.1f) * speedMult;
    }

    // ── Per-channel duration overloads ────────────────────────────────────────

    public static float CalculateDuration(List<CEOffsetKeyframe> kfs, float speedMult) =>
        (kfs.Count > 0 ? kfs[^1].Time + 0.1f : 0.1f) * speedMult;

    public static float CalculateDuration(List<CERotationKeyframe> kfs, float speedMult) =>
        (kfs.Count > 0 ? kfs[^1].Time + 0.1f : 0.1f) * speedMult;

    public static float CalculateDuration(List<CEColorKeyframe> kfs, float speedMult) =>
        (kfs.Count > 0 ? kfs[^1].Time + 0.1f : 0.1f) * speedMult;

    public static float CalculateDuration(List<CEScaleKeyFrame> kfs, float speedMult) =>
        (kfs.Count > 0 ? kfs[^1].Time + 0.1f : 0.1f) * speedMult;

    // ── Easing ───────────────────────────────────────────────────────────────

    public static Func<float, float> GetEasing(CEAnimationEasing easing)
    {
        return easing switch
        {
            CEAnimationEasing.QuadIn => Easings.InQuad,
            CEAnimationEasing.QuadOut => Easings.OutQuad,
            CEAnimationEasing.QuadInOut => Easings.InOutQuad,
            CEAnimationEasing.CubicIn => Easings.InCubic,
            CEAnimationEasing.CubicOut => Easings.OutCubic,
            CEAnimationEasing.CubicInOut => Easings.InOutCubic,
            CEAnimationEasing.QuartIn => Easings.InQuart,
            CEAnimationEasing.QuartOut => Easings.OutQuart,
            CEAnimationEasing.QuartInOut => Easings.InOutQuart,
            _ => p => p,
        };
    }
}
