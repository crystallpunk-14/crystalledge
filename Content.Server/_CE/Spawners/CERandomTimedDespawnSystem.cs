using Robust.Shared.Random;
using Robust.Shared.Spawners;

namespace Content.Server._CE.Spawners;

public sealed class CERandomTimedDespawnSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CERandomTimedDespawnComponent, MapInitEvent>(OnRandomTimedDespawnInit);
    }

    private void OnRandomTimedDespawnInit(Entity<CERandomTimedDespawnComponent> ent, ref MapInitEvent args)
    {
        var lifetime = _random.NextFloat(ent.Comp.MinLifetime, ent.Comp.MaxLifetime);

        var timedDespawn = EnsureComp<TimedDespawnComponent>(ent);
        timedDespawn.Lifetime = lifetime;
        Dirty(ent.Owner, timedDespawn);
    }
}

/// <summary>
/// Spawns an entity with a random lifetime before despawn.
/// The lifetime is randomly selected between MinLifetime and MaxLifetime upon initialization.
/// </summary>
[RegisterComponent]
public sealed partial class CERandomTimedDespawnComponent : Component
{
    /// <summary>
    /// Minimum lifetime in seconds before the entity despawns.
    /// </summary>
    [DataField]
    public float MinLifetime = 1f;

    /// <summary>
    /// Maximum lifetime in seconds before the entity despawns.
    /// </summary>
    [DataField]
    public float MaxLifetime = 5f;
}
