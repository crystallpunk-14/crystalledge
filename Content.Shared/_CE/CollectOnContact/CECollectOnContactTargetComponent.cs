using Robust.Shared.GameStates;

namespace Content.Shared._CE.CollectOnContact;

/// <summary>
/// Marks a storage entity as an auto-collect target for <see cref="CECollectOnContactComponent"/>.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CECollectOnContactTargetComponent : Component;
