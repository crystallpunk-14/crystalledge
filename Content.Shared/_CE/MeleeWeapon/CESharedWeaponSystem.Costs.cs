using Content.Shared._CE.Charges;
using Content.Shared._CE.MeleeWeapon.Components;
using Content.Shared._CE.Stamina;

namespace Content.Shared._CE.MeleeWeapon;

public abstract partial class CESharedWeaponSystem
{
    [Dependency] private readonly CEChargesSystem _charges = default!;
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    private void InitializeCosts()
    {
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUseAttemptEvent>(OnStaminaCostAttempt);
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUsedEvent>(OnStaminaCostUsed);

        SubscribeLocalEvent<CEWeaponChargesCostComponent, CEWeaponUseAttemptEvent>(OnChargesCostAttempt);
        SubscribeLocalEvent<CEWeaponChargesCostComponent, CEWeaponUsedEvent>(OnChargesCostUsed);
    }

    // ── Stamina ──

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

    // ── Charges ──

    private void OnChargesCostAttempt(Entity<CEWeaponChargesCostComponent> ent, ref CEWeaponUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        if (!_charges.HasCharges(ent.Owner, cost))
            args.Cancel();
    }

    private void OnChargesCostUsed(Entity<CEWeaponChargesCostComponent> ent, ref CEWeaponUsedEvent args)
    {
        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        _charges.TrySpend(ent.Owner, cost);
    }
}
