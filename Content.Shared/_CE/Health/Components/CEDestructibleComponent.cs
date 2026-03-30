using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Destroys the entity (QueueDel) when accumulated damage reaches the threshold.
/// For entities with <see cref="CEMobStateComponent"/>, the threshold is counted
/// only after they have entered Critical.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
[Access(typeof(CEDestructibleSystem))]
public sealed partial class CEDestructibleComponent : Component
{
    /// <summary>
    /// Damage value at or above which the entity is destroyed.
    /// For entities with <see cref="CEMobStateComponent"/>, this is the amount of
    /// extra damage they can take after reaching Critical.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public int DestroyThreshold;

    [DataField]
    public SoundSpecifier? DestroySound;

    [DataField]
    public EntityTableSelector? LootTable;
}
