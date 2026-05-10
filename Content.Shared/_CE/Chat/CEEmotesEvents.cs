using Robust.Shared.Serialization;

namespace Content.Shared._CE.Chat;

/// <summary>
/// CrystallEdge: Sent by the client when the player selects a quick phrase from the emote radial menu.
/// Uses an index into the player's saved QuickPhrases list to avoid sending arbitrary text from client.
/// </summary>
[Serializable, NetSerializable]
public sealed class PlayQuickPhraseMessage(int index) : EntityEventArgs
{
    public readonly int Index = index;
}
