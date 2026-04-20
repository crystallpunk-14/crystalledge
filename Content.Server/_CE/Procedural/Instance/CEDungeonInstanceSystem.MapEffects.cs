using Content.Shared._CE.Procedural.Components;
using Content.Shared._CE.Procedural.MapEffects;
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
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;

    /// <summary>
    /// Tracks which status effects are currently applied to each player from map effects.
    /// </summary>
    private readonly Dictionary<EntityUid, List<EntProtoId>> _activeMapEffects = new();

    private void HandleMapEffectsParentChanged(Entity<CEDungeonPlayerComponent> ent, EntParentChangedMessage args)
    {
        // Remove previous map effects.
        RemoveActiveEffects(ent);

        var newMapUid = args.Transform.MapUid;
        if (newMapUid == null)
            return;

        if (!TryComp<CEMapStatusEffectsComponent>(newMapUid.Value, out var mapEffects))
            return;

        // Apply new effects from the destination map.
        var applied = new List<EntProtoId>();
        foreach (var effect in mapEffects.Effects)
        {
            if (_stacks.TryAddStack(ent, effect, out _))
                applied.Add(effect);
        }

        if (applied.Count > 0)
            _activeMapEffects[ent] = applied;
    }

    private void HandleMapEffectsShutdown(EntityUid player)
    {
        RemoveActiveEffects(player);
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
