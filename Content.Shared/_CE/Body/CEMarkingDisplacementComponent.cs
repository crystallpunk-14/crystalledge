using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Robust.Shared.GameObjects;

namespace Content.Shared._CE.Body;

/// <summary>
/// Stores displacement maps for marking layers (Hair, FacialHair, etc.).
/// Applied to visual organ entities to shift marking sprites for non-standard body shapes.
/// </summary>
[RegisterComponent]
public sealed partial class CEMarkingDisplacementComponent : Component
{
    /// <summary>
    /// Displacement maps keyed by HumanoidVisualLayers.
    /// </summary>
    [DataField]
    public Dictionary<HumanoidVisualLayers, DisplacementData> Displacements = new();

    /// <summary>
    /// Tracks displacement layer keys added to the body sprite, for cleanup.
    /// </summary>
    [ViewVariables]
    public List<string> TrackedKeys = new();
}
