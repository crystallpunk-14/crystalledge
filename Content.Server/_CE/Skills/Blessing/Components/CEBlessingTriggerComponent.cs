namespace Content.Server._CE.Skills.Blessing.Components;

/// <summary>
/// A sensor zone entity (stone platform with runes) placed near a blessing statue.
/// When a player walks into this zone, the linked statue spawns blessings on pedestals.
/// </summary>
[RegisterComponent]
[Access(typeof(CEBlessingSystem))]
public sealed partial class CEBlessingTriggerComponent : Component
{
    /// <summary>
    /// Reference to the linked statue, set during initialization via EntityLookup.
    /// </summary>
    public EntityUid? LinkedStatue;
}
