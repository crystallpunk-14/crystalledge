using Content.Shared._CE.Frost;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared._CE.Water;
using Content.Shared.Examine;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Serialization;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Fire;

public sealed partial class CEFireSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stack = default!;
    [Dependency] private readonly SharedMapSystem _mapSystem = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IEntityManager _entManager = default!;
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly EntProtoId _defaultFireProto = "CEFireTileLow";
    private readonly EntProtoId _fireImpactEffect = "CEFireImpactEffect";
    private readonly EntProtoId _steamEffect = "CESteamEffect";

    private readonly SoundSpecifier _fireSound = new SoundPathSpecifier("/Audio/_CE/Effects/fire_whoosh.ogg");
    private readonly SoundSpecifier _steamSound = new SoundPathSpecifier("/Audio/_CE/Effects/steam_burst.ogg");

    private EntityQuery<CEFireComponent> _fireQuery;
    private EntityQuery<CEFlammableComponent> _flammableQuery;
    private EntityQuery<CEPreventIgniteTileComponent> _preventIgniteQuery;

    /// <summary>
    /// Maximum number of steam sounds allowed per game tick to prevent audio engine overload.
    /// Visual steam effects still spawn on every tile/entity.
    /// </summary>
    private const int MaxSteamSoundsPerTick = 2;
    private GameTick _lastSteamSoundTick;
    private int _steamSoundCount;

    public override void Initialize()
    {
        base.Initialize();

        _fireQuery = GetEntityQuery<CEFireComponent>();
        _flammableQuery = GetEntityQuery<CEFlammableComponent>();
        _preventIgniteQuery = GetEntityQuery<CEPreventIgniteTileComponent>();

        // Prevent fire on tiles with CEPreventIgniteTileComponent (water, etc.).
        SubscribeLocalEvent<CEPreventIgniteTileComponent, CEIgniteTileAttemptEvent>(OnPreventIgniteTile);
        SubscribeLocalEvent<TransformComponent, CEIgniteEntityAttemptEvent>(OnPreventIgniteOnTile);

        SubscribeLocalEvent<CEFireComponent, MapInitEvent>(OnFireMapInit);
        SubscribeLocalEvent<CEFireComponent, StartCollideEvent>(OnCollide);
        SubscribeLocalEvent<CEMeltTransformComponent, CEIgnitedEvent>(OnMeltingIgnited);
        SubscribeLocalEvent<CEFlammableComponent, CEIgnitedEvent>(OnFlammableIgnited);

        SubscribeLocalEvent<CEFlammableComponent, MapInitEvent>(OnMapInit);

        // Tile attempt: melt-transform entities block fire tile placement by transforming.
        SubscribeLocalEvent<CEMeltTransformComponent, CEIgniteTileAttemptEvent>(OnMeltTileIgniteAttempt);
    }

    #region Fire Prevention (tile-level)

    /// <summary>
    /// Blocks fire tile placement on tiles with <see cref="CEPreventIgniteTileComponent"/> (e.g. water).
    /// </summary>
    private void OnPreventIgniteTile(Entity<CEPreventIgniteTileComponent> ent, ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        args.Cancelled = true;
        SpawnSteamEffect(args.Coordinates);
    }

    /// <summary>
    /// Prevents ignition of entities standing on a tile with <see cref="CEPreventIgniteTileComponent"/>.
    /// </summary>
    private void OnPreventIgniteOnTile(Entity<TransformComponent> ent, ref CEIgniteEntityAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!IsOnPreventIgniteTile(ent))
            return;

        args.Cancelled = true;
        SpawnSteamEffect(args.Target);
    }

    private bool IsOnPreventIgniteTile(Entity<TransformComponent> ent)
    {
        var xform = ent.Comp;
        if (xform.GridUid is not { } gridUid || !TryComp<MapGridComponent>(gridUid, out var grid))
            return false;

        var coords = _transform.GetMapCoordinates(ent);
        var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), coords);

        foreach (var anchEnt in anchored)
        {
            if (_preventIgniteQuery.HasComp(anchEnt))
                return true;
        }

        return false;
    }

    #endregion

    private void OnMapInit(Entity<CEFlammableComponent> ent, ref MapInitEvent args)
    {
        if (_net.IsClient)
            return;

        //We just wanna randomize some flame time duration duration
        var dur = ent.Comp.BurnCycleDuration.TotalSeconds;
        ent.Comp.BurnCycleDuration = TimeSpan.FromSeconds(_random.NextDouble(dur * 0.75, dur * 1.25));
        Dirty(ent);
    }

    private void OnFlammableIgnited(Entity<CEFlammableComponent> ent, ref CEIgnitedEvent args)
    {
        if (_net.IsClient)
            return;

        var stacks = args.Stacks;
        var cycleDuration = ent.Comp.BurnCycleDuration;

        if (args.MaxStacks != null)
        {
            var current = _stack.GetStack(ent, ent.Comp.StatusEffect);
            var allowed = Math.Max(0, args.MaxStacks.Value - current);
            if (allowed <= 0)
                return;

            stacks = Math.Min(stacks, allowed);
        }

        _stack.TryAddStack(ent, ent.Comp.StatusEffect, out _, stacks, cycleDuration);
        _stack.SetStackDelta(ent, ent.Comp.StatusEffect, ent.Comp.StackDelta);
    }

    /// <summary>
    /// When fire tile is about to be placed on a tile with a melt-transform entity (e.g. ice),
    /// the entity transforms into its melted form and fire placement is cancelled.
    /// </summary>
    private void OnMeltTileIgniteAttempt(Entity<CEMeltTransformComponent> ent, ref CEIgniteTileAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var coordinates = _transform.GetMapCoordinates(ent, xform);

        _entManager.DeleteEntity(ent);

        var restored = _entManager.SpawnEntity(ent.Comp.MeltsInto, coordinates);
        _transform.SetLocalRotation(restored, rotation);

        args.Cancelled = true;
        SpawnSteamEffect(args.Coordinates);
    }

    private void OnMeltingIgnited(Entity<CEMeltTransformComponent> ent, ref CEIgnitedEvent args)
    {
        if (_net.IsClient)
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var coordinates = _transform.GetMapCoordinates(ent, xform);

        _entManager.DeleteEntity(ent);

        var restored = _entManager.SpawnEntity(ent.Comp.MeltsInto, coordinates);
        _transform.SetLocalRotation(restored, rotation);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_net.IsClient)
            return;

        var query = EntityQueryEnumerator<CEFireComponent>();
        while (query.MoveNext(out var uid, out var fire))
        {
            if (_timing.CurTime < fire.NextDecayTime)
                continue;

            fire.NextDecayTime = _timing.CurTime + TimeSpan.FromSeconds(
                _random.NextFloat(fire.MinDecayInterval, fire.MaxDecayInterval));

            AddStacks((uid, fire), -1);
        }
    }

    private void OnCollide(Entity<CEFireComponent> ent, ref StartCollideEvent args)
    {
        IgniteEntity(args.OtherEntity, ent, ent.Comp.Stacks, ent.Comp.Stacks);
    }

    private void OnFireMapInit(Entity<CEFireComponent> ent, ref MapInitEvent args)
    {
        // Set initial decay time.
        ent.Comp.NextDecayTime = _timing.CurTime + TimeSpan.FromSeconds(
            _random.NextFloat(ent.Comp.MinDecayInterval, ent.Comp.MaxDecayInterval));

        // Update appearance for initial stacks.
        UpdateAppearance(ent);

        // Element interaction: check for opposing element on the tile.
        var coords = _transform.GetMapCoordinates(ent);
        var xform = Transform(ent);
        if (xform.GridUid is { } gridUid && TryComp<MapGridComponent>(gridUid, out var grid))
        {
            var attemptEv = new CEIgniteTileAttemptEvent(coords, ent.Comp.Stacks, false);
            var anchored = _mapSystem.GetAnchoredEntities((gridUid, grid), coords);
            foreach (var anch in anchored)
            {
                if (anch == ent.Owner)
                    continue;

                RaiseLocalEvent(anch, ref attemptEv);
                if (attemptEv.Cancelled)
                {
                    Del(ent);
                    return;
                }
            }
        }

        // Ignite entities already on the tile.
        var entitiesOnTile = _lookup.GetEntitiesInRange(coords, 0.5f, LookupFlags.Uncontained);
        foreach (var entity in entitiesOnTile)
        {
            IgniteEntity(entity, ent, ent.Comp.Stacks, ent.Comp.Stacks);
        }
    }

    /// <summary>
    /// Adds or removes stacks from a fire tile. Handles appearance updates and deletion at 0 stacks.
    /// </summary>
    public void AddStacks(Entity<CEFireComponent> ent, int delta)
    {
        if (delta == 0)
            return;

        var oldStacks = ent.Comp.Stacks;
        var newStacks = Math.Max(0, ent.Comp.Stacks + delta);
        if (ent.Comp.MaxStacks > 0)
            newStacks = Math.Min(newStacks, ent.Comp.MaxStacks);
        ent.Comp.Stacks = newStacks;
        Dirty(ent);

        if (ent.Comp.Stacks <= 0)
        {
            Del(ent);
            return;
        }

        if (oldStacks != ent.Comp.Stacks)
            UpdateAppearance(ent);
    }

    /// <summary>
    /// Sets the fire tile to a specific stack count. Handles appearance updates and deletion at 0 stacks.
    /// </summary>
    public void SetStacks(Entity<CEFireComponent> ent, int stacks)
    {
        var capped = Math.Max(0, stacks);
        if (ent.Comp.MaxStacks > 0)
            capped = Math.Min(capped, ent.Comp.MaxStacks);

        if (ent.Comp.Stacks == capped)
            return;

        ent.Comp.Stacks = capped;
        Dirty(ent);

        if (ent.Comp.Stacks <= 0)
        {
            Del(ent);
            return;
        }

        UpdateAppearance(ent);
    }

    private void UpdateAppearance(Entity<CEFireComponent> ent)
    {
        var level = CEFireTileVisualLevel.Low;
        if (ent.Comp.Stacks >= ent.Comp.MediumThreshold)
            level = CEFireTileVisualLevel.Medium;
        if (ent.Comp.Stacks >= ent.Comp.HighThreshold)
            level = CEFireTileVisualLevel.High;

        _appearance.SetData(ent, CEFireTileVisuals.Level, level);
    }

    private int CalculateFireStacks(float normalizedDistance, float falloffFactor, int maxStacks)
    {
        var adjustedDistance = MathF.Pow(normalizedDistance, falloffFactor);
        var intensity = 1f - adjustedDistance;
        return Math.Max(1, (int)MathF.Ceiling(intensity * maxStacks));
    }

    /// <summary>
    /// Removes all CE fire stacks from an entity and spawns a steam effect.
    /// </summary>
    /// <returns>True if the entity had fire and was extinguished.</returns>
    public bool ExtinguishEntity(Entity<CEFlammableComponent?> target)
    {
        if (_net.IsClient)
            return false;

        if (!_flammableQuery.Resolve(target, ref target.Comp, logMissing: false))
            return false;

        var stacks = _stack.GetStack(target, target.Comp.StatusEffect);
        if (stacks <= 0)
            return false;

        _stack.TryRemoveStack(target, target.Comp.StatusEffect, stacks);
        SpawnSteamEffect(target);
        return true;
    }

    /// <summary>
    /// Spawns a steam effect at an entity's position.
    /// Sound is throttled to <see cref="MaxSteamSoundsPerTick"/> per tick.
    /// </summary>
    public void SpawnSteamEffect(EntityUid target)
    {
        if (_net.IsClient)
            return;

        var pos = Transform(target).Coordinates;
        Spawn(_steamEffect, pos);

        if (CanPlaySteamSound())
            _audio.PlayPvs(_steamSound, pos);
    }

    /// <summary>
    /// Spawns a steam effect at map coordinates.
    /// Sound is throttled to <see cref="MaxSteamSoundsPerTick"/> per tick.
    /// </summary>
    public void SpawnSteamEffect(MapCoordinates coordinates)
    {
        if (_net.IsClient)
            return;

        var steam = _entManager.SpawnEntity(_steamEffect, coordinates);

        if (CanPlaySteamSound())
            _audio.PlayPvs(_steamSound, Transform(steam).Coordinates);
    }

    private bool CanPlaySteamSound()
    {
        var curTick = _timing.CurTick;
        if (curTick != _lastSteamSoundTick)
        {
            _lastSteamSoundTick = curTick;
            _steamSoundCount = 0;
        }

        if (_steamSoundCount >= MaxSteamSoundsPerTick)
            return false;

        _steamSoundCount++;
        return true;
    }
}

