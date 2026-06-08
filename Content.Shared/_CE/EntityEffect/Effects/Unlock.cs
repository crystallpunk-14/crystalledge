using Content.Shared.Doors.Systems;
using Content.Shared.Lock;

namespace Content.Shared._CE.EntityEffect.Effects;

/// <summary>
/// Unlocks a <see cref="Content.Shared.Lock.LockComponent"/> on the target entity.
/// Handled server-side by <c>CEUnlockEffectSystem</c>.
/// </summary>
public sealed partial class Unlock : CEEntityEffectBase<Unlock>
{
}

/// <summary>
/// Server-side handler for the <see cref="Unlock"/> CE entity effect.
/// Unlocks the target entity's <see cref="LockComponent"/> and opens it if it is a door.
/// </summary>
public sealed partial class CEUnlockEffectSystem : CEEntityEffectSystem<Unlock>
{
    [Dependency] private LockSystem _lock = default!;
    [Dependency] private SharedDoorSystem _door = default!;

    protected override void Effect(ref CEEntityEffectEvent<Unlock> args)
    {
        if (ResolveEffectEntity(args.Args, args.Effect.EffectTarget) is not { } target)
            return;

        if (!TryComp<LockComponent>(target, out var lockComp) || !lockComp.Locked)
            return;

        _lock.Unlock(target, null, lockComp);
        _door.TryOpen(target, user: null);

        RemCompDeferred<LockComponent>(target);
    }
}
