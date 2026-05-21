using System.Numerics;
using Content.Shared._CE.GOAP;
using Content.Shared._CE.GOAP.Components;
using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Components;
using Content.Shared.Examine;
using Content.Shared.NPC.Components;
using Content.Shared.NPC.Systems;
using Robust.Shared.Timing;

namespace Content.Server._CE.GOAP.Perceptors;

/// <summary>
/// Vision-based perception. Periodically scans hostile entities in radius with line-of-sight
/// and feeds them into the GOAP knowledge store. Entries persist after the entity leaves sight
/// for up to <see cref="MemoryDuration"/> seconds.
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
    [Dependency] private readonly NpcFactionSystem _faction = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly ExamineSystemShared _examine = default!;
    [Dependency] private readonly CEMobStateSystem _mobState = default!;

    private EntityQuery<TransformComponent> _xformQuery;

    public override void Initialize()
    {
        base.Initialize();
        _xformQuery = GetEntityQuery<TransformComponent>();
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

        var npcWorldPos = _transform.GetWorldPosition(xform);
        Entity<NpcFactionMemberComponent?, FactionExceptionComponent?> factionEnt = (uid, null, null);
        var hostiles = _faction.GetNearbyHostiles(factionEnt, eyes.VisionRadius);

        foreach (var targetUid in hostiles)
        {
            if (!_xformQuery.TryGetComponent(targetUid, out var targetXform))
                continue;

            var targetWorldPos = _transform.GetWorldPosition(targetXform);
            var distance = Vector2.Distance(npcWorldPos, targetWorldPos);
            if (distance > eyes.VisionRadius)
                continue;

            if (!_examine.InRangeUnOccluded(uid, targetUid, eyes.VisionRadius + 0.5f))
                continue;

            if (TryComp<CEMobStateComponent>(targetUid, out var mobState) && !_mobState.IsAlive(targetUid, mobState))
                continue;

            _goap.Remember(
                (uid, goap),
                targetUid,
                targetXform.Coordinates,
                eyes.MemoryDuration);
        }
    }
}
