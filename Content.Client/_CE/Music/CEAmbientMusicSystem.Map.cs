using Content.Shared._CE.Music;
using Robust.Shared.Player;

namespace Content.Client._CE.Music;

/// <summary>
/// Map ambient music controller: requests the theme from the local player's current map
/// (<see cref="CEMapAmbientMusicThemeComponent"/>) whenever they change map. Lowest priority — a
/// per-chunk theme overrides it. Declares its preference via <see cref="CEAmbientMusicSystem.SetSourceTheme"/>
/// and never touches playback directly.
/// </summary>
public sealed partial class CEAmbientMusicSystem
{
    [Dependency] private EntityQuery<TransformComponent> _xformQuery = default!;
    [Dependency] private EntityQuery<CEMapAmbientMusicThemeComponent> _mapThemeQuery = default!;

    /// <summary>
    /// Hooked from <see cref="Initialize"/> in the main partial.
    /// </summary>
    private void InitializeMapMusic()
    {
        SubscribeLocalEvent<ActorComponent, EntParentChangedMessage>(OnParentChanged); //Prohibited dark magic used here! TODO: remove that cursed subscription
    }

    private void OnParentChanged(Entity<ActorComponent> ent, ref EntParentChangedMessage args)
    {
        if (args.Entity != _player.LocalEntity)
            return;

        var mapUid = _xformQuery.TryGetComponent(args.Entity, out var xform) ? xform.MapUid : null;
        var theme = mapUid != null && _mapThemeQuery.TryGetComponent(mapUid.Value, out var mapTheme)
            ? mapTheme.Theme
            : null;

        SetSourceTheme(CEAmbientMusicSource.Map, theme);
    }
}
