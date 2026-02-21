using Content.Shared._CE.Mana.Core.Components;
using Content.Shared.Audio;
using Content.Shared.Examine;
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
        ChangeEnergy((ent, ent.Comp), ent.Comp.MaxEnergy - ent.Comp.Energy, out _, out _);
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
        int energy,
        out int deltaEnergy,
        out int overloadEnergy)
    {
        deltaEnergy = 0;
        overloadEnergy = 0;

        if (!Resolve(ent, ref ent.Comp, false))
            return;

        var oldEnergy = ent.Comp.Energy;
        var newEnergy = (int)Math.Clamp(ent.Comp.Energy + (float)energy, 0, ent.Comp.MaxEnergy);

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
        int energy,
        out int deltaEnergy,
        out int overloadEnergy)
    {
        deltaEnergy = 0;
        overloadEnergy = 0;

        if (!Resolve(sender, ref sender.Comp) || !Resolve(receiver, ref receiver.Comp))
            return;

        //We check how much space is left in the container so as not to overload it, but only if it does not support overloading
        var freeSpace = receiver.Comp.MaxEnergy - receiver.Comp.Energy;
        var transferEnergy = Math.Min(freeSpace, energy);

        ChangeEnergy(sender, -transferEnergy, out var change, out var overload);
        ChangeEnergy(receiver , -(change + overload), out deltaEnergy, out overloadEnergy);
    }

    public bool HasEnergy(EntityUid uid, int energy, CEMagicEnergyContainerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return false;

        return component.Energy >= energy;
    }

    public string GetEnergyExaminedText(Entity<CEMagicEnergyContainerComponent> ent)
    {
        var power = ent.Comp.MaxEnergy <= 0
            ? 0
            : ent.Comp.Energy * 100 / ent.Comp.MaxEnergy;

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

    public void SetMaximumEnergy(Entity<CEMagicEnergyContainerComponent?> ent, int energy)
    {
        if (!Resolve(ent, ref ent.Comp, false))
            return;

        if (ent.Comp.MaxEnergy == energy)
            return;

        var oldEnergy = ent.Comp.Energy;
        var oldMax = ent.Comp.MaxEnergy;

        ent.Comp.MaxEnergy = energy;

        if (oldMax > 0)
        {
            // Scale current energy to preserve the ratio (floor due to ints)
            ent.Comp.Energy = (int)((long)oldEnergy * energy / oldMax);
            ent.Comp.Energy = Math.Clamp(ent.Comp.Energy, 0, ent.Comp.MaxEnergy);
        }
        else
        {
            // If previous max was zero or negative, fallback to clamping
            ent.Comp.Energy = Math.Min(ent.Comp.Energy, ent.Comp.MaxEnergy);
        }

        Dirty(ent);

        RaiseLocalEvent(ent, new CEMagicEnergyLevelChangeEvent(ent, oldEnergy, ent.Comp.Energy, ent.Comp.MaxEnergy), true);
    }
}

/// <summary>
/// It's triggered when the energy change in MagicEnergyContainer
/// </summary>
public sealed class CEMagicEnergyLevelChangeEvent(EntityUid target, int oldValue, int newValue, int maxValue)
    : EntityEventArgs
{
    public readonly EntityUid Target = target;
    public readonly int OldValue = oldValue;
    public readonly int NewValue = newValue;
    public readonly int MaxValue = maxValue;
}
