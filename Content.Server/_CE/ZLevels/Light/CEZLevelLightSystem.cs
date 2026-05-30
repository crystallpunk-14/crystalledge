/*
 * This file is sublicensed under MIT License
 * https://github.com/space-wizards/space-station-14/blob/master/LICENSE.TXT
 */

using System.Numerics;
using Content.Shared._CE.ZLevels.Core.Components;
using Content.Shared._CE.ZLevels.Core.EntitySystems;
using Content.Shared._CE.ZLevels.Light;
using Content.Shared.Maps;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Timing;

namespace Content.Server._CE.ZLevels.Light;

/// <summary>
/// Manages propagation of light through transparent tiles across Z-levels by spawning
/// proxy <see cref="PointLightComponent"/> entities on levels above and below a
/// <see cref="CEZLevelLightComponent"/>.
///
/// Propagation rules:
///   Downward: if the tile at the source's position on the current Z-level is transparent,
///     a proxy is spawned one level below with energy − <see cref="CEZLevelLightComponent.EnergyStep"/>.
///     This repeats until a solid tile or MinEnergy is reached.
///   Upward: if the tile on the level ABOVE is transparent (no opaque ceiling),
///     a proxy is spawned there with the same linear falloff.
///   Lowest proxy: when downward propagation is stopped by a solid tile (not by energy),
///     one extra shadow-free proxy is spawned one level below that floor
///     (controlled by <see cref="CEZLevelLightComponent.SpawnLowestProxy"/>).
///   Proxy positions are updated immediately on every MoveEvent.
///   Full tile re-evaluation happens on a configurable periodic timer.
/// </summary>
public sealed class CEZLevelLightSystem : EntitySystem
{
    [Dependency] private readonly CESharedZLevelsSystem _zLevels = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedPointLightSystem _lightSys = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDef = default!;
    [Dependency] private readonly SharedMapSystem _map = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    [Dependency] private readonly EntityQuery<CEZLevelMapComponent> _zMapQuery = default!;
    [Dependency] private readonly EntityQuery<MapGridComponent> _gridQuery = default!;
    [Dependency] private readonly EntityQuery<MapComponent> _mapCompQuery = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEZLevelLightComponent, ComponentStartup>(OnStartup);
        SubscribeLocalEvent<CEZLevelLightComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CEZLevelLightComponent, PointLightToggleEvent>(OnLightToggle);
    }

    /// <summary>
    /// Periodic timer loop: performs a full rebuild for any component whose interval has elapsed.
    /// </summary>
    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var now = _timing.CurTime;
        var query = EntityQueryEnumerator<CEZLevelLightComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (now < comp.NextUpdate)
                continue;

            comp.NextUpdate = now + comp.UpdateInterval;
            RebuildProxies((uid, comp));
        }
    }

    // ── Event handlers ────────────────────────────────────────────────────

    private void OnStartup(Entity<CEZLevelLightComponent> ent, ref ComponentStartup args)
    {
        RebuildProxies(ent);
    }

    private void OnShutdown(Entity<CEZLevelLightComponent> ent, ref ComponentShutdown args)
    {
        DeleteProxies(ent);
    }

    private void OnLightToggle(Entity<CEZLevelLightComponent> ent, ref PointLightToggleEvent args)
    {
        RebuildProxies(ent);
    }

    // ── Core logic ────────────────────────────────────────────────────────

    private void DeleteProxies(Entity<CEZLevelLightComponent> ent)
    {
        foreach (var proxy in ent.Comp.Proxies)
        {
            if (!TerminatingOrDeleted(proxy))
                QueueDel(proxy);
        }
        ent.Comp.Proxies.Clear();

        if (!TerminatingOrDeleted(ent.Comp.LowestProxy))
            QueueDel(ent.Comp.LowestProxy);
        ent.Comp.LowestProxy = EntityUid.Invalid;
    }

    private void RebuildProxies(Entity<CEZLevelLightComponent> ent)
    {
        if (!_lightSys.TryGetLight(ent, out var light) || !light.Enabled)
        {
            DeleteProxies(ent);
            return;
        }

        var xform = Transform(ent);
        if (xform.MapUid is not { } mapUid || !_zMapQuery.TryComp(mapUid, out var zMap))
        {
            DeleteProxies(ent);
            return;
        }

        var worldPos = _transform.GetWorldPosition(ent);
        var (desiredProxies, desiredLowest) = ComputeDesiredProxies(ent.Comp, light, (mapUid, zMap), worldPos);

        ReconcileProxyList(ent, desiredProxies, light, worldPos);
        ReconcileLowestProxy(ent, desiredLowest, light, worldPos);
    }

    private void ReconcileProxyList(
        Entity<CEZLevelLightComponent> ent,
        List<(EntityUid Map, float Energy)> desired,
        SharedPointLightComponent light,
        Vector2 worldPos)
    {
        // Remove excess proxies from the tail.
        while (ent.Comp.Proxies.Count > desired.Count)
        {
            var lastIdx = ent.Comp.Proxies.Count - 1;
            var stale = ent.Comp.Proxies[lastIdx];
            if (!TerminatingOrDeleted(stale))
                QueueDel(stale);
            ent.Comp.Proxies.RemoveAt(lastIdx);
        }

        // Update existing proxies or create missing ones.
        for (var i = 0; i < desired.Count; i++)
        {
            var (targetMapUid, proxyEnergy) = desired[i];
            if (!_mapCompQuery.TryComp(targetMapUid, out var mapComp))
                continue;

            var targetCoords = new MapCoordinates(worldPos, mapComp.MapId);

            EntityUid proxyUid;
            if (i < ent.Comp.Proxies.Count)
            {
                proxyUid = ent.Comp.Proxies[i];
                if (TerminatingOrDeleted(proxyUid))
                {
                    proxyUid = SpawnProxy(targetCoords);
                    ent.Comp.Proxies[i] = proxyUid;
                }
                else
                {
                    _transform.SetMapCoordinates(proxyUid, targetCoords);
                }
            }
            else
            {
                proxyUid = SpawnProxy(targetCoords);
                ent.Comp.Proxies.Add(proxyUid);
            }

            ApplyLightToProxy(proxyUid, light, proxyEnergy, light.CastShadows);
        }
    }

    private void ReconcileLowestProxy(
        Entity<CEZLevelLightComponent> ent,
        (EntityUid Map, float Energy)? desired,
        SharedPointLightComponent light,
        Vector2 worldPos)
    {
        if (desired is null)
        {
            if (!TerminatingOrDeleted(ent.Comp.LowestProxy))
            {
                QueueDel(ent.Comp.LowestProxy);
                ent.Comp.LowestProxy = EntityUid.Invalid;
            }
            return;
        }

        var (targetMapUid, proxyEnergy) = desired.Value;
        if (!_mapCompQuery.TryComp(targetMapUid, out var mapComp))
        {
            if (!TerminatingOrDeleted(ent.Comp.LowestProxy))
            {
                QueueDel(ent.Comp.LowestProxy);
                ent.Comp.LowestProxy = EntityUid.Invalid;
            }
            return;
        }

        var targetCoords = new MapCoordinates(worldPos, mapComp.MapId);

        if (TerminatingOrDeleted(ent.Comp.LowestProxy))
        {
            ent.Comp.LowestProxy = SpawnProxy(targetCoords);
        }
        else
        {
            _transform.SetMapCoordinates(ent.Comp.LowestProxy, targetCoords);
        }

        // Lowest proxy always has shadows disabled.
        ApplyLightToProxy(ent.Comp.LowestProxy, light, proxyEnergy, castShadows: false);
    }

    // ── Computation ───────────────────────────────────────────────────────

    /// <summary>
    /// Computes the desired set of proxy lights propagating downward and upward using linear energy falloff.
    /// Returns: regular proxies (down first, then up) + an optional Lowest proxy.
    /// The Lowest proxy is only produced when downward propagation stops at a solid tile.
    /// </summary>
    private (List<(EntityUid Map, float Energy)> Proxies, (EntityUid Map, float Energy)? Lowest)
        ComputeDesiredProxies(
            CEZLevelLightComponent comp,
            SharedPointLightComponent light,
            Entity<CEZLevelMapComponent> sourceMap,
            Vector2 worldPos)
    {
        var proxies = new List<(EntityUid, float)>();
        (EntityUid, float)? lowest = null;

        // ── Downward propagation ──────────────────────────────────────────
        // Use nullable wrapper because TryMapDown/TryMapUp require Entity<T?>.
        Entity<CEZLevelMapComponent?> currentMap = (sourceMap.Owner, sourceMap.Comp);
        var currentEnergy = light.Energy;
        var stoppedByFloor = false;

        while (true)
        {
            if (!IsTileTransparentAt(currentMap.Owner, worldPos))
            {
                // Hit a solid tile – floor reached.
                stoppedByFloor = true;
                break;
            }

            if (!_zLevels.TryMapDown(currentMap, out var mapBelow))
                break;

            currentEnergy -= comp.EnergyStep;
            if (currentEnergy < comp.MinEnergy)
                break;

            proxies.Add((mapBelow.Owner, currentEnergy));
            currentMap = (mapBelow.Owner, mapBelow.Comp);
        }

        // Lowest proxy: one extra level below the solid floor, always shadow-free.
        if (stoppedByFloor && comp.SpawnLowestProxy && _zLevels.TryMapDown(currentMap, out var lowestMapBelow))
        {
            var lowestEnergy = MathF.Max(comp.MinEnergy, currentEnergy - comp.EnergyStep);
            lowest = (lowestMapBelow.Owner, lowestEnergy);
        }

        // ── Upward propagation ────────────────────────────────────────────
        Entity<CEZLevelMapComponent?> upMap = (sourceMap.Owner, sourceMap.Comp);
        var upEnergy = light.Energy;

        while (true)
        {
            if (!_zLevels.TryMapUp(upMap, out var mapAbove))
                break;

            // Non-transparent tile on the level above = opaque ceiling; stop.
            if (!IsTileTransparentAt(mapAbove.Owner, worldPos))
                break;

            upEnergy -= comp.EnergyStep;
            if (upEnergy < comp.MinEnergy)
                break;

            proxies.Add((mapAbove.Owner, upEnergy));
            upMap = (mapAbove.Owner, mapAbove.Comp);
        }

        return (proxies, lowest);
    }

    private bool IsTileTransparentAt(EntityUid mapUid, Vector2 worldPos)
    {
        if (!_gridQuery.TryComp(mapUid, out var grid))
            return true; // No grid – treat as open void.

        if (!_map.TryGetTileRef(mapUid, grid, worldPos, out var tileRef) || tileRef.Tile.IsEmpty)
            return true; // Missing or empty tile is transparent.

        var tileDef = (ContentTileDefinition)_tileDef[tileRef.Tile.TypeId];
        return tileDef.Transparent;
    }

    private EntityUid SpawnProxy(MapCoordinates coords)
    {
        var uid = Spawn(null, coords);
        EnsureComp<CEZLevelLightProxyComponent>(uid);
        return uid;
    }

    private void ApplyLightToProxy(EntityUid proxy, SharedPointLightComponent source, float energy, bool castShadows)
    {
        var proxyLight = EnsureComp<PointLightComponent>(proxy);
        _lightSys.SetEnabled(proxy, true, proxyLight);
        _lightSys.SetColor(proxy, source.Color, proxyLight);
        _lightSys.SetEnergy(proxy, energy, proxyLight);
        _lightSys.SetRadius(proxy, source.Radius, proxyLight);
        _lightSys.SetSoftness(proxy, source.Softness, proxyLight);
        _lightSys.SetFalloff(proxy, source.Falloff, proxyLight);
        _lightSys.SetCurveFactor(proxy, source.CurveFactor, proxyLight);
        _lightSys.SetCastShadows(proxy, castShadows, proxyLight);
    }
}

