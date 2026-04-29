namespace Content.Shared._CE.TileEffects;

public sealed partial class CETileEffectSystem
{
    private void InitializePreventTileEffect()
    {
        SubscribeLocalEvent<CEPreventTileEffectComponent, CEAttemptSpawnTileEffectEvent>(OnPreventTileEffect);
    }

    private void OnPreventTileEffect(Entity<CEPreventTileEffectComponent> ent, ref CEAttemptSpawnTileEffectEvent args)
    {
        if (args.Cancelled)
            return;

        // Empty list = block all tile effects.
        if (ent.Comp.Blocks.Count == 0)
        {
            args.Cancelled = true;
            return;
        }

        foreach (var blocked in ent.Comp.Blocks)
        {
            if (blocked.Id == args.TileEffect.Id)
            {
                args.Cancelled = true;
                return;
            }
        }
    }
}
