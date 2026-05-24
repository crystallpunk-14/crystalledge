using Content.Shared._CE.Workbench.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.KnowledgeBook;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CEKnowledgeBookComponent : Component
{
    /// <summary>
    /// Recipes this book teaches when used.
    /// </summary>
    [DataField, AutoNetworkedField]
    public List<ProtoId<CEWorkbenchRecipePrototype>> Recipes = new();

    /// <summary>
    /// How long reading takes (DoAfter delay).
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan UseDelay = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Sound played when reading the book (page turn sound).
    /// </summary>
    [DataField]
    public SoundSpecifier? UseSound;
}
