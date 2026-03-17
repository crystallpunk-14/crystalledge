using Content.Shared._CE.Health.Components;

namespace Content.Shared._CE.Health;

/// <summary>
/// Destroys entities via QueueDel when accumulated damage reaches <see cref="CEDestructibleComponent.DestroyThreshold"/>.
/// Works independently from <see cref="CEMobStateSystem"/>.
/// </summary>
public sealed class CEDestructibleSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDestructibleComponent, CEDamageChangedEvent>(OnDamageChanged);
    }

    private void OnDamageChanged(Entity<CEDestructibleComponent> ent, ref CEDamageChangedEvent args)
    {
        if (args.NewDamage >= ent.Comp.DestroyThreshold)
            PredictedQueueDel(ent);
    }
}
