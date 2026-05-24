using Content.Server.Popups;
using Content.Server._CE.Workbench;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;

namespace Content.Server._CE.EntityEffect.Effects;

/// <summary>
/// Server-side handler for the <see cref="LearnWorkbenchRecipes"/> CE entity effect.
/// Adds the specified recipes to the target entity's recipe knowledge.
/// </summary>
public sealed partial class CELearnWorkbenchRecipesEffectSystem : CEEntityEffectSystem<LearnWorkbenchRecipes>
{
    [Dependency] private readonly CERecipeKnowledgeSystem _recipeKnowledge = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    protected override void Effect(ref CEEntityEffectEvent<LearnWorkbenchRecipes> args)
    {
        var target = ResolveEffectEntity(args.Args, args.Effect.EffectTarget);
        if (target is not { } entity)
            return;

        var knowledge = EnsureComp<CERecipeKnowledgeComponent>(entity);

        var newlyLearned = 0;
        foreach (var recipe in args.Effect.Recipes)
        {
            if (_recipeKnowledge.TryAddRecipe(entity, recipe, knowledge))
                newlyLearned++;
        }

        if (newlyLearned > 0)
            _popup.PopupEntity(Loc.GetString("ce-recipe-scroll-learned"), entity, entity);
        else
            _popup.PopupEntity(Loc.GetString("ce-recipe-scroll-already-known"), entity, entity);
    }
}
