using Content.Server._CE.Workbench.Components;
using Content.Shared.Placeable;
using Content.Shared.UserInterface;
using Robust.Shared.Containers;

namespace Content.Server._CE.Workbench;

public sealed partial class CEWorkbenchSystem
{
    private void InitProviders()
    {
        SubscribeLocalEvent<CEWorkbenchPlaceableProviderComponent, CEWorkbenchGetResourcesEvent>(OnGetPlaceableResource);
        SubscribeLocalEvent<CEWorkbenchPlaceableProviderComponent, ItemPlacedEvent>(OnItemPlaced);
        SubscribeLocalEvent<CEWorkbenchPlaceableProviderComponent, ItemRemovedEvent>(OnItemRemoved);

        SubscribeLocalEvent<CEWorkbenchContainerProviderComponent, CEWorkbenchGetResourcesEvent>(OnGetContainerResource);
        SubscribeLocalEvent<CEWorkbenchContainerProviderComponent, EntInsertedIntoContainerMessage>(OnInsertedToContainer);
        SubscribeLocalEvent<CEWorkbenchContainerProviderComponent, EntRemovedFromContainerMessage>(OnRemovedFromContainer);

        SubscribeLocalEvent<CEWorkbenchUserContainersProviderComponent, CEWorkbenchGetResourcesEvent>(OnGetUserContainersResource);
    }

    private void OnGetPlaceableResource(Entity<CEWorkbenchPlaceableProviderComponent> ent, ref CEWorkbenchGetResourcesEvent args)
    {
        if (!TryComp<ItemPlacerComponent>(ent, out var placer))
            return;

        args.AddResources(placer.PlacedEntities);
    }

    private void OnItemRemoved(Entity<CEWorkbenchPlaceableProviderComponent> ent, ref ItemRemovedEvent args)
    {
        UpdateUIRecipes(ent.Owner);
    }

    private void OnItemPlaced(Entity<CEWorkbenchPlaceableProviderComponent> ent, ref ItemPlacedEvent args)
    {
        UpdateUIRecipes(ent.Owner);
    }


    private void OnGetContainerResource(Entity<CEWorkbenchContainerProviderComponent> ent, ref CEWorkbenchGetResourcesEvent args)
    {
        if (!_container.TryGetContainer(ent, ent.Comp.ContainerName, out var container))
            return;

        args.AddResources(container.ContainedEntities);
    }

    private void OnInsertedToContainer(Entity<CEWorkbenchContainerProviderComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        UpdateUIRecipes(ent.Owner);
    }

    private void OnRemovedFromContainer(Entity<CEWorkbenchContainerProviderComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        UpdateUIRecipes(ent.Owner);
    }

    private void OnGetUserContainersResource(Entity<CEWorkbenchUserContainersProviderComponent> ent, ref CEWorkbenchGetResourcesEvent args)
    {
        if (!TryComp<CEWorkbenchComponent>(ent, out var workbench))
            return;

        if (workbench.CurrentUser is not { } user || TerminatingOrDeleted(user))
            return;

        var containerStack = new Stack<ContainerManagerComponent>();

        // Add items held in hands
        foreach (var held in _hands.EnumerateHeld(user))
        {
            args.AddResource(held);
            if (_containerQuery.TryGetComponent(held, out var cm))
                containerStack.Push(cm);
        }

        // Add items equipped in inventory slots (clothing, pockets, etc.)
        if (_inventory.TryGetSlots(user, out var slots))
        {
            foreach (var slot in slots)
            {
                if (!_inventory.TryGetSlotEntity(user, slot.Name, out var slotEnt))
                    continue;

                args.AddResource(slotEnt.Value);
                if (_containerQuery.TryGetComponent(slotEnt.Value, out var cm))
                    containerStack.Push(cm);
            }
        }

        // Recursively scan all containers
        while (containerStack.TryPop(out var manager))
        {
            foreach (var container in manager.Containers.Values)
            {
                foreach (var entity in container.ContainedEntities)
                {
                    args.AddResource(entity);
                    if (_containerQuery.TryGetComponent(entity, out var cm))
                        containerStack.Push(cm);
                }
            }
        }
    }
}

public sealed class CEWorkbenchGetResourcesEvent : EntityEventArgs
{
    public HashSet<EntityUid> Resources { get; private set; } = new();

    public void AddResource(EntityUid resource)
    {
        Resources.Add(resource);
    }

    public void AddResources(IEnumerable<EntityUid> resources)
    {
        foreach (var resource in resources)
        {
            Resources.Add(resource);
        }
    }
}
