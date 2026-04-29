using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TileEffects;

public sealed partial class CETileEffectSystem
{
    private void InitializeTransform()
    {
        SubscribeLocalEvent<CETileEffectTransformComponent, CEAffectedByTileEffectEvent>(OnTransformAffected);
    }

    private void OnTransformAffected(Entity<CETileEffectTransformComponent> ent, ref CEAffectedByTileEffectEvent args)
    {
        if (_net.IsClient)
            return;

        // Look up the triggering tile effect prototype in our transforms dict.
        var triggerProto = MetaData(args.TileEffect.Owner).EntityPrototype;
        if (triggerProto == null)
            return;

        var triggerId = new EntProtoId(triggerProto.ID);
        if (!ent.Comp.Transforms.TryGetValue(triggerId, out var spawnProto))
            return;

        var xform = Transform(ent);
        var rotation = xform.LocalRotation;
        var coords = _transform.GetMapCoordinates(ent, xform);

        Del(ent.Owner);

        var spawned = Spawn(spawnProto, coords);
        _transform.SetLocalRotation(spawned, rotation);

        if (ent.Comp.DeleteSourceTileEffect)
        {
            QueueDel(args.TileEffect);
        }
    }
}
