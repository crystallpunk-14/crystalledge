using System.Linq;
using Content.Shared._CE.Damage;
using Robust.Client.GameObjects;
using Robust.Shared.Utility;

namespace Content.Client._CE.Damage;

/// <summary>
/// Updates per-body-part damage overlays based on the damage fraction
/// received via <see cref="AppearanceSystem"/>.
/// Simplified port of the vanilla <c>DamageVisualsSystem</c> without per-group splitting.
/// </summary>
public sealed class CEDamageVisualsSystem : VisualizerSystem<CEDamageVisualsComponent>
{
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEDamageVisualsComponent, ComponentInit>(OnInit);
    }

    private void OnInit(EntityUid uid, CEDamageVisualsComponent comp, ComponentInit args)
    {
        if (!ValidateSetup(uid, comp))
        {
            comp.Valid = false;
            RemCompDeferred<CEDamageVisualsComponent>(uid);
            return;
        }

        InitializeVisualizer(uid, comp);
    }

    private bool ValidateSetup(EntityUid uid, CEDamageVisualsComponent comp)
    {
        if (comp.Thresholds.Count < 1)
        {
            Log.Error($"CEDamageVisuals: no thresholds defined on {uid}.");
            return false;
        }

        if (comp.TargetLayers is not { Count: > 0 })
        {
            Log.Error($"CEDamageVisuals: no target layers defined on {uid}.");
            return false;
        }

        return true;
    }

    private void InitializeVisualizer(EntityUid uid, CEDamageVisualsComponent comp)
    {
        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        // Add a zeroth threshold and sort.
        if (!comp.Thresholds.Contains(0f))
            comp.Thresholds.Add(0f);

        comp.Thresholds.Sort();

        // Resolve target layers.
        foreach (var key in comp.TargetLayers!)
        {
            if (!SpriteSystem.LayerMapTryGet((uid, sprite), key, out _, false))
            {
                Log.Warning($"CEDamageVisuals: layer {key} invalid on {uid}, skipping.");
                continue;
            }

            comp.TargetLayerMapKeys.Add(key);
        }

        if (comp.TargetLayerMapKeys.Count == 0)
        {
            Log.Error($"CEDamageVisuals: no valid target layers on {uid}.");
            comp.Valid = false;
            return;
        }

        // Create one overlay layer per target layer, placed right above it.
        var firstThreshold = comp.Thresholds.Count > 1 ? comp.Thresholds[1] : comp.Thresholds[0];

        foreach (var layerKey in comp.TargetLayerMapKeys)
        {
            var layerName = layerKey.ToString()!;
            comp.LayerMapKeyStates[layerKey] = layerName;

            var targetIndex = SpriteSystem.LayerMapGet((uid, sprite), layerKey);
            var insertIndex = targetIndex + 1 < sprite.AllLayers.Count()
                ? targetIndex + 1
                : (int?) null;

            var initialState = $"{layerName}_{comp.StatePrefix}_{ThresholdToSuffix(firstThreshold, comp.ThresholdMultiplier)}";
            var mapKey = $"{layerName}_{comp.StatePrefix}";

            var newLayer = SpriteSystem.AddLayer(
                (uid, sprite),
                new SpriteSpecifier.Rsi(new ResPath(comp.Sprite), initialState),
                insertIndex);

            SpriteSystem.LayerMapSet((uid, sprite), mapKey, newLayer);

            if (comp.Color != null)
                SpriteSystem.LayerSetColor((uid, sprite), newLayer, Color.FromHex(comp.Color));

            SpriteSystem.LayerSetVisible((uid, sprite), newLayer, false);
        }
    }

    protected override void OnAppearanceChange(EntityUid uid, CEDamageVisualsComponent comp, ref AppearanceChangeEvent args)
    {
        if (!comp.Valid)
            return;

        if (!AppearanceSystem.TryGetData<float>(uid, CEDamageVisuals.DamageFraction, out var fraction, args.Component))
            return;

        if (!TryComp<SpriteComponent>(uid, out var sprite))
            return;

        fraction = Math.Clamp(fraction, 0f, 1f);

        // Compare fraction directly against fractional thresholds.
        if (!CheckThresholdBoundary(fraction, comp, out var threshold))
            return;

        comp.LastThreshold = threshold;

        foreach (var layerKey in comp.TargetLayerMapKeys)
        {
            UpdateTargetLayer((uid, sprite), comp, layerKey, threshold);
        }
    }

    private bool CheckThresholdBoundary(float scaledDamage, CEDamageVisualsComponent comp, out float threshold)
    {
        threshold = 0f;

        var index = comp.Thresholds.BinarySearch(scaledDamage);
        if (index < 0)
        {
            index = ~index;
            threshold = comp.Thresholds[index - 1];
        }
        else
        {
            threshold = comp.Thresholds[index];
        }

        return !MathHelper.CloseTo(threshold, comp.LastThreshold);
    }

    private static string ThresholdToSuffix(float threshold, int multiplier)
    {
        return ((int) Math.Round(threshold * multiplier)).ToString();
    }

    private void UpdateTargetLayer(Entity<SpriteComponent> ent, CEDamageVisualsComponent comp, Enum layerKey, float threshold)
    {
        var mapKey = $"{comp.LayerMapKeyStates[layerKey]}_{comp.StatePrefix}";

        if (!SpriteSystem.LayerMapTryGet(ent.AsNullable(), mapKey, out var spriteLayer, false))
            return;

        if (MathHelper.CloseTo(threshold, 0f))
        {
            SpriteSystem.LayerSetVisible(ent.AsNullable(), spriteLayer, false);
        }
        else
        {
            SpriteSystem.LayerSetVisible(ent.AsNullable(), spriteLayer, true);

            var stateName = $"{comp.LayerMapKeyStates[layerKey]}_{comp.StatePrefix}_{ThresholdToSuffix(threshold, comp.ThresholdMultiplier)}";
            SpriteSystem.LayerSetRsiState(ent.AsNullable(), spriteLayer, stateName);
        }
    }
}
