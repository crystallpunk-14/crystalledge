using Content.Client.Body;
using Content.Client.DisplacementMap;
using Content.Shared._CE.Body;
using Content.Shared.Body;
using Content.Shared.Body.Components;
using Content.Shared.Humanoid.Markings;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._CE.Body;

/// <summary>
/// Applies displacement maps to marking layers (Hair, FacialHair, etc.) after they are rendered.
/// Works with <see cref="CEMarkingDisplacementComponent"/> on the organ entity.
/// </summary>
public sealed class CEMarkingDisplacementSystem : EntitySystem
{
    [Dependency] private readonly DisplacementMapSystem _displacement = default!;
    [Dependency] private readonly SpriteSystem _sprite = default!;
    [Dependency] private readonly MarkingManager _markingManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEMarkingDisplacementComponent, OrganGotInsertedEvent>(OnInserted, after: [typeof(VisualBodySystem)]);
        SubscribeLocalEvent<CEMarkingDisplacementComponent, OrganGotRemovedEvent>(OnRemoved, after: [typeof(VisualBodySystem)]);
        SubscribeLocalEvent<CEMarkingDisplacementComponent, BodyRelayedEvent<ApplyOrganMarkingsEvent>>(OnMarkingsApplied, after: [typeof(SharedVisualBodySystem)]);
    }

    private void OnInserted(Entity<CEMarkingDisplacementComponent> ent, ref OrganGotInsertedEvent args)
    {
        ApplyDisplacements(ent, args.Target);
    }

    private void OnRemoved(Entity<CEMarkingDisplacementComponent> ent, ref OrganGotRemovedEvent args)
    {
        RemoveDisplacements(ent, args.Target);
    }

    private void OnMarkingsApplied(Entity<CEMarkingDisplacementComponent> ent, ref BodyRelayedEvent<ApplyOrganMarkingsEvent> args)
    {
        if (Comp<OrganComponent>(ent).Body is not { } body)
            return;

        ApplyDisplacements(ent, body);
    }

    private void ApplyDisplacements(Entity<CEMarkingDisplacementComponent> ent, EntityUid body)
    {
        if (!TryComp<SpriteComponent>(body, out var sprite))
            return;

        if (!TryComp<VisualOrganMarkingsComponent>(ent, out var markings))
            return;

        // Clean up previous displacement layers
        RemoveDisplacements(ent, body);

        foreach (var marking in markings.AppliedMarkings)
        {
            if (!_markingManager.TryGetMarking(marking, out var proto))
                continue;

            if (!ent.Comp.Displacements.TryGetValue(proto.BodyPart, out var displacementData))
                continue;

            foreach (var spriteSpec in proto.Sprites)
            {
                DebugTools.Assert(spriteSpec is SpriteSpecifier.Rsi);
                if (spriteSpec is not SpriteSpecifier.Rsi rsi)
                    continue;

                var layerId = $"{proto.ID}-{rsi.RsiState}";

                if (!_sprite.LayerMapTryGet(body, layerId, out var index, false))
                    continue;

                if (_displacement.TryAddDisplacement(displacementData, (body, sprite), index, layerId,
                    out var displacementKey))
                {
                    ent.Comp.TrackedKeys.Add(displacementKey);
                }
            }
        }
    }

    private void RemoveDisplacements(Entity<CEMarkingDisplacementComponent> ent, EntityUid body)
    {
        if (ent.Comp.TrackedKeys.Count == 0)
            return;

        foreach (var key in ent.Comp.TrackedKeys)
        {
            _sprite.RemoveLayer(body, key, false);
        }

        ent.Comp.TrackedKeys.Clear();
    }
}
