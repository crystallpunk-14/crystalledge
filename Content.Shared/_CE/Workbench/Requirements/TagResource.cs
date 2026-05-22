using Content.Shared._CE.Economy;
using Content.Shared._CE.Tag;
using Content.Shared.Stacks;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Workbench.Requirements;

public sealed partial class TagResource : CEWorkbenchCraftRequirement
{
    [DataField(required: true)]
    public ProtoId<CETagPrototype> Tag;

    [DataField]
    public int Count = 1;

    public override bool CheckRequirement(IEntityManager entManager, IPrototypeManager protoManager, HashSet<EntityUid> placedEntities, EntityUid? user)
    {
        var tagSys = entManager.System<CETagSystem>();

        var count = 0;
        foreach (var ent in placedEntities)
        {
            if (!tagSys.HasTag(ent, Tag))
                continue;

            count += 1;
        }

        if (count < Count)
            return false;

        return true;
    }

    public override void PostCraft(IEntityManager entManager, IPrototypeManager protoManager, HashSet<EntityUid> placedEntities, EntityUid? user)
    {
        var stackSystem = entManager.System<SharedStackSystem>();
        var tagSys = entManager.System<CETagSystem>();

        var requiredCount = Count;
        foreach (var ent in placedEntities)
        {
            if (requiredCount <= 0)
                break;

            if (!tagSys.HasTag(ent, Tag))
                continue;

            requiredCount--;
            entManager.DeleteEntity(ent);
        }
    }

    public override double GetPrice(IEntityManager entManager,
        IPrototypeManager protoManager)
    {
        return 0; //TODO
    }

    public override string GetRequirementTitle(IPrototypeManager protoManager)
    {
        if (!protoManager.TryIndex(Tag, out var indexedTag))
            return "Error tag";

        return $"{Loc.GetString(indexedTag.Name)} x{Count}";
    }

    public override SpriteSpecifier? GetRequirementTexture(IPrototypeManager protoManager)
    {
        return !protoManager.TryIndex(Tag, out var indexedTag) ? null : indexedTag.Icon;
    }
}
