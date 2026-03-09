using Content.Shared.Whitelist;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class AreaEffect : CEAnimationActionEntry
{
    [DataField(required: true)]
    public List<CEAnimationActionEntry> Effects { get; set; } = new();

    [DataField]
    public EntityWhitelist? Whitelist;

    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// How many entities can be subject to EntityEffect? Leave 0 to remove the restriction.
    /// </summary>
    [DataField]
    public int MaxTargets;

    [DataField(required: true)]
    public float Range = 1f;

    [DataField]
    public bool AffectCaster;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        EntityCoordinates? targetPoint = null;

        if (target is not null &&
            entManager.TryGetComponent<TransformComponent>(target.Value, out var transformComponent))
            targetPoint = transformComponent.Coordinates;
        else if (position is not null)
            targetPoint = position;

        if (targetPoint is null)
            return;

        var lookup = entManager.System<EntityLookupSystem>();
        var whitelist = entManager.System<EntityWhitelistSystem>();

        var entitiesAround = lookup.GetEntitiesInRange(targetPoint.Value, Range, LookupFlags.Uncontained);

        var count = 0;
        foreach (var entity in entitiesAround)
        {
            if (entity == user && !AffectCaster)
                continue;

            if (!whitelist.CheckBoth(entity, Whitelist, Blacklist))
                continue;

            foreach (var effect in Effects)
            {
                effect.Play(entManager, user, used, angle, speed, frame, entity, null);
            }

            count++;

            if (MaxTargets > 0 && count >= MaxTargets)
                break;
        }
    }
}
