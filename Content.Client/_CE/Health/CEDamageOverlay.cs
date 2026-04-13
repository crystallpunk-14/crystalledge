using System.Numerics;
using Content.Shared._CE.Health.Components;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Client._CE.Health;

public sealed class CEDamageOverlay : Overlay
{
    private static readonly ProtoId<ShaderPrototype> CircleMaskShader = "GradientCircleMask";

    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;

    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private readonly ShaderInstance _painShader;

    /// <summary>
    /// Pain level 0..1 where 0 = full health, 1 = 0% health.
    /// Only active when not in crit. Starts at 50% health (0.5 maps to level 0).
    /// </summary>
    public float PainLevel;

    private float _oldPainLevel;

    /// <summary>
    /// Crit level 0..1 representing progress toward death while in Critical state.
    /// 0 = just entered crit, 1 = about to die.
    /// </summary>
    public float CritLevel;

    private float _oldCritLevel;

    /// <summary>
    /// Whether the entity is currently in critical state (CEMobState.Critical).
    /// </summary>
    public bool InCrit;

    public CEDamageOverlay()
    {
        IoCManager.InjectDependencies(this);
        _painShader = _prototypeManager.Index(CircleMaskShader).InstanceUnique();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        if (!_entityManager.TryGetComponent(_playerManager.LocalEntity, out EyeComponent? eyeComp))
            return;

        if (args.Viewport.Eye != eyeComp.Eye)
            return;

        var viewport = args.WorldAABB;
        var handle = args.WorldHandle;
        var distance = args.ViewportBounds.Width;

        var time = (float) _timing.RealTime.TotalSeconds;
        var lastFrameTime = (float) _timing.FrameTime.TotalSeconds;

        // Smooth lerp pain level.
        if (!MathHelper.CloseTo(_oldPainLevel, PainLevel, 0.001f))
        {
            var diff = PainLevel - _oldPainLevel;
            _oldPainLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldPainLevel = PainLevel;
        }

        // Smooth lerp crit level.
        if (!MathHelper.CloseTo(_oldCritLevel, CritLevel, 0.001f))
        {
            var diff = CritLevel - _oldCritLevel;
            _oldCritLevel += GetDiff(diff, lastFrameTime);
        }
        else
        {
            _oldCritLevel = CritLevel;
        }

        // === Mode 1: Red pulsing overlay (alive, below 50% health) ===
        var painToDraw = InCrit ? 1f : _oldPainLevel;

        if (painToDraw > 0f)
        {
            // Heartbeat-like pulse: two quick beats then a pause.
            // Uses a combination of sine waves to create a "lub-dub" rhythm.
            var heartRate = InCrit ? 0.5f : MathHelper.Lerp(0.3f, 0.6f, painToDraw);
            var t = time * heartRate;

            //Some pulsing
            var beat1 = MathF.Max(0f, MathF.Sin(t * MathF.Tau));
            var beat2 = MathF.Max(0f, MathF.Sin((t + 0.15f) * MathF.Tau));
            var pulse = MathF.Max(beat1, beat2 * 0.7f);

            var pulseStrength = MathHelper.Lerp(0.15f, 0.45f, painToDraw);
            var effectivePulse = pulse * pulseStrength;

            var outerMaxRadius = 2.0f * distance;
            var outerMinRadius = 0.7f * distance;
            var innerMaxRadius = 0.5f * distance;
            var innerMinRadius = 0.15f * distance;

            var outerRadius = outerMaxRadius - painToDraw * (outerMaxRadius - outerMinRadius);
            var innerRadius = innerMaxRadius - painToDraw * (innerMaxRadius - innerMinRadius);

            _painShader.SetParameter("time", effectivePulse);
            _painShader.SetParameter("color", new Vector3(1f, 0f, 0f));
            _painShader.SetParameter("darknessAlphaOuter", 0.85f);
            _painShader.SetParameter("outerCircleRadius", outerRadius);
            _painShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.2f * distance);
            _painShader.SetParameter("innerCircleRadius", innerRadius);
            _painShader.SetParameter("innerCircleMaxRadius", innerRadius + 0.02f * distance);
            handle.UseShader(_painShader);
            handle.DrawRect(viewport, Color.White);
        }

        // === Mode 2: Black narrowing overlay (critical state) ===
        if (InCrit && _oldCritLevel >= 0f)
        {
            // CritLevel goes from 0 (just entered crit) to 1 (about to die).
            // At 0% crit progress (just entered), outer radius is large (~50% screen visible).
            // At 100% crit progress (near death), vision narrows to ~1.5 tiles.

            // 1.5 tiles ≈ 1.5 * 32px in world units, but in viewport coords we use distance-relative.
            // distance is the viewport half-width, so 1.5 tiles / typical zoom ≈ 0.08 * distance.
            var minVisionRadius = 0.08f * distance; // ~1.5 tiles
            var maxVisionRadius = 0.5f * distance;  // ~50% of screen at crit entry

            // Outer circle: how far from center the black starts.
            var outerRadius = MathHelper.Lerp(maxVisionRadius, minVisionRadius, _oldCritLevel);
            // Inner circle: always slightly smaller than outer for smooth gradient edge.
            var innerRadius = outerRadius * 0.4f;

            _painShader.SetParameter("time", 0f);
            _painShader.SetParameter("color", new Vector3(0f, 0f, 0f));
            _painShader.SetParameter("darknessAlphaOuter", 1.0f);
            _painShader.SetParameter("outerCircleRadius", outerRadius);
            _painShader.SetParameter("outerCircleMaxRadius", outerRadius + 0.15f * distance);
            _painShader.SetParameter("innerCircleRadius", innerRadius);
            _painShader.SetParameter("innerCircleMaxRadius", innerRadius);
            handle.DrawRect(viewport, Color.White);
        }

        handle.UseShader(null);
    }

    private float GetDiff(float value, float lastFrameTime)
    {
        var adjustment = value * 5f * lastFrameTime;

        if (value < 0f)
            adjustment = Math.Clamp(adjustment, value, -value);
        else
            adjustment = Math.Clamp(adjustment, -value, value);

        return adjustment;
    }
}
