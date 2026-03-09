using Content.Shared.Chat;
using Robust.Shared.Map;

namespace Content.Shared._CE.Animation.Core.Actions;

public abstract partial class SharedSayChat : CEAnimationActionEntry
{
    /// <summary>
    /// A message spoken by a character. Will automatically attempt to use it as LocId, but you can also insert regular text.
    /// </summary>
    [DataField(required: true)]
    public string Sentence;

    [DataField]
    public InGameICChatType ChatType = InGameICChatType.Speak;

    public override void Play(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        //Only server side logic
    }
}
