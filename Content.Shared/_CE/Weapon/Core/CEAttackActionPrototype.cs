using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Weapon.Core;

/// <summary>
/// Modular attack action prototype. Defines how an attack behaves when bound
/// to a button on <see cref="Components.CEWeaponComponent"/>.
/// </summary>
[Prototype("attack")]
public sealed partial class CEAttackActionPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    /// <summary>
    /// The attack mode determining hit detection behavior.
    /// </summary>
    [DataField]
    public CEAttackMode Mode = CEAttackMode.Precise;

    /// <summary>
    /// Base damage dealt by this attack action.
    /// </summary>
    [DataField(required: true)]
    public DamageSpecifier Damage = default!;

    /// <summary>
    /// Attacks per second.
    /// </summary>
    [DataField]
    public float AttackRate = 1f;

    /// <summary>
    /// Maximum reach of this attack in tiles.
    /// </summary>
    [DataField]
    public float Range = 1.5f;

    /// <summary>
    /// Arc width for wide attacks.
    /// </summary>
    [DataField]
    public Angle Angle = Angle.FromDegrees(60);

    /// <summary>
    /// Maximum number of targets for wide attacks.
    /// </summary>
    [DataField]
    public int MaxTargets = 5;

    /// <summary>
    /// Animation entity prototype for this attack.
    /// </summary>
    [DataField]
    public EntProtoId Animation = "WeaponArcSlash";

    /// <summary>
    /// Rotation applied to the animation sprite (e.g. flip/tilt the arc graphic).
    /// </summary>
    [DataField]
    public Angle AnimationRotation = Angle.Zero;

    /// <summary>
    /// Distance of the weapon arc animation from the player.
    /// </summary>
    [DataField]
    public float AnimationOffset = 1f;

    /// <summary>
    /// Sound played on swing (miss).
    /// </summary>
    [DataField]
    public SoundSpecifier SwingSound = new SoundPathSpecifier("/Audio/Weapons/punchmiss.ogg")
    {
        Params = AudioParams.Default.WithVolume(-3f).WithVariation(0.025f),
    };

    /// <summary>
    /// Sound played on successful hit.
    /// </summary>
    [DataField]
    public SoundSpecifier? HitSound;

    /// <summary>
    /// Sound played when hitting but dealing no damage.
    /// </summary>
    [DataField]
    public SoundSpecifier NoDamageSound = new SoundCollectionSpecifier("WeakHit");

    /// <summary>
    /// Whether this attack bypasses damage resistances.
    /// </summary>
    [DataField]
    public bool ResistanceBypass;
}

/// <summary>
/// The mode of an attack action, determining hit detection behavior.
/// </summary>
public enum CEAttackMode : byte
{
    /// <summary>
    /// Single-target click attack. Hits the entity under the cursor.
    /// </summary>
    Precise,

    /// <summary>
    /// Arc-based sweep attack. Hits multiple targets in an arc.
    /// </summary>
    Wide,
}
