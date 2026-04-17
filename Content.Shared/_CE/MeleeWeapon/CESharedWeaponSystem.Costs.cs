using Content.Shared._CE.Animation.Item.Components;
using Content.Shared._CE.Charges;
using Content.Shared._CE.Mana.Core;
using Content.Shared._CE.Mana.Core.Components;
using Content.Shared._CE.MeleeWeapon.Components;
using Content.Shared._CE.Stamina;
using Content.Shared.Examine;

namespace Content.Shared._CE.MeleeWeapon;

public abstract partial class CESharedWeaponSystem
{
    [Dependency] private readonly CEChargesSystem _charges = default!;
    [Dependency] private readonly CESharedMagicEnergySystem _magicEnergy = default!;
    [Dependency] private readonly CEStaminaSystem _stamina = default!;

    private void InitializeCosts()
    {
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUseAttemptEvent>(OnStaminaCostAttempt);
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, CEWeaponUsedEvent>(OnStaminaCostUsed);
        SubscribeLocalEvent<CEWeaponStaminaCostComponent, ExaminedEvent>(OnStaminaCostExamined);

        SubscribeLocalEvent<CEWeaponManaCostComponent, CEWeaponUseAttemptEvent>(OnManaCostAttempt);
        SubscribeLocalEvent<CEWeaponManaCostComponent, CEWeaponUsedEvent>(OnManaCostUsed);
        SubscribeLocalEvent<CEWeaponManaCostComponent, ExaminedEvent>(OnManaCostExamined);

        SubscribeLocalEvent<CEWeaponChargesCostComponent, CEWeaponUseAttemptEvent>(OnChargesCostAttempt);
        SubscribeLocalEvent<CEWeaponChargesCostComponent, CEWeaponUsedEvent>(OnChargesCostUsed);
        SubscribeLocalEvent<CEWeaponChargesCostComponent, ExaminedEvent>(OnChargesCostExamined);
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

    private void OnStaminaCostExamined(Entity<CEWeaponStaminaCostComponent> ent, ref ExaminedEvent args)
    {
        foreach (var (useType, cost) in ent.Comp.Costs)
        {
            if (cost <= 0f)
                continue;

            var useTypeName = Loc.GetString($"ce-weapon-usetype-{useType.ToString().ToLowerInvariant()}");
            args.PushMarkup(
                Loc.GetString("ce-weapon-cost-stamina", ("usetype", useTypeName), ("cost", cost)),
                priority: ExaminePriority(useType, 1));
        }
    }

    // ── Mana ──

    private void OnManaCostAttempt(Entity<CEWeaponManaCostComponent> ent, ref CEWeaponUseAttemptEvent args)
    {
        if (args.Cancelled)
            return;

        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        if (!TryComp<CEMagicEnergyContainerComponent>(args.User, out var mana) || mana.Energy < cost)
            args.Cancel();
    }

    private void OnManaCostUsed(Entity<CEWeaponManaCostComponent> ent, ref CEWeaponUsedEvent args)
    {
        if (!ent.Comp.Costs.TryGetValue(args.UseType, out var cost) || cost <= 0)
            return;

        _magicEnergy.Take(args.User, cost);
    }

    private void OnManaCostExamined(Entity<CEWeaponManaCostComponent> ent, ref ExaminedEvent args)
    {
        foreach (var (useType, cost) in ent.Comp.Costs)
        {
            if (cost <= 0)
                continue;

            var useTypeName = Loc.GetString($"ce-weapon-usetype-{useType.ToString().ToLowerInvariant()}");
            args.PushMarkup(
                Loc.GetString("ce-weapon-cost-mana", ("usetype", useTypeName), ("cost", cost)),
                priority: ExaminePriority(useType, 2));
        }
    }

    /// <summary>
    /// Returns a priority value that groups costs by UseType in examine output.
    /// Lower UseType ordinal = higher priority (shown first). Costs within same UseType
    /// are ordered by <paramref name="costOrder"/>.
    /// </summary>
    private static int ExaminePriority(CEUseType useType, int costOrder)
    {
        // Higher priority = shown first. Primary (0) should come before Secondary (1).
        // Base 10-per-UseType, subtract costOrder for ordering within a group.
        return 10 - (int) useType * 3 - costOrder;
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

    private void OnChargesCostExamined(Entity<CEWeaponChargesCostComponent> ent, ref ExaminedEvent args)
    {
        foreach (var (useType, cost) in ent.Comp.Costs)
        {
            if (cost <= 0)
                continue;

            var useTypeName = Loc.GetString($"ce-weapon-usetype-{useType.ToString().ToLowerInvariant()}");
            args.PushMarkup(
                Loc.GetString("ce-weapon-cost-charges", ("usetype", useTypeName), ("cost", cost)),
                priority: ExaminePriority(useType, 0));
        }
    }
}
