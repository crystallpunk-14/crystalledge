using Robust.Shared.Serialization;

namespace Content.Client._CE.Damage;

/// <summary>
/// Displays damage overlay sprites on humanoid body parts based on the
/// damage fraction, using <see cref="CEDamageVisualsSystem"/>.
/// Simplified port of the vanilla DamageVisualsComponent without per-group
/// damage tracking.
/// </summary>
[RegisterComponent]
public sealed partial class CEDamageVisualsComponent : Component
{
    /// <summary>
    /// Damage fraction thresholds (0–1) representing percentage of max health.
    /// A zeroth threshold is automatically added. Sorted on init.
    /// Example: [0.1, 0.2, 0.3, 0.5, 0.7, 1.0]
    /// means overlays appear at 10%, 20%, 30%, 50%, 70%, and 100% damage.
    /// </summary>
    [DataField(required: true)]
    public List<float> Thresholds = new();

    /// <summary>
    /// Layers to target by their layer map key (e.g. HumanoidVisualLayers enum values).
    /// One overlay layer is created per target layer.
    /// </summary>
    [DataField]
    public List<Enum>? TargetLayers;

    /// <summary>
    /// RSI path containing damage overlay states.
    /// States should be named <c>{LayerName}_{StatePrefix}_{Suffix}</c>,
    /// where Suffix = round(Threshold x ThresholdMultiplier).
    /// Example: <c>Chest_Brute_3</c> for threshold 0.3 with multiplier 10.
    /// </summary>
    [DataField(required: true)]
    public string Sprite = default!;

    /// <summary>
    /// Optional overlay color (hex).
    /// </summary>
    [DataField]
    public string? Color;

    /// <summary>
    /// Prefix inserted between the layer name and the threshold suffix
    /// in the RSI state name: <c>{LayerName}_{StatePrefix}_{Suffix}</c>.
    /// </summary>
    [DataField]
    public string StatePrefix = "Brute";

    /// <summary>
    /// Multiplier to convert fractional thresholds to RSI state suffixes.
    /// State suffix = round(threshold × this value).
    /// Example: threshold 0.3, multiplier 10 → suffix "3".
    /// </summary>
    [DataField]
    public int ThresholdMultiplier = 10;

    // --- runtime state ---

    [ViewVariables]
    public readonly List<Enum> TargetLayerMapKeys = new();

    [ViewVariables]
    public readonly Dictionary<object, string> LayerMapKeyStates = new();

    [ViewVariables]
    public float LastThreshold;

    [ViewVariables]
    public bool Valid = true;
}
