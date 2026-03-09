namespace Content.Server._CE.Skills.Blessing.Components;

/// <summary>
/// Marker component for pedestal entities that display blessings.
/// Found by the statue during initialization via EntityLookup.
/// </summary>
[RegisterComponent]
[Access(typeof(CEBlessingSystem))]
public sealed partial class CEBlessingTableComponent : Component
{
}
