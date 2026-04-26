using Robust.Shared.GameStates;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
/// limits the maximum number of souls that can be collected from a single dungeon level.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedSoulSystem))]
public sealed partial class CESoulCapComponent : Component
{
    [DataField, AutoNetworkedField]
    public int Cap = 30;

    [DataField, AutoNetworkedField]
    public int Current;
}
