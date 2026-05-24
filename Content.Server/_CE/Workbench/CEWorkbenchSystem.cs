using System.Numerics;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Stack;
using Content.Shared._CE.Workbench;
using Content.Shared._CE.Workbench.Prototypes;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Inventory;
using Content.Shared.UserInterface;
using Robust.Server.Audio;
using Robust.Server.Containers;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Workbench;

public sealed partial class CEWorkbenchSystem : EntitySystem
{
    [Dependency] private readonly AudioSystem _audio = default!;
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly UserInterfaceSystem _userInterface = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly ContainerSystem _container = default!;
    [Dependency] private readonly PhysicsSystem _physics = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly CERecipeKnowledgeSystem _recipeKnowledge = default!;

    private EntityQuery<CEWorkbenchComponent> _workbenchQuery;
    private EntityQuery<ContainerManagerComponent> _containerQuery;

    public override void Initialize()
    {
        base.Initialize();
        InitProviders();
        InitUserCrafter();

        _workbenchQuery = GetEntityQuery<CEWorkbenchComponent>();
        _containerQuery = GetEntityQuery<ContainerManagerComponent>();

        SubscribeLocalEvent<CEWorkbenchComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CEWorkbenchComponent, BeforeActivatableUIOpenEvent>(OnBeforeUIOpen);
        SubscribeLocalEvent<CEWorkbenchComponent, CEWorkbenchUiClickRecipeMessage>(OnSetRecipe);
    }

    private void OnMapInit(Entity<CEWorkbenchComponent> ent, ref MapInitEvent args)
    {
        foreach (var recipe in _proto.EnumeratePrototypes<CEWorkbenchRecipePrototype>())
        {
            if (ent.Comp.Recipes.Contains(recipe.ID))
                continue;

            if (!ent.Comp.RecipeTags.Contains(recipe.Tag))
                continue;

            ent.Comp.Recipes.Add(recipe.ID);
        }
    }

    private void OnBeforeUIOpen(Entity<CEWorkbenchComponent> ent, ref BeforeActivatableUIOpenEvent args)
    {
        ent.Comp.CurrentUser = args.User;
        UpdateUIRecipes((ent, ent.Comp));
    }

    private void OnSetRecipe(Entity<CEWorkbenchComponent> ent, ref CEWorkbenchUiClickRecipeMessage args)
    {
        if (!ent.Comp.Recipes.Contains(args.Recipe))
            return;

        ent.Comp.SelectedRecipe = args.Recipe;
        UpdateUIRecipes((ent, ent.Comp));
    }

    private void UpdateUIRecipes(Entity<CEWorkbenchComponent?> entity)
    {
        if (!Resolve(entity, ref entity.Comp, false))
            return;

        var getResource = new CEWorkbenchGetResourcesEvent();
        RaiseLocalEvent(entity, getResource);

        var resources = getResource.Resources;


        var recipes = new List<CEWorkbenchUiRecipesEntry>();
        foreach (var recipeId in entity.Comp.Recipes)
        {
            if (!_proto.Resolve(recipeId, out var indexedRecipe))
                continue;

            // Only show recipes the current user knows (if they have knowledge tracking),
            // unless the recipe is marked as roundstart (available without learning).
            if (entity.Comp.CurrentUser is null)
                continue;

            if (!indexedRecipe.RoundStart && !_recipeKnowledge.KnowsRecipe(entity.Comp.CurrentUser.Value, recipeId))
                continue;

            var canCraft = true;

            foreach (var requirement in indexedRecipe.Requirements)
            {
                if (!requirement.CheckRequirement(EntityManager, _proto, resources, entity.Comp.CurrentUser.Value))
                {
                    canCraft = false;
                    break;
                }
            }

            var entry = new CEWorkbenchUiRecipesEntry(recipeId, canCraft);

            recipes.Add(entry);
        }

        _userInterface.SetUiState(entity.Owner, CEWorkbenchUiKey.Key, new CEWorkbenchUiRecipesState(recipes, entity.Comp.SelectedRecipe));
    }

    private bool CanCraftRecipe(CEWorkbenchRecipePrototype recipe, HashSet<EntityUid> entities, EntityUid? user = null)
    {
        // Validate the user knows the recipe (server-side), unless it is roundstart.
        if (!recipe.RoundStart && user is { } u && !_recipeKnowledge.KnowsRecipe(u, recipe.ID))
            return false;

        foreach (var req in recipe.Requirements)
        {
            if (!req.CheckRequirement(EntityManager, _proto, entities, user))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Consumes resources required for crafting.
    /// </summary>
    private void ConsumeRecipeResources(CEWorkbenchRecipePrototype recipe, HashSet<EntityUid> resources, EntityUid? user)
    {
        foreach (var req in recipe.Requirements)
        {
            req.PostCraft(EntityManager, _proto, resources, user);
        }
    }

    /// <summary>
    /// Spawns the craft result and places it near the workbench.
    /// </summary>
    private void SpawnRecipeResult(CEWorkbenchRecipePrototype recipe, EntityUid workbench)
    {
        var resultEntities = new HashSet<EntityUid>();
        for (var i = 0; i < recipe.ResultCount; i++)
        {
            var resultEntity = Spawn(recipe.Result);
            resultEntities.Add(resultEntity);
        }

        // Teleport result to workbench AFTER crafting
        foreach (var resultEntity in resultEntities)
        {
            _transform.SetCoordinates(resultEntity, Transform(workbench).Coordinates.Offset(new Vector2(_random.NextFloat(-0.25f, 0.25f), _random.NextFloat(-0.25f, 0.25f))));
            _stack.TryMergeToContacts(resultEntity);
            _physics.WakeBody(resultEntity);
        }
    }
}
