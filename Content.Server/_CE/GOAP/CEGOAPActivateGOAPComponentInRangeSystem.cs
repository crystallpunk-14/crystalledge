using Content.Shared._CE.GOAP;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP;

/// <summary>
/// Activates all GOAP for mobs in range.
/// </summary>
public sealed partial class CEGOAPActivateGOAPComponentInRangeSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly CEGOAPSystem _goap = default!;

    public override void Initialize()
    {
        base.Initialize();
    }

    private TimeSpan _checkDelay = TimeSpan.FromSeconds(1.0);
    private TimeSpan _lastCheckTime = TimeSpan.Zero;

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var curTime = _timing.CurTime;
        if (curTime < _lastCheckTime + _checkDelay)
            return;

        var query = EntityQueryEnumerator<CEGOAPActivateGOAPComponentInRangeComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            var mobs = _lookup.GetEntitiesInRange<CEGOAPComponent>(Transform(uid).Coordinates, comp.Range);
            foreach (var mob in mobs)
            {
                if (HasComp<CEActiveGOAPComponent>(mob))
                    continue;

                _goap.UpdateAwakeStatus(mob!);
            }
        }

        _lastCheckTime = _timing.CurTime;
    }
}
