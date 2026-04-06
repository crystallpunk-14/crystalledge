using Content.Shared._CE.Health;
using Content.Shared.StatusEffectNew;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Timing;

namespace Content.Shared._CE.Skill.Skills.ChangeHealType;

public sealed partial class CEChangeHealTypeStatusEffectSystem : EntitySystem
{
    [Dependency] private CESharedDamageableSystem _damageable = default!;
    [Dependency] private SharedAudioSystem _audio = default!;
    [Dependency] private IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEChangeHealTypeStatusEffectComponent, StatusEffectRelayedEvent<CEAttemptHealEvent>>(OnAttemptHeal);
    }

    private void OnAttemptHeal(Entity<CEChangeHealTypeStatusEffectComponent> ent, ref StatusEffectRelayedEvent<CEAttemptHealEvent> args)
    {
        if (!_timing.IsFirstTimePredicted)
            return;

        var targetType = ent.Comp.Target;

        var damage = new CEDamageSpecifier(targetType, (int)(args.Args.HealAmount * ent.Comp.DamageMultiplier));
        args.Args.Cancel();

        var pos = Transform(args.Args.Target).Coordinates;
        _damageable.TakeDamage(args.Args.Target, damage, ent);
        Spawn(ent.Comp.Vfx, pos);
        _audio.PlayPvs(ent.Comp.Sound, pos);
    }
}
