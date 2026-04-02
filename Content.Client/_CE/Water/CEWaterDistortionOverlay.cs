using Content.Client.Graphics;
using Content.Shared._CE.Water;
using Content.Shared.CCVar;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Prototypes;
using System.Numerics;
using Content.Client.Resources;
using Texture = Robust.Client.Graphics.Texture;

namespace Content.Client._CE.Water;

/// <summary>
/// Overlay responsible for rendering water distortion shader on tiles
/// with an anchored <see cref="CEWaterDistortionComponent"/> entity.
/// </summary>
public sealed class CEWaterDistortionOverlay : Overlay
{
    public override bool RequestScreenTexture { get; set; } = true;
    public override OverlaySpace Space => OverlaySpace.WorldSpace;

    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";
    private static readonly ProtoId<ShaderPrototype> WaterDistortionShader = "CEWaterDistortion";

    // Reduced motion multipliers
    private const float ReducedMotionStrengthMul = 0.15f;
    private const float ReducedMotionScaleMul = 0.33f;
    private const float ReducedMotionSpeedMul = 0.25f;

    private bool _reducedMotion;
    private float _currentStrength = 0.06f;
    private float _currentScale = 1.5f;
    private float _currentSpeed = 0.2f;

    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IClyde _clyde = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;

    private readonly EntityLookupSystem _lookup;
    private readonly SharedTransformSystem _xformSys;
    private readonly EntityQuery<TransformComponent> _xformQuery;
    private readonly ShaderInstance _shader;
    private readonly Texture _noiseTexture;

    private readonly HashSet<Entity<CEWaterDistortionComponent>> _entities = new();
    private List<Entity<MapGridComponent>> _grids = new();
    private readonly OverlayResourceCache<CachedResources> _resources = new();

    public CEWaterDistortionOverlay(IEntityManager entManager)
    {
        IoCManager.InjectDependencies(this);

        _lookup = entManager.System<EntityLookupSystem>();
        _xformSys = entManager.System<SharedTransformSystem>();
        _xformQuery = entManager.GetEntityQuery<TransformComponent>();

        _noiseTexture = _resourceCache.GetTexture("/Textures/_CE/Shaders/perlin_noise.png");
        _shader = _proto.Index(WaterDistortionShader).InstanceUnique();

        _configManager.OnValueChanged(CCVars.ReducedMotion, SetReducedMotion, invokeImmediately: true);
    }

    private void SetReducedMotion(bool reducedMotion)
    {
        _reducedMotion = reducedMotion;
    }

    protected override bool BeforeDraw(in OverlayDrawArgs args)
    {
        if (args.MapId == MapId.Nullspace)
            return false;

        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());
        var target = args.Viewport.RenderTarget;

        if (res.WaterTarget?.Texture.Size != target.Size)
        {
            res.WaterTarget?.Dispose();
            res.WaterTarget = _clyde.CreateRenderTarget(
                target.Size,
                new RenderTargetFormatParameters(RenderTargetColorFormat.Rgba8Srgb),
                name: nameof(CEWaterDistortionOverlay));
        }

        var worldHandle = args.WorldHandle;
        var mapId = args.MapId;
        var worldAABB = args.WorldAABB;
        var worldBounds = args.WorldBounds;
        var viewport = args.Viewport;
        var anyDistortion = false;

        worldHandle.UseShader(_proto.Index(UnshadedShader).Instance());

        _grids.Clear();
        _mapManager.FindGridsIntersecting(mapId, worldAABB, ref _grids, approx: true);

        worldHandle.RenderInRenderTarget(res.WaterTarget,
            () =>
            {
                foreach (var grid in _grids)
                {
                    var gridInvMatrix = _xformSys.GetInvWorldMatrix(grid);
                    var localBounds = gridInvMatrix.TransformBox(worldBounds);

                    _entities.Clear();
                    _lookup.GetLocalEntitiesIntersecting(grid.Owner, localBounds, _entities);

                    if (_entities.Count == 0)
                        continue;

                    anyDistortion = true;

                    // Capture shader params from first visible entity
                    foreach (var first in _entities)
                    {
                        _currentStrength = first.Comp.Strength;
                        _currentScale = first.Comp.Scale;
                        _currentSpeed = first.Comp.Speed;
                        break;
                    }

                    var gridMatrix = _xformSys.GetWorldMatrix(grid.Owner);
                    var worldToViewportLocal = viewport.GetWorldToLocalMatrix();
                    var gridToViewportLocal = Matrix3x2.Multiply(gridMatrix, worldToViewportLocal);

                    worldHandle.SetTransform(gridToViewportLocal);

                    foreach (var ent in _entities)
                    {
                        var xform = _xformQuery.Comp(ent);
                        worldHandle.DrawRect(
                            Box2.CenteredAround(xform.LocalPosition, new Vector2(1f, 1f)),
                            Color.White);
                    }
                }
            },
            new Color(0, 0, 0, 0));

        if (!anyDistortion)
        {
            worldHandle.UseShader(null);
            worldHandle.SetTransform(Matrix3x2.Identity);
            return false;
        }

        return true;
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var res = _resources.GetForViewport(args.Viewport, static _ => new CachedResources());

        if (ScreenTexture is null || res.WaterTarget is null)
            return;

        var strength = _reducedMotion ? _currentStrength * ReducedMotionStrengthMul : _currentStrength;
        var scale = _reducedMotion ? _currentScale * ReducedMotionScaleMul : _currentScale;
        var speed = _reducedMotion ? _currentSpeed * ReducedMotionSpeedMul : _currentSpeed;

        _shader.SetParameter("strength_scale", strength);
        _shader.SetParameter("spatial_scale", scale);
        _shader.SetParameter("speed_scale", speed);
        _shader.SetParameter("SCREEN_TEXTURE", ScreenTexture);
        _shader.SetParameter("NOISE_TEXTURE", _noiseTexture);

        args.WorldHandle.UseShader(_shader);
        args.WorldHandle.DrawTextureRect(res.WaterTarget.Texture, args.WorldBounds);

        args.WorldHandle.UseShader(null);
        args.WorldHandle.SetTransform(Matrix3x2.Identity);
    }

    protected override void DisposeBehavior()
    {
        _resources.Dispose();
        _configManager.UnsubValueChanged(CCVars.ReducedMotion, SetReducedMotion);
        base.DisposeBehavior();
    }

    internal sealed class CachedResources : IDisposable
    {
        public IRenderTexture? WaterTarget;

        public void Dispose()
        {
            WaterTarget?.Dispose();
        }
    }
}
