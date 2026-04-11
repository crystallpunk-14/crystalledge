using Content.Shared._CE.Fire;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Examine;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Frost;

public sealed class CEFrostSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly CEFireSystem _fire = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId _freezeEffect = "CEFreezeEffect";
    private readonly SoundSpecifier _freezeSound = new SoundPathSpecifier("/Audio/_CE/Effects/ice_burst.ogg");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEFreezeTransformComponent, CEFreezedEvent>(OnFreezingFreezed);
        SubscribeLocalEvent<CEFreezableComponent, CEFreezedEvent>(OnFreezableFreezed);
        SubscribeLocalEvent<CEFreezableComponent, CEIgniteEntityAttemptEvent>(OnIgniteEntityAttempt);

        SubscribeLocalEvent<CEFreezeImmunityStatusEffectComponent, StatusEffectRelayedEvent<CEFreezeEntityAttemptEvent>>(OnFreezeImmunity);

        // Tile attempt: fire entities block frost tile placement (fire is extinguished).
        SubscribeLocalEvent<CEFireComponent, CEFreezeTileAttemptEvent>(OnFireFreezeTileAttempt);
    }

    private void OnFreezeImmunity(Entity<CEFreezeImmunityStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEFreezeEntityAttemptEvent> args)
    {
        var inner = args.Args;
        inner.Cancelled = true;
        args.Args = inner;
    }

    /// <summary>
    /// Frost neutralizes fire: when something tries to ignite a frosted entity,
    /// cold stacks cancel out an equal number of incoming fire stacks.
    /// </summary>
    private void OnIgniteEntityAttempt(Entity<CEFreezableComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        var frostStacks = _stack.GetFlammableStack(ent, ent.Comp.StatusEffect);
        if (frostStacks <= 0)
            return;

        var neutralized = Math.Min(frostStacks, args.Stacks);
        _stack.TryRemoveStack(ent, ent.Comp.StatusEffect, neutralized);
        args.Stacks -= neutralized;

        _fire.SpawnSteamEffect(ent);

        if (args.Stacks <= 0)
            args.Cancelled = true;
    }

    /// <summary>
    /// When frost tile is about to be placed on a tile with a fire entity,
    /// the fire is extinguished and frost placement is cancelled.
    /// </summary>
    private void OnFireFreezeTileAttempt(Entity<CEFireComponent> ent, ref CEFreezeTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!_net.IsClient)
            EntityManager.DeleteEntity(ent);

        args.Cancelled = true;
        _fire.SpawnSteamEffect(args.Coordinates);
    }

    private void OnFreezableFreezed(Entity<CEFreezableComponent> ent, ref CEFreezedEvent args)
    {
        if (_net.IsClient)
            return;

        var stacks = args.Stacks;
        var cycleDuration = args.Duration ?? ent.Comp.DefaultDuration;

        if (args.MaxStacks != null)
        {
            var current = _stack.GetFlammableStack(ent, ent.Comp.StatusEffect);
            var allowed = Math.Max(0, args.MaxStacks.Value - current);
            if (allowed <= 0)
                return;

            stacks = Math.Min(stacks, allowed);
        }

        _stack.TryAddStack(ent, ent.Comp.StatusEffect, out _, stacks, cycleDuration);
    }

    private void OnFreezingFreezed(Entity<CEFreezeTransformComponent> ent, ref CEFreezedEvent args)
    {
        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var coordinates = _transform.GetMapCoordinates(ent, xform);

        _entManager.DeleteEntity(ent);

        var frozen = _entManager.SpawnEntity(ent.Comp.FreezesInto, coordinates);
        _transform.SetLocalRotation(frozen, rotation);
    }

    /// <summary>
    /// Raises a <see cref="CEFreezedEvent"/> on the target entity.
    /// Entities with freezing-related components handle the event to apply their effects.
    /// </summary>
    public void FreezeEntity(EntityUid target, int stack = 1, int? maxStack = null, TimeSpan? duration = null)
    {
        if (stack <= 0)
            return;

        if (_net.IsClient)
            return;

        // Element interaction: frost vs fire mutual neutralization.
        var attemptEv = new CEFreezeEntityAttemptEvent(target, stack, false);
        RaiseLocalEvent(target, ref attemptEv);
        if (attemptEv.Cancelled)
            return;
        stack = attemptEv.Stacks;

        var freezedEv = new CEFreezedEvent(stack, maxStack, duration);
        RaiseLocalEvent(target, ref freezedEv);
    }

    /// <summary>
    /// Freezes all entities on the given tile by calling <see cref="FreezeEntity"/> on each.
    /// </summary>
    public void FreezeTile(Entity<MapGridComponent?> grid, MapCoordinates coordinates, int stacks = 1, int? maxStacks = null, TimeSpan? duration = null, bool playSound = true)
    {
        if (_net.IsClient)
            return;

        if (!Resolve(grid, ref grid.Comp))
            return;

        if (!_mapSystem.TryGetTileRef(grid.Owner, grid.Comp, coordinates.Position, out var tileRef) || tileRef.Tile.IsEmpty)
            return;

        var attemptEv = new CEFreezeTileAttemptEvent(coordinates, false);
        var anchored = _mapSystem.GetAnchoredEntities((grid, grid.Comp), coordinates);
        foreach (var ent in anchored)
        {
            RaiseLocalEvent(ent, ref attemptEv);
            if (attemptEv.Cancelled)
                return;
        }

        var entities = _lookup.GetEntitiesInRange(coordinates, 0.5f, LookupFlags.Uncontained);
        foreach (var ent in entities)
        {
            FreezeEntity(ent, stacks, maxStacks, duration);
        }

        // Spawn freeze visual effect.
        var fx = _entManager.SpawnEntity(_freezeEffect, coordinates);
        if (playSound)
            _audio.PlayPvs(_freezeSound, fx);
    }

    /// <summary>
    /// Freezes an area: spawns ice on tiles and applies cold slowdown to entities in range.
    /// </summary>
    public void FreezeArea(EntityCoordinates center, float radius = 3f, float falloffFactor = 0.5f, int maxStacks = 3)
    {
        if (_net.IsClient)
            return;

        if (radius <= 0f)
            return;

        var mapCenter = _transform.ToMapCoordinates(center);

        if (!_mapManager.TryFindGridAt(mapCenter, out var gridUid, out var grid))
            return;

        var centerWorld = mapCenter.Position;
        var tileSize = grid.TileSize;

        var minX = (int) MathF.Floor((centerWorld.X - radius) / tileSize);
        var maxX = (int) MathF.Ceiling((centerWorld.X + radius) / tileSize);
        var minY = (int) MathF.Floor((centerWorld.Y - radius) / tileSize);
        var maxY = (int) MathF.Ceiling((centerWorld.Y + radius) / tileSize);

        for (var x = minX; x <= maxX; x++)
        {
            for (var y = minY; y <= maxY; y++)
            {
                var tileIndices = new Vector2i(x, y);
                var tileWorldPos = _mapSystem.GridTileToWorldPos(gridUid, grid, tileIndices);
                var tileCoords = new MapCoordinates(tileWorldPos, mapCenter.MapId);

                var distance = (tileWorldPos - centerWorld).Length();

                if (distance > radius)
                    continue;

                if (!_examine.InRangeUnOccluded(mapCenter, tileCoords, radius, null))
                    continue;

                var normalizedDist = distance / radius;
                var stacks = CalculateStacks(normalizedDist, falloffFactor, maxStacks);
                FreezeTile((gridUid, grid), tileCoords, stacks, maxStacks, playSound: false);
            }
        }

        _audio.PlayPvs(_freezeSound, center);
    }

    private int CalculateStacks(float normalizedDistance, float falloffFactor, int maxStacks)
    {
        var adjustedDistance = MathF.Pow(normalizedDistance, falloffFactor);
        var intensity = 1f - adjustedDistance;
        return Math.Max(1, (int) MathF.Ceiling(intensity * maxStacks));
    }
}

/// <summary>
/// Raised as a broadcast event before frost/cold stacks are applied to an entity.
/// Handlers can modify Stacks or set Cancelled to prevent freezing.
/// </summary>
[ByRefEvent]
public record struct CEFreezeEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a directed event on each anchored entity on a tile before frost is placed.
/// Handlers can set <see cref="Cancelled"/> to block frost tile placement.
/// Handled by <c>CEFireComponent</c> (fire extinguishing).
/// </summary>
[ByRefEvent]
public record struct CEFreezeTileAttemptEvent(MapCoordinates Coordinates, bool Cancelled);

/// <summary>
/// Raised as a directed event on an entity when it receives a frost/freeze effect.
/// Carries the freeze intensity for handlers to apply their specific effects.
/// </summary>
[ByRefEvent]
public record struct CEFreezedEvent(int Stacks = 0, int? MaxStacks = null, TimeSpan? Duration = null);
