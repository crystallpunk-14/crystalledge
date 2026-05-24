using Content.Shared._CE.Workbench.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.Workbench;

[Serializable, NetSerializable]
public enum CEWorkbenchUiKey
{
    Key,
}

[Serializable, NetSerializable]
public sealed class CEWorkbenchUiClickRecipeMessage(ProtoId<CEWorkbenchRecipePrototype> recipe)
    : BoundUserInterfaceMessage
{
    public readonly ProtoId<CEWorkbenchRecipePrototype> Recipe = recipe;
}

[Serializable, NetSerializable]
public sealed class CEWorkbenchUiRecipesState(List<CEWorkbenchUiRecipesEntry> recipes, ProtoId<CEWorkbenchRecipePrototype>? selectedRecipe) : BoundUserInterfaceState
{
    public readonly ProtoId<CEWorkbenchRecipePrototype>? SelectedRecipe = selectedRecipe;
    public readonly List<CEWorkbenchUiRecipesEntry> Recipes = recipes;
}

[Serializable, NetSerializable]
public readonly struct CEWorkbenchUiRecipesEntry(ProtoId<CEWorkbenchRecipePrototype> protoId, bool craftable)
    : IEquatable<CEWorkbenchUiRecipesEntry>
{
    public readonly ProtoId<CEWorkbenchRecipePrototype> ProtoId = protoId;
    public readonly bool Craftable = craftable;

    public override bool Equals(object? obj)
    {
        return obj is CEWorkbenchUiRecipesEntry other && Equals(other);
    }

    public bool Equals(CEWorkbenchUiRecipesEntry other)
    {
        return ProtoId.Id == other.ProtoId.Id && Craftable == other.Craftable;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(ProtoId, Craftable);
    }

    public override string ToString()
    {
        return $"{ProtoId} ({Craftable})";
    }
}
