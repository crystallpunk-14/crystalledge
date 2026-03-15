namespace Content.Server._CE.Procedural.Generators;

/// <summary>
/// Abstract base system for handling a specific <see cref="CEDungeonGeneratorConfigBase{T}"/>.
/// Each concrete generator system subscribes to <see cref="CEDungeonGenerateEvent{TConfig}"/>
/// and implements the <see cref="Generate"/> method to perform the actual map creation/loading.
/// </summary>
/// <typeparam name="TConfig">The concrete generator config type this system handles.</typeparam>
public abstract partial class CEDungeonGeneratorSystem<TConfig> : EntitySystem
    where TConfig : CEDungeonGeneratorConfigBase<TConfig>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDungeonGenerateEvent<TConfig>>(OnGenerate);
    }

    private void OnGenerate(ref CEDungeonGenerateEvent<TConfig> args)
    {
        Generate(ref args);
    }

    /// <summary>
    /// Perform the actual generation logic for this config type.
    /// Implementations should set <see cref="CEDungeonGenerateEvent{T}.MapUid"/>,
    /// <see cref="CEDungeonGenerateEvent{T}.MapId"/>, and <see cref="CEDungeonGenerateEvent{T}.Handled"/>.
    /// </summary>
    protected abstract void Generate(ref CEDungeonGenerateEvent<TConfig> args);
}
