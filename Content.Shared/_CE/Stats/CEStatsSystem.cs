namespace Content.Shared._CE.Stats;

public sealed partial class CEStatsSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();

        InitVitality();
    }
}
