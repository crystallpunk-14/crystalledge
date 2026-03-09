using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Skills.Blessing.Components;

/// <summary>
/// The main coordinator component placed on a statue entity.
/// Links to a trigger zone and pedestal tables via EntityLookup on MapInit.
/// Tracks which players have been offered blessings and manages active blessing entities.
/// </summary>
[RegisterComponent]
[Access(typeof(CEBlessingSystem))]
public sealed partial class CEBlessingStatueComponent : Component
{
    /// <summary>
    /// Search radius (in tiles) for linking trigger and tables on initialization.
    /// </summary>
    [DataField]
    public float LinkRadius = 4f;

    /// <summary>
    /// Prototype ID for the blessing entity to spawn on tables.
    /// </summary>
    [DataField]
    public EntProtoId BlessingPrototype = "CEUpgradeBlank";

    /// <summary>
    /// References to linked table (pedestal) entities, found during initialization.
    /// </summary>
    public HashSet<EntityUid> LinkedTables = new();

    /// <summary>
    /// Players who have already claimed a blessing from this statue.
    /// They can no longer use this statue.
    /// </summary>
    [DataField]
    public HashSet<EntityUid> PlayersBlessed = new();

    /// <summary>
    /// The player currently inside the trigger zone receiving blessing options.
    /// </summary>
    public EntityUid? ActivePlayer;

    /// <summary>
    /// Currently spawned blessing entities on the pedestals for the active player.
    /// </summary>
    public List<EntityUid> ActiveBlessings = new();

    /// <summary>
    /// Per-player cache of skill offerings for this statue.
    /// Preserved when a player leaves the trigger zone so re-entry shows the same skills.
    /// Cleared when the player claims a blessing (non-chosen become skipped).
    /// </summary>
    public Dictionary<EntityUid, List<ProtoId<CESkillPrototype>>> OfferedSkills = new();
}
