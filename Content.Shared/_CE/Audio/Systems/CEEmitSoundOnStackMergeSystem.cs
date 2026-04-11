using Content.Shared._CE.Audio.Components;
using Content.Shared.Stacks;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;

namespace Content.Shared._CE.Audio.Systems;

/// <summary>
/// System that plays sounds when stacks are merged together.
/// </summary>
public sealed class CEEmitSoundOnStackMergeSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly INetManager _net = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEEmitSoundOnStackMergeComponent, StackCountChangedEvent>(OnStackCountChanged);
    }

    private void OnStackCountChanged(Entity<CEEmitSoundOnStackMergeComponent> ent, ref StackCountChangedEvent args)
    {
        // Only play sound when count increases (merge) not decreases (split/use)
        if (args.NewCount <= args.OldCount || ent.Comp.Sound == null)
            return;

        if (_net.IsClient)
            return;

        _audio.PlayPvs(ent.Comp.Sound, ent);
    }
}
