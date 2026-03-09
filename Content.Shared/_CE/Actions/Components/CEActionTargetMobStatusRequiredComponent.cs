using Content.Shared._CE.Health.Components;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Actions.Components;

/// <summary>
/// Allows you to limit the use of a spell based on the target's alive/dead status
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CEActionTargetMobStatusRequiredComponent : Component
{
    [DataField]
    public HashSet<CEMobState> AllowedStates = [CEMobState.Alive];
}
