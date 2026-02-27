using Robust.Shared.GameStates;

namespace Content.Shared._CE.IdentityRecognition;

/// <summary>
/// Stores all the names of other characters that the player has memorized.
/// These players will be visible to the player under that name, rather than as nameless characters.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CERememberedNamesComponent : Component
{
    /// <summary>
    /// Pair of NetEntity Id and names
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<int, string> Names = [];
}
