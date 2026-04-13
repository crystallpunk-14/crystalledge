using Content.Shared._CE.Health;
using Content.Shared._CE.Health.Prototypes;
using Content.Shared._CE.StatusEffectStacks;
using Content.Shared.Inventory;
using Content.Shared.StatusEffectNew;
using Content.Shared.StatusEffectNew.Components;
using Robust.Shared.Network;
using Robust.Shared.Prototypes;

namespace Content.Shared._CE.TempShield;

public sealed class CETempShieldSystem : EntitySystem
{
    [Dependency] private readonly CEStatusEffectStackSystem _stacks = default!;
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedTransformSystem _transform = default!;

    private static readonly Dictionary<ProtoId<CEDamageTypePrototype>, EntProtoId> ShieldEffects = new()
    {
        { "Physical", "CEStatusEffectTempShield" },
        { "Fire",     "CEStatusEffectTempShieldFire" },
        { "Cold",     "CEStatusEffectTempShieldCold" },
    };

    private static readonly Dictionary<ProtoId<CEDamageTypePrototype>, EntProtoId> AddEffects = new()
    {
        { "Physical", "CEEffectAddTempShieldPhysical" },
        { "Fire",     "CEEffectAddTempShieldFire" },
        { "Cold",     "CEEffectAddTempShieldFrost" },
    };

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CETempShieldStatusEffectComponent, StatusEffectRelayedEvent<CEDamageCalculateEvent>>(OnBeforeDamage);
        SubscribeLocalEvent<CETempShieldStatusEffectComponent, StatusEffectRemovedEvent>(OnRemoved);
    }

    private void OnRemoved(Entity<CETempShieldStatusEffectComponent> ent, ref StatusEffectRemovedEvent args)
    {
        if (!_net.IsServer) //TODO: Fix prediction
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect) || statusEffect.AppliedTo is null)
            return;

        var vfx = Spawn(ent.Comp.BreakEffect, Transform(statusEffect.AppliedTo.Value).Coordinates);
        _transform.SetParent(vfx, statusEffect.AppliedTo.Value);
    }

    private static readonly TimeSpan DefaultCycleDuration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Adds temporary shield stacks of the specified damage type to the target.
    /// </summary>
    public bool TryAddTempShield(
        EntityUid target,
        ProtoId<CEDamageTypePrototype> damageType,
        int stacks = 1)
    {
        if (stacks <= 0)
            return false;

        if (!ShieldEffects.TryGetValue(damageType, out var statusEffect))
        {
            Log.Error($"No temporary shield status effect defined for damage type '{damageType}'.");
            return false;
        }

        var ev = new CECalculateTempShieldStacksEvent(stacks);
        RaiseLocalEvent(target, ev);
        stacks = ev.Stacks;

        if (stacks <= 0)
            return false;

        if (!_stacks.TryAddStack(target, statusEffect, out _, stacks, DefaultCycleDuration))
            return false;

        if (_net.IsServer && AddEffects.TryGetValue(damageType, out var effectProto))
        {
            var vfx = Spawn(effectProto, Transform(target).Coordinates);
            _transform.SetParent(vfx, target);
        }

        _stacks.SetStackDelta(target, statusEffect, -1);
        return true;
    }

    private void OnBeforeDamage(Entity<CETempShieldStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEDamageCalculateEvent> args)
    {
        if (args.Args.Cancelled)
            return;

        if (!TryComp<CEStatusEffectStackComponent>(ent, out var stackComp))
            return;

        if (!TryComp<StatusEffectComponent>(ent, out var statusEffect) || statusEffect.AppliedTo is null)
            return;

        var shield = ent.Comp;
        var currentStacks = stackComp.Stacks;
        var absorbBudget = currentStacks * shield.AbsorbPerStack;
        var totalAbsorbed = 0;

        var newDamage = new CEDamageSpecifier();
        foreach (var (damageType, damageAmount) in args.Args.Damage.Types)
        {
            if (damageAmount <= 0)
            {
                newDamage.Types[damageType] = damageAmount;
                continue;
            }

            if (shield.AbsorbedTypes.Count > 0 && !shield.AbsorbedTypes.Contains(damageType))
            {
                newDamage.Types[damageType] = damageAmount;
                continue;
            }

            var absorbed = Math.Min(damageAmount, absorbBudget);
            absorbBudget -= absorbed;
            totalAbsorbed += absorbed;

            var remaining = damageAmount - absorbed;
            if (remaining > 0)
                newDamage.Types[damageType] = remaining;
        }

        if (totalAbsorbed <= 0)
            return;

        var stacksConsumed = (int) Math.Ceiling((double) totalAbsorbed / shield.AbsorbPerStack);
        stacksConsumed = Math.Min(stacksConsumed, currentStacks);

        _stacks.TryRemoveStack(ent.Owner, stacksConsumed);


        if (_net.IsServer && stacksConsumed != currentStacks) //TODO: Fix prediction
        {
            var vfx = Spawn(ent.Comp.TakeDamageEffect, Transform(statusEffect.AppliedTo.Value).Coordinates);
            _transform.SetParent(vfx, statusEffect.AppliedTo.Value);
        }

        if (newDamage.Total <= 0)
            args.Args.Cancelled = true;
        else
            args.Args.Damage = newDamage;
    }
}

/// <summary>
/// Raised on the target entity when temporary shield stacks are about to be added.
/// Handlers can modify the stack count (e.g. double it via a passive skill).
/// </summary>
public sealed class CECalculateTempShieldStacksEvent(int stacks) : EntityEventArgs, IInventoryRelayEvent
{
    public int Stacks = stacks;

    public SlotFlags TargetSlots => SlotFlags.WITHOUT_POCKET;
}
