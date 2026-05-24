using Content.Shared._CE.Health.Components;
using Content.Shared.Throwing;

namespace Content.Shared._CE.Health;

/// <summary>
/// Applies CE damage when an entity with <see cref="CEDamageOnLandComponent"/> lands after being thrown.
/// This triggers <see cref="CEDamageChangedEvent"/> which can then trigger <see cref="CEDestructibleSystem"/>.
/// </summary>
public sealed class CEDamageOnLandSystem : EntitySystem
{
    [Dependency] private readonly CESharedDamageableSystem _damage = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEDamageOnLandComponent, LandEvent>(OnLand);
    }

    private void OnLand(Entity<CEDamageOnLandComponent> ent, ref LandEvent args)
    {
        _damage.TakeDamage(ent.Owner, ent.Comp.Damage, args.User);
    }
}
