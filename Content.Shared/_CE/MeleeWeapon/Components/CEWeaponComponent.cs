using Content.Shared._CE.Animation.Core.Prototypes;
using Content.Shared._CE.Health;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Animation.Item.Components;

/// <summary>
/// Using this item in combat mode triggers action animations on the character.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(fieldDeltas: true)]
[Access(typeof(CESharedWeaponSystem))]
public sealed partial class CEWeaponComponent : Component
{
    /// <summary>
    /// Mapping from input button to attack action prototype.
    /// </summary>
    [DataField, AutoNetworkedField]
    public Dictionary<CEUseType, List<CEAnimationEntry>> Animations = new();

    /// <summary>
    ///
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public CEDamageSpecifier Damage = new();

    /// <summary>
    /// Are we currently holding down the mouse for an attack.
    /// Used so we can't just hold the mouse button and attack constantly.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Using;

    /// <summary>
    /// Extra time after the animation ends before the combo resets.
    /// </summary>
    [DataField]
    public TimeSpan ComboResetDelay = TimeSpan.FromSeconds(0.5);

    /// <summary>
    /// Which use type the current combo chain belongs to.
    /// Switching to a different use type resets the combo.
    /// </summary>
    [DataField, AutoNetworkedField]
    public CEUseType? LastComboUseType;

    /// <summary>
    /// Next animation index to play in the current combo chain.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int ComboIndex;

    /// <summary>
    /// Absolute time after which the combo resets to the first animation.
    /// Calculated as: attack time + animation duration + <see cref="ComboResetDelay"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan ComboResetDeadline = TimeSpan.Zero;

    /// <summary>
    /// Initial rotation to apply to the sprite (in degrees), added to the animation angle.
    /// This is used as the starting rotation if no rotation animation is specified.
    /// </summary>
    [DataField]
    public float SpriteRotation;

    /// <summary>
    /// Used only for NPC to calculate minimun required distance to attack
    /// </summary>
    [DataField]
    public float NPCAttackRange = 2f;

    /// <summary>
    /// The sound played on a weapon when it hits something.
    /// </summary>
    [DataField]
    public SoundSpecifier HitSound = new SoundCollectionSpecifier("WeakHit");

    /// <summary>
    /// Modify weapon attack animations range
    /// </summary>
    [DataField]
    public float RangeMultiplier = 1f;
}

[DataDefinition, Serializable]
public sealed partial class CEAnimationEntry
{
    [DataField(required: true)]
    public ProtoId<CEAnimationActionPrototype> Anim;

    /// <summary>
    /// animation playback speed modifier
    /// </summary>
    [DataField]
    public float Speed = 1f;
}

/// <summary>
/// Which input button binding triggers the attack.
/// </summary>
public enum CEUseType : byte
{
    Primary,
    Secondary,
}
