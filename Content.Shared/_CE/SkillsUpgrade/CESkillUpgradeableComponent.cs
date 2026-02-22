using Content.Shared._CE.Skills;
using Content.Shared._CE.Skills.Prototypes;
using Content.Shared.Alert;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.SkillsUpgrade;

/// <summary>
/// Component that stores the skills learned by a player and their progress in the skill trees.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(CESharedSkillUpgradeableSystem))]
public sealed partial class CESkillUpgradeableComponent : Component
{
    /// <summary>
    /// All possible skills that can be randomly dropped as upgrades for the character.
    /// Cached when the component is initialized.
    /// When the next set is selected for CurrentUpgradeSelection, all selected ones are removed from here.
    /// </summary>
    [DataField(serverOnly: true)]
    public List<ProtoId<CESkillPrototype>> PossibleSkills = new();

    /// <summary>
    /// Current skills that can be selected to upgrade your character.
    /// </summary>
    [DataField, AutoNetworkedField]
    public HashSet<ProtoId<CESkillPrototype>> CurrentUpgradeSelection = new();

    [DataField, AutoNetworkedField]
    public int MaxUpgradeSelection = 3;

    /// <summary>
    /// The current level of the character. Increases when the player actually selects a skill upgrade.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Level = 1;

    /// <summary>
    /// Number of pending level ups that the player has not yet used.
    /// Stacks when multiple level ups are triggered before the player selects upgrades.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int PendingLevels;

    [DataField]
    public ProtoId<AlertPrototype> UpgradeAlert = "CEUpgradeSkillAlert";
}
