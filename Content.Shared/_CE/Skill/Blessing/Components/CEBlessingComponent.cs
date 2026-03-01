using System.Numerics;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Skill.Blessing.Components;

/// <summary>
/// An entity above the pedestal that you can interact with to obtain a skill for your character.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true, fieldDeltas: true)]
[Access(typeof(CESharedBlessingSystem))]
public sealed partial class CEBlessingComponent : Component
{
    /// <summary>
    /// What skill will the player gain from interacting with this pedestal?
    /// </summary>
    [DataField, AutoNetworkedField]
    public ProtoId<CESkillPrototype>? Skill;

    /// <summary>
    /// If not null, only the specified entity can receive the skill associated with this blessing.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? ForPlayer;

    /// <summary>
    /// Sprite layer map for visualising skill icon
    /// </summary>
    [DataField]
    public string MapLayer = "blessing";

    /// <summary>
    /// Sprite layer map for visualising skill icon
    /// </summary>
    [DataField]
    public string MapVFXLayer = "vfx";

    /// <summary>
    /// How long it takes to go from the bottom of the animation to the top.
    /// </summary>
    [DataField]
    public float AnimationTime = 2f;

    [DataField]
    public Vector2 FloatingStartOffset = new(0, 0.4f);

    [DataField]
    public Vector2 FloatingOffset = new(0, 0.45f);

    public readonly string AnimationKey = "blessingfloat";

    /// <summary>
    /// Sibling blessing entities spawned alongside this one.
    /// Used for predicted cleanup when one blessing is claimed.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<EntityUid> SiblingBlessings = new();

    /// <summary>
    /// Reference to the statue that spawned this blessing dynamically.
    /// Set by the server when spawning; not networked.
    /// </summary>
    public EntityUid? SourceStatue;
}
