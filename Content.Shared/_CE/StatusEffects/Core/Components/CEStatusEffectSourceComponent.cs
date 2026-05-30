using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared._CE.StatusEffects.Core;
using Robust.Shared.Analyzers;
using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.StatusEffectStacks;

/// <summary>
/// Tracks who applied a status effect. Placed on the status effect entity.
/// State is serialized manually to safely handle a deleted source entity.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CEStatusEffectStackSystem), Other = AccessPermissions.None)]
public sealed partial class CEStatusEffectSourceComponent : Component
{
    /// <summary>
    /// Raw backing field. Only accessible to declared friends above.
    /// External code must use <c>CEStatusEffectStackSystem.GetSource()</c> instead.
    /// </summary>
    [DataField]
    public EntityUid? Source;
}

[Serializable, NetSerializable]
public sealed class CEStatusEffectSourceComponentState : ComponentState
{
    public NetEntity? Source;
}
