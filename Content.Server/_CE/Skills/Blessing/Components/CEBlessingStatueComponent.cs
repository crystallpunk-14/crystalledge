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
    [DataField]
    public HashSet<EntityUid> LinkedTables = new();

    /// <summary>
    /// The player currently interacting with the statue (offer pending). While non-null
    /// the statue is "occupied" — other players cannot click it.
    /// </summary>
    [DataField]
    public EntityUid? ActivePlayer;

    /// <summary>
    /// Currently spawned blessing entities on the pedestals for the active player.
    /// </summary>
    [DataField]
    public List<EntityUid> ActiveBlessings = new();

    /// <summary>
    /// Per-player cache of skill offerings for this statue.
    /// Preserved when a player leaves the trigger zone so re-entry shows the same skills.
    /// Cleared when the player claims a blessing (non-chosen become skipped).
    /// </summary>
    public Dictionary<EntityUid, List<ProtoId<CESkillPrototype>>> OfferedSkills = new();

    [DataField]
    public bool StatueInitialized = false;

    [DataField]
    public string TriggerFixtureId = "trigger";
}
