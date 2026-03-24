using Content.Server.Chat.Systems;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Robust.Shared.Map;

namespace Content.Server._CE.EntityEffect.Effects;

public sealed partial class SayChat : SharedSayChat
{
    public override void Effect(
        EntityManager entManager,
        EntityUid user,
        EntityUid? used,
        Angle angle,
        float speed,
        TimeSpan frame,
        EntityUid? target,
        EntityCoordinates? position)
    {
        // If we can't speak, we can't speak
        if (!entManager.HasComponent<SpeechComponent>(user) || entManager.HasComponent<MutedComponent>(user))
            return;
        if (string.IsNullOrWhiteSpace(Sentence))
            return;

        var chat = entManager.System<ChatSystem>();

        chat.TrySendInGameICMessage(user, Loc.GetString(Sentence), ChatType, true);
    }
}
