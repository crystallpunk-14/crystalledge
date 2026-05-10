using Content.Shared._CE.Preferences;
using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private (LineEdit edit, CheckBox emotion)[] _quickPhraseControls = Array.Empty<(LineEdit, CheckBox)>();
    private bool _updatingQuickPhrases;

    private void InitializeQuickPhrases()
    {
        _quickPhraseControls = new[]
        {
            (QuickPhrase1, QuickPhraseEmotion1),
            (QuickPhrase2, QuickPhraseEmotion2),
            (QuickPhrase3, QuickPhraseEmotion3),
            (QuickPhrase4, QuickPhraseEmotion4),
            (QuickPhrase5, QuickPhraseEmotion5),
            (QuickPhrase6, QuickPhraseEmotion6),
            (QuickPhrase7, QuickPhraseEmotion7),
            (QuickPhrase8, QuickPhraseEmotion8),
        };

        foreach (var (edit, emotion) in _quickPhraseControls)
        {
            edit.OnTextChanged += _ => OnQuickPhraseChanged();
            emotion.OnToggled += _ => OnQuickPhraseChanged();
        }
    }

    public void RefreshQuickPhrases()
    {
        var phrases = Profile?.QuickPhrases ?? new List<CEQuickPhrase>();
        for (var i = 0; i < _quickPhraseControls.Length; i++)
        {
            var (edit, emotion) = _quickPhraseControls[i];
            if (i < phrases.Count)
            {
                edit.Text = phrases[i].Text;
                emotion.Pressed = phrases[i].IsEmotion;
            }
            else
            {
                edit.Text = string.Empty;
                emotion.Pressed = false;
            }
        }
    }

    private void OnQuickPhraseChanged()
    {
        if (_updatingQuickPhrases)
            return;

        _updatingQuickPhrases = true;
        try
        {
            var phrases = new List<CEQuickPhrase>();
            foreach (var (edit, emotion) in _quickPhraseControls)
            {
                var text = edit.Text;
                // Enforce MaxLength on the client — truncate and update the control if needed
                if (text.Length > CEQuickPhrase.MaxLength)
                {
                    text = text[..CEQuickPhrase.MaxLength];
                    edit.Text = text;
                }
                phrases.Add(new CEQuickPhrase(text, emotion.Pressed));
            }

            Profile = Profile?.WithQuickPhrases(phrases);
            SetDirty();
        }
        finally
        {
            _updatingQuickPhrases = false;
        }
    }
}
