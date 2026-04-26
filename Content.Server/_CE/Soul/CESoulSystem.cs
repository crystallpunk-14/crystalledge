
using Content.Server._CE.Procedural.Overview;
using Content.Shared._CE.Soul;
using Content.Shared._CE.Soul.Components;

namespace Content.Server._CE.Soul;

public sealed class CESoulSystem : CESharedSoulSystem
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESoulCapComponent, CEDungeonPlayerLevelChangedEvent>(OnLevelChange);
    }

    private void OnLevelChange(Entity<CESoulCapComponent> ent, ref CEDungeonPlayerLevelChangedEvent args)
    {
        ent.Comp.Current = 0;
        Dirty(ent);
    }
}
