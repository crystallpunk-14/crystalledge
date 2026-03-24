using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Shared._CE.EntityEffect.Effects;

public sealed partial class PlaySound : CEEntityEffect
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    public override void Effect(EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        var audio = entManager.System<SharedAudioSystem>();

        audio.PlayPredicted(Sound, user, user, Sound.Params.WithVariation(0.15f));
    }
}
