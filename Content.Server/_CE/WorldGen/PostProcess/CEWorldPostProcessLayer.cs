using Content.Server._CE.WorldGen.Generators;

namespace Content.Server._CE.WorldGen.PostProcess;

/// <summary>
/// Data-only base for per-tile worldgen post-processing layers. Logic is in systems subscribing to
/// <see cref="CEWorldPostProcessEvent{T}"/>, mirroring the <c>CEChunkGenerator</c> ECS pattern.
///
/// Unlike <c>CEDungeonPostProcessLayer</c>, these layers are synchronous and per-tile — no async,
/// no full-map iteration. Only called for non-void, non-player-modified tiles.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract partial class CEWorldPostProcessLayer
{
    /// <summary>
    /// Dispatches this layer by raising a typed broadcast event.
    /// </summary>
    public abstract void Process(
        IEntityManager entMan,
        in CETileGenContext ctx,
        ref CETileContent content,
        int layerSeed);
}

/// <summary>
/// Generic base that provides automatic event dispatch for concrete layer types.
/// Concrete layers inherit from this instead of <see cref="CEWorldPostProcessLayer"/> directly.
/// </summary>
public abstract partial class CEWorldPostProcessLayerBase<T> : CEWorldPostProcessLayer
    where T : CEWorldPostProcessLayerBase<T>
{
    public override void Process(
        IEntityManager entMan,
        in CETileGenContext ctx,
        ref CETileContent content,
        int layerSeed)
    {
        if (this is not T typed)
            return;

        var ev = new CEWorldPostProcessEvent<T>(typed, ctx, content, layerSeed);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        content = ev.Content;
    }
}

/// <summary>
/// Broadcast event raised when a post-process layer is dispatched for a tile.
/// <see cref="Content"/> may be modified by the handling system.
/// </summary>
[ByRefEvent]
public struct CEWorldPostProcessEvent<T>(T layer, CETileGenContext ctx, CETileContent content, int layerSeed)
    where T : CEWorldPostProcessLayerBase<T>
{
    public readonly T Layer = layer;
    public readonly CETileGenContext Ctx = ctx;
    public CETileContent Content = content;
    public readonly int LayerSeed = layerSeed;
}

/// <summary>
/// Abstract base system for handling worldgen post-process layers.
/// </summary>
public abstract partial class CEWorldPostProcessSystem<T> : EntitySystem
    where T : CEWorldPostProcessLayerBase<T>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEWorldPostProcessEvent<T>>(OnProcess);
    }

    private void OnProcess(ref CEWorldPostProcessEvent<T> ev)
    {
        Process(ref ev);
    }

    /// <summary>
    /// Modify <see cref="CEWorldPostProcessEvent{T}.Content"/> to apply this layer.
    /// </summary>
    protected abstract void Process(ref CEWorldPostProcessEvent<T> ev);
}
