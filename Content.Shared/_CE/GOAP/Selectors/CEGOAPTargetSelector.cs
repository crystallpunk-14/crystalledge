using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.Health;
using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Maths;

namespace Content.Shared._CE.GOAP.Selectors;

/// <summary>
/// Data-only base for polymorphic GOAP target selectors. A selector resolves a target
/// to an entity, a coordinate, or both. Logic is implemented by per-type EntitySystems
/// derived from <see cref="CEGOAPTargetSelectorSystem{TSelector}"/>.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CEGOAPTargetSelector
{
    /// <summary>
    /// Optional conditions used to pre-filter candidate entities before selection.
    /// Each candidate must pass all conditions (AND logic). Ignored by selectors with no candidate list.
    /// </summary>
    [DataField]
    public List<CEEntityCondition> Conditions = new();

    /// <summary>
    /// Resolves the selector to an entity and/or coordinate.
    /// </summary>
    public abstract CEGOAPSelectorResult Resolve(EntityUid agent, IEntityManager entMan);
}

/// <summary>
/// Generic base providing automatic event dispatch for concrete selector types.
/// </summary>
public abstract partial class CEGOAPTargetSelectorBase<T> : CEGOAPTargetSelector
    where T : CEGOAPTargetSelectorBase<T>
{
    public override CEGOAPSelectorResult Resolve(EntityUid agent, IEntityManager entMan)
    {
        if (this is not T self)
            return default;

        var ev = new CEGOAPSelectorResolveEvent<T>(self, agent);
        entMan.EventBus.RaiseEvent(EventSource.Local, ref ev);
        return new CEGOAPSelectorResult(ev.Entity, ev.Position);
    }
}

/// <summary>
/// Result of a selector resolution. Either or both fields may be set.
/// </summary>
public readonly record struct CEGOAPSelectorResult(EntityUid? Entity, EntityCoordinates? Position)
{
    public bool HasResult => Entity != null || Position != null;
}

/// <summary>
/// Broadcast event raised when a GOAP selector is being resolved.
/// The handling system sets <see cref="Entity"/> and/or <see cref="Position"/>.
/// </summary>
[ByRefEvent]
public record struct CEGOAPSelectorResolveEvent<T>(T Selector, EntityUid Agent)
    where T : CEGOAPTargetSelectorBase<T>
{
    public EntityUid? Entity;
    public EntityCoordinates? Position;
}

/// <summary>
/// Base system that auto-subscribes to <see cref="CEGOAPSelectorResolveEvent{TSelector}"/>.
/// </summary>
public abstract partial class CEGOAPTargetSelectorSystem<TSelector> : EntitySystem
    where TSelector : CEGOAPTargetSelectorBase<TSelector>
{
    [Dependency] protected CEMobStateSystem _mobState = default!;
    [Dependency] protected SharedTransformSystem _transform = default!;
    [Dependency] protected EntityQuery<TransformComponent> _xformQuery = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEGOAPSelectorResolveEvent<TSelector>>(OnResolve);
    }

    private void OnResolve(ref CEGOAPSelectorResolveEvent<TSelector> ev)
    {
        Resolve(ref ev);
    }

    protected abstract void Resolve(ref CEGOAPSelectorResolveEvent<TSelector> ev);

    /// <summary>
    /// Returns true if <paramref name="candidate"/> passes all <paramref name="conditions"/>.
    /// Constructs args with <paramref name="agent"/> as Source and <paramref name="candidate"/> as Target.
    /// </summary>
    protected bool PassesConditions(IReadOnlyList<CEEntityCondition> conditions, EntityUid agent, EntityUid candidate)
    {
        if (conditions.Count == 0)
            return true;

        var args = new CEEntityEffectArgs(EntityManager, agent, null, Angle.Zero, 0f, candidate, null);
        foreach (var cond in conditions)
        {
            if (!cond.Passes(args))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Filters <paramref name="candidates"/> to those that are alive and pass all selector conditions.
    /// </summary>
    protected List<EntityUid> GetFilteredEnemies(
        EntityUid agent,
        IEnumerable<EntityUid> candidates,
        IReadOnlyList<CEEntityCondition> conditions)
    {
        var result = new List<EntityUid>();
        foreach (var candidate in candidates)
        {
            if (!_mobState.IsAlive(candidate))
                continue;

            if (!PassesConditions(conditions, agent, candidate))
                continue;

            result.Add(candidate);
        }
        return result;
    }
}
