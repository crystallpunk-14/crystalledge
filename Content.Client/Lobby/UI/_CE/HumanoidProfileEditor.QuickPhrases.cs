using Robust.Client.UserInterface.Controls;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private LineEdit[] _quickPhraseEdits = Array.Empty<LineEdit>();

    private void InitializeQuickPhrases()
    {
        _quickPhraseEdits = new[]
        {
            QuickPhrase1, QuickPhrase2, QuickPhrase3, QuickPhrase4,
            QuickPhrase5, QuickPhrase6, QuickPhrase7, QuickPhrase8,
        };

        foreach (var edit in _quickPhraseEdits)
        {
            edit.OnTextChanged += _ => OnQuickPhraseChanged();
        }
    }

    public void RefreshQuickPhrases()
    {
        var phrases = Profile?.QuickPhrases ?? new List<string>();
        for (var i = 0; i < _quickPhraseEdits.Length; i++)
        {
            _quickPhraseEdits[i].Text = i < phrases.Count ? phrases[i] : string.Empty;
        }
    }

    private void OnQuickPhraseChanged()
    {
        var phrases = new List<string>();
        foreach (var edit in _quickPhraseEdits)
        {
            phrases.Add(edit.Text);
        }

        Profile = Profile?.WithQuickPhrases(phrases);
        SetDirty();
    }
}
