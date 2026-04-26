using Robust.Shared.GameStates;

namespace Content.Shared._CE.Soul.Components;

/// <summary>
/// Holds the amount of souls collected by a player.
/// Souls are spent on blessings (and similar interactions).
/// </summary>
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
[Access(typeof(CESharedSoulSystem))]
public sealed partial class CESoulContainerComponent : Component
{
    /// <summary>
    /// Current amount of souls in the container.
    /// Always clamped to <c>[0, MaxSouls]</c> by <see cref="CESharedSoulSystem"/>.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int Souls;

    /// <summary>
    /// Maximum number of souls that can be stored.
    /// </summary>
    [DataField, AutoNetworkedField]
    public int MaxSouls = 99;
}
