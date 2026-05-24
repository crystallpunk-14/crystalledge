using System.Text;
using Content.Server.Chat.Managers;
using Content.Server.Popups;
using Content.Server._CE.Workbench;
using Content.Shared._CE.KnowledgeBook;
using Robust.Server.Audio;
using Robust.Shared.Player;

namespace Content.Server._CE.KnowledgeBook;

public sealed class CEKnowledgeBookSystem : CESharedKnowledgeBookSystem
{
    [Dependency] private readonly CERecipeKnowledgeSystem _recipeKnowledge = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly IChatManager _chat = default!;

    protected override void OnDoAfter(Entity<CEKnowledgeBookComponent> ent, ref CEReadKnowledgeBookDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;

        var target = args.User;
        var knowledge = EnsureComp<CERecipeKnowledgeComponent>(target);

        // Try to learn recipes
        var learnedRecipes = new List<string>();
        foreach (var recipeId in ent.Comp.Recipes)
        {
            if (_recipeKnowledge.TryAddRecipe(target, recipeId, knowledge))
            {
                // Get localized recipe name
                var recipeName = GetRecipeName(recipeId);
                learnedRecipes.Add(recipeName);
            }
        }

        // Check if any recipes were learned
        if (learnedRecipes.Count == 0)
        {
            // All recipes already known - show popup only to this player
            _popup.PopupEntity(Loc.GetString("ce-knowledgebook-already-known"), target, target);
            return;
        }

        // Play page turn sound (if set)
        if (ent.Comp.UseSound != null)
            _audio.PlayPvs(ent.Comp.UseSound, ent);

        // Get player session for global sound and chat
        if (!TryComp<ActorComponent>(target, out var actor))
            return;

        var sb = new StringBuilder();
        sb.Append(Loc.GetString("ce-knowledgebook-learned-header"));
        foreach (var recipeName in learnedRecipes)
        {
            sb.Append($"\n- {recipeName}");
        }

        _chat.DispatchServerMessage(actor.PlayerSession, sb.ToString());

        _popup.PopupEntity(Loc.GetString("ce-recipe-scroll-learned"), target, target);
    }
}