/// <summary>
/// Appearance visuals key for fire tile entities.
/// </summary>
[NetSerializable, Serializable]
public enum CEFireTileVisuals
{
    Level,
}

/// <summary>
/// Visual level of a fire tile, driven by stack thresholds.
/// </summary>
[NetSerializable, Serializable]
public enum CEFireTileVisualLevel
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Raised as a directed event on the target entity before fire stacks are applied.
/// Handlers can modify <see cref="Stacks"/> or set <see cref="Cancelled"/> to prevent ignition.
/// Handled by <c>CEFrostSystem</c> for frost neutralization and <c>CESharedWaterSystem</c> for water blocking.
/// </summary>
[ByRefEvent]
public record struct CEIgniteEntityAttemptEvent(EntityUid Target, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a directed event on each anchored entity on a tile before fire is placed.
/// Handlers can set <see cref="Cancelled"/> to block fire tile placement.
/// Handled by <c>CEMeltTransformComponent</c> (ice melting) and <c>CEWaterComponent</c> (water blocking).
/// </summary>
[ByRefEvent]
public record struct CEIgniteTileAttemptEvent(MapCoordinates Coordinates, int Stacks, bool Cancelled);

/// <summary>
/// Raised as a directed event on an entity when it receives a fire/ignite effect.
/// Carries the fire intensity for handlers to apply their specific effects.
/// </summary>
[ByRefEvent]
public record struct CEIgnitedEvent(int Stacks = 0, int? MaxStacks = null);
