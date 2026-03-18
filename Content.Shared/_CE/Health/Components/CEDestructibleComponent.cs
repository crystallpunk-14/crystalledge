using Content.Shared.EntityTable.EntitySelectors;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Shared._CE.Health.Components;

/// <summary>
/// Destroys the entity (QueueDel) when accumulated damage reaches the threshold.
/// Works independently from <see cref="CEMobStateComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CEDestructibleComponent : Component
{
    /// <summary>
    /// Damage value at or above which the entity is destroyed.
    /// </summary>
    [DataField(required: true), AutoNetworkedField]
    public int DestroyThreshold;

    [DataField]
    public SoundSpecifier? DestroySound;

    [DataField]
    public EntityTableSelector? Loot;
}
