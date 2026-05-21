using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Examine;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Perceptors;

/// <summary>
/// Vision-based perception. Periodically scans living mobs in radius with line-of-sight
/// and feeds them into the GOAP knowledge store without any faction filtering — classification
/// (friend/foe/etc.) is the responsibility of higher layers. Entries persist after the entity
/// leaves sight for up to <see cref="MemoryDuration"/> seconds.
/// </summary>
[RegisterComponent]
public sealed partial class CEGOAPEyesPerceptorComponent : Component
{
    /// <summary>
    /// Detection range in tiles.
    /// </summary>
    [DataField]
    public float VisionRadius = 10f;

    /// <summary>
    /// How often the perceptor re-scans.
    /// </summary>
    [DataField]
    public TimeSpan UpdateInterval = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// How long a sighted entity stays in knowledge after the last sighting.
    /// Each new sighting refreshes the expiry.
    /// </summary>
    [DataField]
    public TimeSpan MemoryDuration = TimeSpan.FromSeconds(60);

    [ViewVariables]
    public TimeSpan NextUpdateTime;
}

public sealed class CEGOAPEyesPerceptorSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly CEGOAPSystem _goap = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    [Dependency] private readonly EntityQuery<TransformComponent> _xformQuery = default!;

    private readonly HashSet<Entity<CEMobStateComponent>> _nearbyBuffer = new();

    public override void Initialize()
    {
        base.Initialize();
    }

    public override void Update(float frameTime)
    {
        var curTime = _timing.CurTime;
        var query = EntityQueryEnumerator<CEGOAPEyesPerceptorComponent, CEGOAPComponent, CEActiveGOAPComponent>();
        while (query.MoveNext(out var uid, out var eyes, out var goap, out _))
        {
            if (curTime < eyes.NextUpdateTime)
                continue;

            eyes.NextUpdateTime = curTime + eyes.UpdateInterval;
            Scan((uid, eyes, goap));
        }
    }

    private void Scan(Entity<CEGOAPEyesPerceptorComponent, CEGOAPComponent> ent)
    {
        var (uid, eyes, goap) = ent;

        if (!_xformQuery.TryGetComponent(uid, out var xform))
            return;

        var selfWorldPos = _transform.GetWorldPosition(xform);

        _nearbyBuffer.Clear();
        _lookup.GetEntitiesInRange(xform.Coordinates, eyes.VisionRadius, _nearbyBuffer);

        foreach (var target in _nearbyBuffer)
        {
            var targetUid = target.Owner;
            if (targetUid == uid)
                continue;

            if (!_mobState.IsAlive(targetUid, target.Comp))
                continue;

            if (!_xformQuery.TryGetComponent(targetUid, out var targetXform))
                continue;

            var targetWorldPos = _transform.GetWorldPosition(targetXform);
            if (Vector2.Distance(selfWorldPos, targetWorldPos) > eyes.VisionRadius)
                continue;

            if (!_examine.InRangeUnOccluded(uid, targetUid, eyes.VisionRadius + 0.5f))
                continue;

            _goap.Remember(
                (uid, goap),
                targetUid,
                targetXform.Coordinates,
                eyes.MemoryDuration);
        }
    }
}
