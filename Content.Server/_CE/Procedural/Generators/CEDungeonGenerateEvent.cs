using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Raised as a broadcast event when a dungeon level needs to be generated.
/// The strongly-typed config <typeparamref name="T"/> lets the matching
/// <see cref="CEDungeonGeneratorSystem{T}"/> pick it up via ECS event subscription.
/// </summary>
/// <remarks>
/// Handlers should set <see cref="MapUid"/>, <see cref="MapId"/>, and <see cref="Handled"/>.
/// Any generator-specific state (z-networks, grids, etc.) should be managed internally.
/// </remarks>
[ByRefEvent]
public record struct CEDungeonGenerateEvent<T>(T Config) where T : CEDungeonGeneratorConfigBase<T>
{
    /// <summary>
    /// Set by the handler: the EntityUid of the primary map entity.
    /// </summary>
    public EntityUid? MapUid;

    /// <summary>
    /// Set by the handler: the MapId of the primary map.
    /// </summary>
    public MapId? MapId;

    /// <summary>
    /// Set to true by the handler if generation succeeded.
    /// </summary>
    public bool Handled;
}
