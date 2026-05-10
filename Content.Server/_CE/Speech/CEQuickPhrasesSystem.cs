using Content.Server.Chat.Systems;
using Content.Server.Preferences.Managers;
using Content.Shared._CE.Chat;
using Content.Shared._CE.Preferences;
using Content.Shared.Chat;

namespace Content.Server._CE.Speech;

/// <summary>
/// CrystallEdge: Handles quick phrases sent from the emote radial menu.
/// Reads the player's saved QuickPhrases from preferences and sends the phrase as IC speak or emote.
/// </summary>
public sealed class CEQuickPhrasesSystem : EntitySystem
{
    [Dependency] private readonly ChatSystem _chat = default!;
    [Dependency] private readonly IServerPreferencesManager _preferencesManager = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeAllEvent<PlayQuickPhraseMessage>(OnPlayQuickPhrase);
    }

    private void OnPlayQuickPhrase(PlayQuickPhraseMessage msg, EntitySessionEventArgs args)
    {
        var player = args.SenderSession.AttachedEntity;
        if (!player.HasValue)
            return;

        // Index sanity check — a cheating client could send any int.
        if (msg.Index < 0 || msg.Index >= CEQuickPhrase.MaxQuickPhrases)
            return;

        if (!_preferencesManager.TryGetCachedPreferences(args.SenderSession.UserId, out var prefs))
            return;

        if (prefs.SelectedCharacter is not { } profile)
            return;

        if (msg.Index >= profile.QuickPhrases.Count)
            return;

        var phrase = profile.QuickPhrases[msg.Index];
        var text = phrase.Text;

        if (string.IsNullOrWhiteSpace(text))
            return;

        // Truncate defensively — text was validated on save, but guard anyway.
        if (text.Length > CEQuickPhrase.MaxLength)
            text = text[..CEQuickPhrase.MaxLength];

        var chatType = phrase.IsEmotion ? InGameICChatType.Emote : InGameICChatType.Speak;
        _chat.TrySendInGameICMessage(player.Value, text, chatType, ChatTransmitRange.Normal, false);
    }
}
