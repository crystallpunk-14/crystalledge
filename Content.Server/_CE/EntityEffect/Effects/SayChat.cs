using Content.Server.Chat.Systems;
using Content.Shared._CE.EntityEffect;
using Content.Shared._CE.EntityEffect.Effects;
using Content.Shared.Speech;
using Content.Shared.Speech.Muting;

namespace Content.Server._CE.EntityEffect.Effects;

public sealed partial class CESayChatEffectSystem : CEEntityEffectSystem<SayChat>
{
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override void Effect(ref CEEntityEffectEvent<SayChat> args)
    {
        if (!HasComp<SpeechComponent>(args.Args.User) || HasComp<MutedComponent>(args.Args.User))
            return;
        if (string.IsNullOrWhiteSpace(args.Effect.Sentence))
            return;

        _chat.TrySendInGameICMessage(args.Args.User, Loc.GetString(args.Effect.Sentence), args.Effect.ChatType, true);
    }
}
