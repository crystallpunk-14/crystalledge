using Content.Shared._CE.Animation.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Item.Components;

/// <summary>
/// Replaces attack animations for the item being used if the item is held in both hands (wielded).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CESharedItemAnimationSystem))]
public sealed partial class CEWieldedItemAnimationComponent : Component
{
    /// <summary>
    /// Mapping from input button to attack action prototype.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public Dictionary<CEUseType, List<ProtoId<CEAnimationActionPrototype>>> Animations = new();
}
