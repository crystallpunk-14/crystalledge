using Robust.Shared.Serialization;

namespace Content.Shared._CE.Preferences;

/// <summary>
/// A single quick phrase entry for a character, with an optional emote flag.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEQuickPhrase : IEquatable<CEQuickPhrase>
{
    /// <summary>Maximum number of characters allowed in a quick phrase. Enforced on both client and server.</summary>
    public const int MaxLength = 32;

    /// <summary>Maximum number of quick phrase slots per character.</summary>
    public const int MaxQuickPhrases = 8;

    public string Text { get; set; } = string.Empty;
    public bool IsEmotion { get; set; }

    public CEQuickPhrase() { }

    public CEQuickPhrase(string text, bool isEmotion)
    {
        Text = text;
        IsEmotion = isEmotion;
    }

    public CEQuickPhrase Clone() => new(Text, IsEmotion);

    public bool Equals(CEQuickPhrase? other) =>
        other is not null && Text == other.Text && IsEmotion == other.IsEmotion;

    public override bool Equals(object? obj) => obj is CEQuickPhrase other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Text, IsEmotion);
}
