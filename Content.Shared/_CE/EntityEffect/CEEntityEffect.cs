using JetBrains.Annotations;
using Robust.Shared.Map;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.EntityEffect;

/// <summary>
/// Data-only base class for CE entity effects.
/// Logic is handled by systems subscribing to <see cref="CEEntityEffectEvent{T}"/>.
/// </summary>
[ImplicitDataDefinitionForInheritors]
[MeansImplicitUse]
public abstract partial class CEEntityEffect
{
    /// <summary>
    /// Dispatches this effect by raising a typed broadcast event through the event bus.
    /// </summary>
    public abstract void Effect(CEEntityEffectArgs args);
}

/// <summary>
/// Generic base that provides automatic event dispatch for concrete effect types.
/// Each concrete effect should inherit from this instead of <see cref="CEEntityEffect"/> directly.
/// </summary>
public abstract partial class CEEntityEffectBase<T> : CEEntityEffect where T : CEEntityEffectBase<T>
{
    public override void Effect(CEEntityEffectArgs args)
    {
        if (this is not T typed)
            return;

        var ev = new CEEntityEffectEvent<T>(typed, args);
        args.EntityManager.EventBus.RaiseEvent(EventSource.Local, ref ev);
    }
}

/// <summary>
/// Context passed to effects when they are triggered.
/// </summary>
public record struct CEEntityEffectArgs(
    IEntityManager EntityManager,
    EntityUid User,
    EntityUid? Used,
    Angle Angle,
    float Speed,
    EntityUid? Target,
    EntityCoordinates? Position);

/// <summary>
/// Broadcast event raised when a CE entity effect is dispatched.
/// Systems subscribe to this for their specific effect type.
/// </summary>
[ByRefEvent]
public record struct CEEntityEffectEvent<T>(T Effect, CEEntityEffectArgs Args) where T : CEEntityEffectBase<T>;

/// <summary>
/// Abstract base system for handling CE entity effects.
/// Concrete systems inherit this and implement <see cref="Effect"/>.
/// </summary>
public abstract partial class CEEntityEffectSystem<TEffect> : EntitySystem where TEffect : CEEntityEffectBase<TEffect>
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEEntityEffectEvent<TEffect>>(OnEffect);
    }

    private void OnEffect(ref CEEntityEffectEvent<TEffect> args)
    {
        Effect(ref args);
    }

    protected abstract void Effect(ref CEEntityEffectEvent<TEffect> args);

    /// <summary>
    /// Attempts to resolve a target position from the effect args.
    /// Prefers the Target entity's coordinates; falls back to Position.
    /// </summary>
    protected bool TryResolveTargetCoordinates(CEEntityEffectArgs args, out EntityCoordinates targetPoint)
    {
        if (args.Target is not null)
        {
            targetPoint = Transform(args.Target.Value).Coordinates;
            return true;
        }

        if (args.Position is not null)
        {
            targetPoint = args.Position.Value;
            return true;
        }

        targetPoint = default;
        return false;
    }
}
