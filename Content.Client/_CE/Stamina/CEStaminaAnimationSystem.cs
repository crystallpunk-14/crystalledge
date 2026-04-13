using System.Numerics;
using Content.Client.Stunnable;
using Content.Shared._CE.Stamina;
using Robust.Client.GameObjects;

namespace Content.Client._CE.Stamina;

/// <summary>
/// Client-side system that plays a fatigue (breathing + jitter) sprite animation
/// whose intensity scales with how low the entity's CE stamina is.
/// Mirrors the vanilla StaminaSystem's animation approach.
/// </summary>
public sealed class CEStaminaAnimationSystem : EntitySystem
{
    [Dependency] private readonly AnimationPlayerSystem _animation = default!;
    [Dependency] private readonly CEStaminaSystem _stamina = default!;
    [Dependency] private readonly StunSystem _stun = default!;

    private const string AnimationKey = "ce-stamina";

    /// <summary>
    /// Per-entity client-side animation state (sprite start offset, last jitter quadrant).
    /// </summary>
    private readonly Dictionary<EntityUid, AnimState> _states = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEStaminaComponent, AnimationCompletedEvent>(OnAnimationCompleted);
        SubscribeLocalEvent<CEStaminaComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEStaminaComponent, AfterAutoHandleStateEvent>(OnHandleState);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var query = EntityQueryEnumerator<CEStaminaComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var comp, out var sprite))
        {
            TryStartAnimation(uid, comp, sprite);
        }
    }

    private void OnHandleState(Entity<CEStaminaComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (TryComp<SpriteComponent>(ent, out var sprite))
            TryStartAnimation(ent, ent.Comp, sprite);
    }

    private void OnShutdown(Entity<CEStaminaComponent> ent, ref ComponentShutdown args)
    {
        StopAnimation(ent);
    }

    private void OnAnimationCompleted(Entity<CEStaminaComponent> ent, ref AnimationCompletedEvent args)
    {
        if (args.Key != AnimationKey || !args.Finished)
            return;

        if (!TryComp<SpriteComponent>(ent, out var sprite))
            return;

        var ratio = _stamina.GetStamina((ent, ent.Comp)) / ent.Comp.MaxStamina;

        if (ratio >= ent.Comp.AnimationThreshold)
        {
            StopAnimation(ent, sprite);
            return;
        }

        if (!HasComp<AnimationPlayerComponent>(ent))
            return;

        PlayAnimation(ent, ent.Comp, sprite);
    }

    private void TryStartAnimation(EntityUid uid, CEStaminaComponent comp, SpriteComponent sprite)
    {
        if (_animation.HasRunningAnimation(uid, AnimationKey))
            return;

        var ratio = _stamina.GetStamina(uid) / comp.MaxStamina;

        // Don't animate if above the threshold.
        if (ratio >= comp.AnimationThreshold)
            return;

        if (!_states.ContainsKey(uid))
            _states[uid] = new AnimState { StartOffset = sprite.Offset };

        PlayAnimation(uid, comp, sprite);
    }

    private void PlayAnimation(EntityUid uid, CEStaminaComponent comp, SpriteComponent sprite)
    {
        var ratio = _stamina.GetStamina(uid) / comp.MaxStamina;

        // step: 0 at AnimationThreshold, 1 at 0 stamina
        var step = Math.Clamp(1f - ratio / comp.AnimationThreshold, 0f, 1f);

        var frequency = comp.FrequencyMin + step * comp.FrequencyMod;
        var jitter = comp.JitterAmplitudeMin + step * comp.JitterAmplitudeMod;
        var breathing = comp.BreathingAmplitudeMin + step * comp.BreathingAmplitudeMod;

        if (!_states.TryGetValue(uid, out var state))
        {
            state = new AnimState { StartOffset = sprite.Offset };
            _states[uid] = state;
        }

        _animation.Play(uid,
            _stun.GetFatigueAnimation(
                sprite,
                frequency,
                comp.Jitters,
                jitter * comp.JitterMin,
                jitter * comp.JitterMax,
                breathing,
                state.StartOffset,
                ref state.LastJitter),
            AnimationKey);
    }

    private void StopAnimation(EntityUid uid, SpriteComponent? sprite = null)
    {
        _animation.Stop(uid, AnimationKey);

        if (_states.TryGetValue(uid, out var state))
        {
            if (sprite != null)
                sprite.Offset = state.StartOffset;

            _states.Remove(uid);
        }
    }

    private sealed class AnimState
    {
        public Vector2 StartOffset;
        public Vector2 LastJitter;
    }
}
