using Content.Shared.Chat;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public abstract partial class SharedSayChat : CEAnimationActionEntry
{
    /// <summary>
    /// A message spoken by a character.
    /// </summary>
    [DataField(required: true)]
    public LocId Sentence;

    [DataField]
    public InGameICChatType ChatType = InGameICChatType.Speak;

    public override void Play(
        EntityManager entManager,
        EntityUid entity,
        EntityUid? used,
        Angle angle,
        float animationSpeed,
        TimeSpan frame,
        EntityUid? targetEntity,
        EntityCoordinates? targetCoordinates)
    {
        //Only server side logic
    }
}
