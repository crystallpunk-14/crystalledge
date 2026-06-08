using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.Procedural.MapEffects;
using Content.Shared._CE.StatusEffects.Core;
using Content.Shared._CE.StatusEffectStacks;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Procedural.Instance;

/// <summary>
/// Applies / removes status-effect stacks from <see cref="CEMapStatusEffectsComponent"/>
/// when a <see cref="CEDungeonPlayerComponent"/> enters or leaves a map.
/// Merged into the instance system to avoid duplicate event subscriptions.
/// </summary>
public sealed partial class CEDungeonInstanceSystem
{
    [Dependency] private CEStatusEffectStackSystem _stacks = default!;

    /// <summary>
    /// Tracks which status effects are currently applied to each player from map effects.
    /// </summary>
    private readonly Dictionary<EntityUid, List<EntProtoId>> _activeMapEffects = new();

    private void HandleMapEffectsParentChanged(Entity<CEDungeonPlayerComponent> ent, EntParentChangedMessage args)
    {
        var newMapUid = args.Transform.MapUid;

        // Gather effects required on the new map (null when the map has none).
        List<EntProtoId>? incoming = null;
        if (newMapUid != null && TryComp<CEMapStatusEffectsComponent>(newMapUid.Value, out var mapEffects))
            incoming = mapEffects.Effects;

        // Effects that were active on the previous map.
        _activeMapEffects.TryGetValue(ent, out var outgoing);

        // Remove only the effects that the new map does NOT have.
        // Keeping shared effects alive avoids a remove->add cycle that would silently
        // drop them: TryRemoveStatusEffect uses PredictedQueueDel (deferred), so the
        // effect entity is still returned by TryGetStatusEffect immediately after and
        // TryAddStack adds stacks to a dead entity — the effect then vanishes.
        if (outgoing != null)
        {
            foreach (var effect in outgoing)
            {
                if (incoming == null || !incoming.Contains(effect))
                    _stacks.TryRemoveStack(ent, effect);
            }
            _activeMapEffects.Remove(ent);
        }

        if (incoming == null)
            return;

        // Apply effects from the new map, carrying over those already active.
        var applied = new List<EntProtoId>();
        foreach (var effect in incoming)
        {
            // Effect was already active from the previous map — skip the re-add so we
            // don't touch the live entity at all (see deferred-deletion note above).
            if (outgoing != null && outgoing.Contains(effect))
            {
                applied.Add(effect);
                continue;
            }

            if (_stacks.TryAddStack(ent, effect, out _))
                applied.Add(effect);
        }

        if (applied.Count > 0)
            _activeMapEffects[ent] = applied;
    }

    private void RemoveActiveEffects(EntityUid player)
    {
        if (!_activeMapEffects.TryGetValue(player, out var effects))
            return;

        foreach (var effect in effects)
        {
            _stacks.TryRemoveStack(player, effect);
        }

        _activeMapEffects.Remove(player);
    }
}
