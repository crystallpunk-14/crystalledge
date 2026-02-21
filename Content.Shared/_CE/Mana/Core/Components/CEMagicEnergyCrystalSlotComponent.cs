using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Mana.Core.Components;

/// <summary>
/// Allows an entity to have a slot for inserting magic energy crystals.
/// </summary>
[RegisterComponent, NetworkedComponent]
[Access(typeof(CESharedMagicEnergyCrystalSlotSystem))]
public sealed partial class CEMagicEnergyCrystalSlotComponent : Component
{
    [DataField(required: true)]
    public string SlotId = string.Empty;

    public bool Powered = false;
}

[Serializable, NetSerializable]
public enum CEMagicSlotVisuals : byte
{
    Inserted,
    Powered,
}

/// <summary>
/// Is called when the state of the crystal is changed: it is pulled out, inserted, or the amount of energy in it has changed.
/// </summary>
public sealed class CESlotCrystalChangedEvent(bool ejected) : EntityEventArgs
{
    public readonly bool Ejected = ejected;
}

/// <summary>
/// Is called when the power status of the device changes.
/// </summary>
public sealed class CESlotCrystalPowerChangedEvent(bool powered) : EntityEventArgs
{
    public readonly bool Powered = powered;
}
