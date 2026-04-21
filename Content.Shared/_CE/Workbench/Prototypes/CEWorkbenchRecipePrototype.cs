using Content.Shared.Tag;
using Robust.Shared.Audio;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.Workbench.Prototypes;

[Prototype("CERecipe")]
public sealed partial class CEWorkbenchRecipePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public ProtoId<TagPrototype> Tag;

    [DataField]
    public TimeSpan CraftTime = TimeSpan.FromSeconds(1f);

    [DataField]
    public SoundSpecifier? OverrideCraftSound;

    /// <summary>
    /// Mandatory conditions, without which the craft button will not even be active
    /// </summary>
    [DataField(required: true)]
    public List<CEWorkbenchCraftRequirement> Requirements = new();

    [DataField(required: true)]
    public EntProtoId Result;

    [DataField]
    public int ResultCount = 1;

    [DataField]
    public ProtoId<CEWorkbenchRecipeCategoryPrototype>? Category;

    [DataField]
    public int Priority = 0;  // In descending order. More means it will be first.
}
