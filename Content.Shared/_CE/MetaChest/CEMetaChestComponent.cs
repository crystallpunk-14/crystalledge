using Robust.Shared.GameObjects;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared._CE.MetaChest;

/// <summary>
/// Marks an entity as a meta chest — an interaction point that opens a player's persistent storage.
/// The real inventory is stored in the database; a shadow entity is spawned per player on open.
/// </summary>
[RegisterComponent]
public sealed partial class CEMetaChestComponent : Component
{
    /// <summary>
    /// Which storage bank this chest maps to. Different values give players independent persistent storages.
    /// </summary>
    [DataField]
    public int ChestSlot = 0;
}
