using System.Threading.Tasks;

namespace Content.Server._CE.Procedural.PostProcess;

/// <summary>
/// Base class for dungeon post-processing layers. Layers run sequentially after
/// dungeon generation completes. Deserialized polymorphically from YAML via
/// <c>!type:</c> tags on the <c>postProcess</c> field of <c>dungeonLevel</c> prototypes.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEDungeonPostProcessLayer
{
    /// <summary>
    /// Executes this post-processing layer on the given dungeon map.
    /// </summary>
    /// <param name="entMan">Entity manager for resolving systems and components.</param>
    /// <param name="mapUid">The primary map entity of the dungeon.</param>
    /// <param name="mainZLevel">The z-level depth to treat as the main level.</param>
    /// <param name="suspend">Cooperative yield function to call periodically to avoid frame hitches.</param>
    public abstract Task Execute(
        IEntityManager entMan,
        EntityUid mapUid,
        int mainZLevel,
        Func<ValueTask> suspend);
}
