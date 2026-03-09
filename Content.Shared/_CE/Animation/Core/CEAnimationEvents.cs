using Content.Shared._CE.Animation.Item.Components;

namespace Content.Shared._CE.Animation.Core;

/// <summary>
/// Is called on the object being used to determine what animations it provides
/// </summary>
public sealed class CEGetWeaponEvent(Entity<CEWeaponComponent> used, CEUseType useType) : HandledEntityEventArgs
{
    public Entity<CEWeaponComponent> Used = used;
    public CEUseType UseType = useType;
    public List<CEAnimationEntry> Animations = new();
}
