using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Workbench.Requirements;

public sealed partial class MagicEnergyResource : CEWorkbenchCraftRequirement
{
    [DataField]
    public int Amount = 1;

    [DataField]
    public EntProtoId VFX = "CEEffectManaDrain";

    public override bool CheckRequirement(IEntityManager entManager,
        IPrototypeManager protoManager,
        HashSet<EntityUid> placedEntities,
        EntityUid? user)
    {
        if (user is null)
            return false;

        if (!entManager.TryGetComponent<CEMagicEnergyContainerComponent>(user.Value, out var magicEnergy))
            return false;

        if (magicEnergy.Energy < Amount)
            return false;

        return true;
    }

    public override void PostCraft(IEntityManager entManager,
        IPrototypeManager protoManager,
        HashSet<EntityUid> placedEntities,
        EntityUid? user)
    {
        if (user is null)
            return;

        if (!entManager.TryGetComponent<TransformComponent>(user.Value, out var xform))
            return;

        var magicSys = entManager.System<CESharedMagicEnergySystem>();

        magicSys.Take(user.Value, Amount);
        entManager.SpawnAtPosition(VFX, xform.Coordinates);
    }

    public override double GetPrice(IEntityManager entManager,
        IPrototypeManager protoManager)
    {
        return Amount * 2;
    }

    public override string GetRequirementTitle(IPrototypeManager protoManager)
    {
        return $"{Loc.GetString("ce-magic-manacost")} x{Amount}";
    }

    public override SpriteSpecifier? GetRequirementTexture(IPrototypeManager protoManager)
    {
        return new SpriteSpecifier.Rsi(new ResPath("/Textures/_CE/Interface/mana_icon.rsi"), "icon");
    }
}
