using Content.Shared._CE.Tag;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Loot;

/// <summary>
/// Marks an entity as belonging to one or more loot categories with individual spawn weights.
/// Used by trading slots, dungeon loot tables, and any other system that needs tag-based weighted selection.
/// </summary>
[RegisterComponent]
public sealed partial class CELootCategoryComponent : Component
{
    [DataField]
    public List<CELootCategoryEntry> Tags = new();
}

[DataDefinition, Serializable, NetSerializable]
public sealed partial class CELootCategoryEntry
{
    [DataField(required: true)]
    public ProtoId<CETagPrototype> Tag;

    /// <summary>
    /// Relative spawn weight within this category. Higher = more common.
    /// </summary>
    [DataField]
    public float Weight = 1.0f;
}
