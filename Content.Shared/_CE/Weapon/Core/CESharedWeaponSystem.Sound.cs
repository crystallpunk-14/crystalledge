using System.Linq;
using Content.Shared.Damage;
using Content.Shared.FixedPoint;
using Content.Shared.Weapons.Melee.Components;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CE.Weapon.Core;

public abstract partial class CESharedWeaponSystem
{
    private void PlaySwingSound(EntityUid user, EntityUid weapon, CEAttackActionPrototype action)
    {
        _audio.PlayPredicted(action.SwingSound, weapon, user);
    }

    private void PlayHitSound(
        EntityUid target,
        EntityUid? user,
        string? damageType,
        SoundSpecifier? hitSoundOverride,
        CEAttackActionPrototype action)
    {
        var playedSound = false;

        if (Deleted(target))
            return;

        var coords = Transform(target).Coordinates;

        // Check target-specific sounds (MeleeSoundComponent on the target).
        if (TryComp<MeleeSoundComponent>(target, out var damageSoundComp))
        {
            if (damageType == null && damageSoundComp.NoDamageSound != null)
            {
                _audio.PlayPredicted(damageSoundComp.NoDamageSound, coords, user,
                    damageSoundComp.NoDamageSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (damageType != null &&
                     damageSoundComp.SoundTypes?.TryGetValue(damageType, out var st) == true)
            {
                _audio.PlayPredicted(st, coords, user, st.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (damageType != null &&
                     damageSoundComp.SoundGroups?.TryGetValue(damageType, out var sg) == true)
            {
                _audio.PlayPredicted(sg, coords, user, sg.Params.WithVariation(0.05f));
                playedSound = true;
            }
        }

        // Use weapon / action sounds.
        if (!playedSound)
        {
            if (hitSoundOverride != null)
            {
                _audio.PlayPredicted(hitSoundOverride, coords, user,
                    hitSoundOverride.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else if (action.HitSound != null)
            {
                _audio.PlayPredicted(action.HitSound, coords, user,
                    action.HitSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
            else
            {
                _audio.PlayPredicted(action.NoDamageSound, coords, user,
                    action.NoDamageSound.Params.WithVariation(0.05f));
                playedSound = true;
            }
        }

        // Generic fallbacks.
        if (!playedSound)
        {
            switch (damageType)
            {
                case "Burn":
                case "Heat":
                case "Radiation":
                case "Cold":
                    _audio.PlayPredicted(
                        new SoundPathSpecifier("/Audio/Items/welder.ogg"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
                case null:
                    _audio.PlayPredicted(
                        new SoundCollectionSpecifier("WeakHit"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
                case "Brute":
                    _audio.PlayPredicted(
                        new SoundCollectionSpecifier("MetalThud"),
                        target, user, AudioParams.Default.WithVariation(0.05f));
                    break;
            }
        }
    }

    /// <summary>
    /// Returns the damage type or group name with the highest damage for sound selection.
    /// </summary>
    public string? GetHighestDamageSound(DamageSpecifier modifiedDamage)
    {
        var groups = modifiedDamage.GetDamagePerGroup(_protoManager);

        if (groups.Count == 1)
            return groups.Keys.First();

        var highestDamage = FixedPoint2.Zero;
        string? highestDamageType = null;

        foreach (var (type, dmg) in modifiedDamage.DamageDict)
        {
            if (dmg <= highestDamage)
                continue;

            highestDamage = dmg;
            highestDamageType = type;
        }

        return highestDamageType;
    }
}
