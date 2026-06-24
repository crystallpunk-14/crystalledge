using Content.Shared._CE.MeleeWeapon.Components;
using Content.Shared._CE.Stamina;
using Content.Shared.Power.EntitySystems;

namespace Content.Shared._CE.MeleeWeapon;

public abstract partial class CESharedWeaponSystem
{
    [Dependency] private SharedBatterySystem _battery = default!;
    [Dependency] private CEStaminaSystem _stamina = default!;

    private void InitializeCosts()
    {
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUseAttemptEvent>(OnStaminaCostAttempt);
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUsedEvent>(OnStaminaCostUsed);

        SubscribeLocalEvent<CEWeaponManaCostComponent, CEWeaponUseAttemptEvent>(OnManaCostAttempt);
        SubscribeLocalEvent<CEWeaponManaCostComponent, CEWeaponUsedEvent>(OnManaCostUsed);
    }

    private void OnStaminaCostAttempt(Entity<CEWeaponStaminaCostComponent> ent, ref CEWeaponUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0f)
            return;

        if (!_stamina.CanAfford(args.User))
            args.Cancel();
    }

    private void OnStaminaCostUsed(Entity<CEWeaponStaminaCostComponent> ent, ref CEWeaponUsedEvent args)
    {
        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0f)
            return;

        _stamina.TryTakeDamage(args.User, cost);
    }

    private void OnManaCostAttempt(Entity<CEWeaponManaCostComponent> ent, ref CEWeaponUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        if (_battery.GetCharge(ent.Owner) < cost)
            args.Cancel();
    }

    private void OnManaCostUsed(Entity<CEWeaponManaCostComponent> ent, ref CEWeaponUsedEvent args)
    {
        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        _battery.UseCharge(ent.Owner, cost);
    }
}
