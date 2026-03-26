using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class PlaySound : CEEntityEffectBase<PlaySound>
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;
}

public sealed partial class CEPlaySoundEffectSystem : CEEntityEffectSystem<PlaySound>
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    protected override void Effect(ref CEEntityEffectEvent<PlaySound> args)
    {
        _audio.PlayPredicted(args.Effect.Sound, args.Args.User, args.Args.User,
            args.Effect.Sound.Params.WithVariation(0.15f));
    }
}
