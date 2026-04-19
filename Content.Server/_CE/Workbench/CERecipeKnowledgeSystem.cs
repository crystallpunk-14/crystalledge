using Content.Shared._CE.Workbench.Prototypes;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Workbench;

/// <summary>
/// Manages the set of workbench recipes known by individual entities (player mobs).
/// Provides a clean API for adding, removing, and querying recipe knowledge.
/// </summary>
public sealed class CERecipeKnowledgeSystem : EntitySystem
{
    /// <summary>
    /// Adds a recipe to the entity's known recipes.
    /// </summary>
    /// <returns>True if the recipe was newly added; false if already known or component missing.</returns>
    public bool TryAddRecipe(EntityUid target,
        ProtoId<CEWorkbenchRecipePrototype> recipe,
        CERecipeKnowledgeComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false;

        var added = component.KnownRecipes.Add(recipe);
        if (added)
            Dirty(target, component);

        return added;
    }

    /// <summary>
    /// Removes a recipe from the entity's known recipes.
    /// </summary>
    /// <returns>True if the recipe was removed; false if not known or component missing.</returns>
    public bool TryRemoveRecipe(EntityUid target,
        ProtoId<CEWorkbenchRecipePrototype> recipe,
        CERecipeKnowledgeComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false;

        var removed = component.KnownRecipes.Remove(recipe);
        if (removed)
            Dirty(target, component);

        return removed;
    }

    /// <summary>
    /// Checks whether the entity knows a specific recipe.
    /// Returns false if the entity has no <see cref="CERecipeKnowledgeComponent"/> (no knowledge = no access).
    /// </summary>
    public bool KnowsRecipe(EntityUid target,
        ProtoId<CEWorkbenchRecipePrototype> recipe,
        CERecipeKnowledgeComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false; // No knowledge component = knows nothing

        return component.KnownRecipes.Contains(recipe);
    }

    /// <summary>
    /// Returns all recipes known by the entity, or null if no knowledge component exists.
    /// </summary>
    public HashSet<ProtoId<CEWorkbenchRecipePrototype>>? GetKnownRecipes(EntityUid target,
        CERecipeKnowledgeComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return null;

        return component.KnownRecipes;
    }
}
