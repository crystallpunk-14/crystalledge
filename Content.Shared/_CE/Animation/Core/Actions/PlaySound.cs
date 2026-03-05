using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public sealed partial class PlaySound : CEAnimationActionEntry
{
    [DataField(required: true)]
    public SoundSpecifier Sound = default!;

    public override void Play(EntityManager entManager,
        EntityUid entity,
        EntityUid? used,
        Angle angle,
        float animationSpeed,
        TimeSpan frame,
        EntityUid? targetEntity,
        EntityCoordinates? targetCoordinates)
    {
        var audio = entManager.System<SharedAudioSystem>();

        audio.PlayPredicted(Sound, entity, entity, Sound.Params.WithVariation(0.15f));
    }
}
