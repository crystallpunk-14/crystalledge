using Robust.Shared.Audio;
using Robust.Shared.Localization;
using Robust.Shared.Serialization;

namespace Content.Shared._CE.ScreenPopup;

/// <summary>
/// Sent from the server to a specific client to display a full-screen cinematic popup.
/// Used when a player enters a new dungeon location for the first time.
/// The client resolves title and description via their locale.
/// </summary>
[Serializable, NetSerializable]
public sealed class CEScreenPopupShowEvent : EntityEventArgs
{
    /// <summary>Localization key for the title, resolved per-client locale.</summary>
    public LocId? Title;

    /// <summary>Localization key for the description, resolved per-client locale.</summary>
    public LocId? Desc;

    /// <summary>Optional sound played when the popup is shown.</summary>
    public SoundSpecifier? Sound;
}
