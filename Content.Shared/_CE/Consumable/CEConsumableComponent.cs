using Content.Shared._CE.EntityEffect;
using Content.Shared.Whitelist;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Consumable;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEConsumableComponent : Component
{
    /// <summary>
    /// Effects applied to the target when the item is consumed.
    /// </summary>
    [DataField(required: true)]
    public List<CEEntityEffect> Effects = new();

    /// <summary>
    /// How long the DoAfter takes when using on yourself.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Multiplier applied to <see cref="UseDelay"/> when using on another entity.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float OtherUseDelayMultiplier = 2f;

    /// <summary>
    /// Sound played when the item is consumed.
    /// </summary>
    [DataField]
    public SoundSpecifier? UseSound;

    /// <summary>
    /// If set, spawns this entity at the item's position when the item is consumed or depleted.
    /// For example, an empty vial after drinking a potion.
    /// If null, the item is simply deleted when depleted.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntProtoId? ReplacementEntity;

    /// <summary>
    /// If set, only entities matching this whitelist can be targeted by this consumable.
    /// </summary>
    [DataField]
    public EntityWhitelist Whitelist = new()
    {
        Components = new []
        {
            "CEGOAP",
            "CEMobState",
        }
    };

    /// <summary>
    /// If set, entities matching this blacklist cannot be targeted by this consumable.
    /// </summary>
    [DataField]
    public EntityWhitelist? Blacklist;

    /// <summary>
    /// If false, the item is not deleted after use and can be consumed repeatedly.
    /// Defaults to true (single-use behavior).
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool SingleUse = true;
}
