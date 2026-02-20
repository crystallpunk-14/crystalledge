using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Audio;
using Content.Shared.Examine;
using Content.Shared.FixedPoint;
using Content.Shared.Rejuvenate;

namespace Content.Shared._CE.Mana.Core;

public abstract class CESharedMagicEnergySystem : EntitySystem
{
    [Dependency] private readonly SharedAmbientSoundSystem _ambient = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<CEMagicEnergyContainerComponent, RejuvenateEvent>(OnRejuvenate);

        SubscribeLocalEvent<CEMagicEnergyExaminableComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<CEMagicEnergyAmbientSoundComponent, CESlotCrystalPowerChangedEvent>(OnSlotPowerChanged);
    }

    private void OnRejuvenate(Entity<CEMagicEnergyContainerComponent> ent, ref RejuvenateEvent args)
    {
        ChangeEnergy((ent, ent.Comp), ent.Comp.MaxEnergy - ent.Comp.Energy, out var deltaEnergy, out var overloadEnergy, true);
    }

    private void OnExamined(Entity<CEMagicEnergyExaminableComponent> ent, ref ExaminedEvent args)
    {
        if (!TryComp<CEMagicEnergyContainerComponent>(ent, out var magicContainer))
            return;

        if (!args.IsInDetailsRange)
            return;

        args.PushMarkup(GetEnergyExaminedText((ent, magicContainer)));
    }

    private void OnSlotPowerChanged(Entity<CEMagicEnergyAmbientSoundComponent> ent, ref CESlotCrystalPowerChangedEvent args)
    {
        _ambient.SetAmbience(ent, args.Powered);
    }

    public void ChangeEnergy(Entity<CEMagicEnergyContainerComponent?> ent,
        FixedPoint2 energy,
        out FixedPoint2 deltaEnergy,
        out FixedPoint2 overloadEnergy,
        bool safe = false)
    {
        deltaEnergy = 0;
        overloadEnergy = 0;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (!safe)
        {
            // Overload
            if (ent.Comp.Energy + energy > ent.Comp.MaxEnergy && ent.Comp.UnsafeSupport)
            {
                overloadEnergy = ent.Comp.Energy + energy - ent.Comp.MaxEnergy;
                RaiseLocalEvent(ent, new CEMagicEnergyOverloadEvent(overloadEnergy));
            }

            // Burn out
            if (ent.Comp.Energy + energy < 0 && ent.Comp.UnsafeSupport)
            {
                overloadEnergy = ent.Comp.Energy + energy;
                RaiseLocalEvent(ent, new CEMagicEnergyBurnOutEvent(-energy - ent.Comp.Energy));
            }
        }

        var oldEnergy = ent.Comp.Energy;
        var newEnergy = Math.Clamp((float) ent.Comp.Energy + (float) energy, 0, (float) ent.Comp.MaxEnergy);

        deltaEnergy = newEnergy - oldEnergy;
        ent.Comp.Energy = newEnergy;
        Dirty(ent);

        if (oldEnergy != newEnergy)
            RaiseLocalEvent(ent, new CEMagicEnergyLevelChangeEvent(ent, oldEnergy, newEnergy, ent.Comp.MaxEnergy), true);
    }

    /// <summary>
    /// Set energy to 0
    /// </summary>
    public void ClearEnergy(Entity<CEMagicEnergyContainerComponent?> ent)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ChangeEnergy(ent, -ent.Comp.Energy, out _, out _);
    }

    public void TransferEnergy(Entity<CEMagicEnergyContainerComponent?> sender,
        Entity<CEMagicEnergyContainerComponent?> receiver,
        FixedPoint2 energy,
        out FixedPoint2 deltaEnergy,
        out FixedPoint2 overloadEnergy,
        bool safe = false)
    {
        deltaEnergy = 0;
        overloadEnergy = 0;

        if (!Resolve(sender, ref sender.Comp) || !Resolve(receiver, ref receiver.Comp))
            return;

        var transferEnergy = energy;
        //We check how much space is left in the container so as not to overload it, but only if it does not support overloading
        if (!receiver.Comp.UnsafeSupport || safe)
        {
            var freeSpace = receiver.Comp.MaxEnergy - receiver.Comp.Energy;
            transferEnergy = FixedPoint2.Min(freeSpace, energy);
        }

        ChangeEnergy(sender, -transferEnergy, out var change, out var overload, safe);
        ChangeEnergy(receiver , -(change + overload), out deltaEnergy, out overloadEnergy, safe);
    }

    public bool HasEnergy(EntityUid uid, FixedPoint2 energy, CEMagicEnergyContainerComponent? component = null, bool safe = false)
    {
        if (!Resolve(uid, ref component))
            return false;

        if (!safe && component.UnsafeSupport)
            return true;

        return component.Energy >= energy;
    }

    public string GetEnergyExaminedText(Entity<CEMagicEnergyContainerComponent> ent)
    {
        var power = (int) (ent.Comp.Energy / ent.Comp.MaxEnergy * 100);

        // TODO: customization for examined

        var color = "#3fc488";
        if (power < 66)
            color = "#f2a93a";

        if (power < 33)
            color = "#c23030";

        return Loc.GetString("ce-magic-energy-scan-result",
            ("item", MetaData(ent).EntityName),
            ("power", power),
            ("color", color));
    }

    public void ChangeMaximumEnergy(Entity<CEMagicEnergyContainerComponent?> ent, FixedPoint2 energy)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        ent.Comp.MaxEnergy += energy;

        ChangeEnergy(ent, energy, out _, out _);
    }
}

/// <summary>
/// It's triggered when the energy change in MagicEnergyContainer
/// </summary>
public sealed class CEMagicEnergyLevelChangeEvent(EntityUid target, FixedPoint2 oldValue, FixedPoint2 newValue, FixedPoint2 maxValue)
    : EntityEventArgs
{
    public readonly EntityUid Tagret = target;
    public readonly FixedPoint2 OldValue = oldValue;
    public readonly FixedPoint2 NewValue = newValue;
    public readonly FixedPoint2 MaxValue = maxValue;
}

/// <summary>
/// It's triggered when more energy enters the MagicEnergyContainer than it can hold.
/// </summary>
public sealed class CEMagicEnergyOverloadEvent(FixedPoint2 overloadEnergy) : EntityEventArgs
{
    public readonly FixedPoint2 OverloadEnergy = overloadEnergy;
}

/// <summary>
/// It's triggered they something try to get energy out of MagicEnergyContainer that is lacking there.
/// </summary>
public sealed class CEMagicEnergyBurnOutEvent(FixedPoint2 burnOutEnergy) : EntityEventArgs
{
    public readonly FixedPoint2 BurnOutEnergy = burnOutEnergy;
}
