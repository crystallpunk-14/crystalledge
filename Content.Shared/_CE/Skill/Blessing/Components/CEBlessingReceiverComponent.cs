using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Blessing.Components;

/// <summary>
/// The component allows entity to receive blessings from statues.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(CESharedBlessingSystem))]
public sealed partial class CEBlessingReceiverComponent : Component
{
    /// <summary>
    /// Which skills were skipped and not selected when offered to the player?
    /// These skills will no longer be offered to the player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<CESkillPrototype>> SkippedSkills = new();
}
