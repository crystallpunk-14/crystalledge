using Content.Server.Chat.Systems;
using Content.Shared._CE.Animation.Core.Actions;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;
using Robust.Shared.Map;

namespace Content.Server._CE.Animation.Core.Actions;

public sealed partial class SayChat : SharedSayChat
{
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
        // If we can't speak, we can't speak
        if (!entManager.HasComponent<SpeechComponent>(entity) || entManager.HasComponent<MutedComponent>(entity))
            return;
        if (string.IsNullOrWhiteSpace(Sentence))
            return;

        var chat = entManager.System<ChatSystem>();

        chat.TrySendInGameICMessage(entity, Loc.GetString(Sentence), ChatType, true);
    }
}
