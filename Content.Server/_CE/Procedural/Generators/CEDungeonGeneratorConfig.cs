using Robust.Shared.Map;

namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Abstract base for all dungeon generator configurations.
/// Concrete configs define data for a specific generation strategy (static map, procedural, etc.)
/// and raise typed events so the matching <see cref="CEDungeonGeneratorSystem{TConfig}"/> can handle them.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonGeneratorConfig
{
    /// <summary>
    /// Raises a <see cref="CEDungeonGenerateEvent{T}"/> to dispatch generation to the correct handler system.
    /// Returns the result of the generation.
    /// </summary>
    public abstract CEDungeonGenerateResult Generate(IEntityManager entMan);
}

/// <summary>
/// Result returned by <see cref="CEDungeonGeneratorConfig.Generate"/>.
/// Contains only the primary map created by the generator.
/// Any generator-specific data (z-networks, grids, etc.) should be managed internally by the generator system.
/// </summary>
public record struct CEDungeonGenerateResult(
    bool Success,
    EntityUid? MapUid = null,
    MapId? MapId = null);

public abstract partial class CEDungeonGeneratorConfigBase<T> : CEDungeonGeneratorConfig
    where T : CEDungeonGeneratorConfigBase<T>
{
    public override CEDungeonGenerateResult Generate(IEntityManager entMan)
    {
        if (this is not T typed)
            return new CEDungeonGenerateResult(false);

        var ev = new CEDungeonGenerateEvent<T>(typed);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);

        return new CEDungeonGenerateResult(ev.Handled, ev.MapUid, ev.MapId);
    }
}
