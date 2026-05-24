namespace Content.Server._CE.Teleportation;

/// <summary>
/// Marks a portal that wants to be auto-linked to another portal carrying the same
/// <see cref="Key"/> at map init time. After a successful pairing, this component
/// is removed from both ends so it never tries to link again.
/// </summary>
[RegisterComponent]
public sealed partial class CEPortalAutoLinkComponent : Component
{
    /// <summary>
    /// Free-form pairing key. Two portals with matching keys (and only two) on the
    /// same map will be linked together when their <c>MapInitEvent</c> fires.
    /// </summary>
    [DataField(required: true)]
    public string Key = string.Empty;
}
