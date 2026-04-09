using Content.Client._CE.Speech;
using Content.Shared._CE.Speech;
using Robust.Shared.Prototypes;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<CEBarkSpeechPrototype> _barkVoices = new();
    private CEBarkSpeechSystem? _barkSystem;

    private void RefreshBarkVoices()
    {
        BarkVoiceButton.Clear();
        _barkVoices.Clear();

        _barkVoices.AddRange(_prototypeManager.EnumeratePrototypes<CEBarkSpeechPrototype>());
        _barkVoices.Sort((a, b) => string.Compare(a.ID, b.ID, StringComparison.Ordinal));

        for (var i = 0; i < _barkVoices.Count; i++)
        {
            BarkVoiceButton.AddItem(_barkVoices[i].ID, i);

            if (Profile?.BarkVoice == _barkVoices[i].ID)
                BarkVoiceButton.SelectId(i);
        }
    }

    private void UpdateBarkControls()
    {
        if (Profile == null)
            return;

        for (var i = 0; i < _barkVoices.Count; i++)
        {
            if (_barkVoices[i].ID == Profile.BarkVoice)
            {
                BarkVoiceButton.SelectId(i);
                break;
            }
        }

        BarkPitchSlider.Value = Profile.BarkPitch * 100f;
    }

    private void SetBarkVoice(string id)
    {
        Profile = Profile?.WithBarkVoice(id);
        SetDirty();
    }

    private void SetBarkPitch(float pitch)
    {
        Profile = Profile?.WithBarkPitch(pitch);
        SetDirty();
    }

    private void OnBarkPreviewPressed()
    {
        if (Profile == null)
            return;

        _barkSystem ??= _entManager.System<CEBarkSpeechSystem>();
        _barkSystem.PlayPreview(Profile.BarkVoice, Profile.BarkPitch);
    }
}
