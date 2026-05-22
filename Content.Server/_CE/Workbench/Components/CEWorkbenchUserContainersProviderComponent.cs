namespace Content.Server._CE.Workbench.Components;

/// <summary>
/// Provides resources to the workbench from the inventory of the player currently using it.
/// Includes items in hands, equipped clothing/pocket slots, and recursively scans containers.
/// </summary>
[RegisterComponent]
[Access(typeof(CEWorkbenchSystem))]
public sealed partial class CEWorkbenchUserContainersProviderComponent : Component
{
}
