using Content.Shared._CE.Music;
using Robust.Shared.GameStates;

namespace Content.Client._CE.Music;

/// <summary>
/// Per-chunk ambient music controller: requests the theme of the world chunk the local player currently
/// stands in. The server pushes that theme onto the player's <see cref="CEChunkAmbientMusicComponent"/>;
/// this controller mirrors it into the engine at <see cref="CEAmbientMusicSource.Chunk"/> priority, so it
/// overrides the map theme. Declares its preference via <see cref="CEAmbientMusicSystem.SetSourceTheme"/>
/// and never touches playback directly. The per-chunk analog of the map controller.
/// </summary>
public sealed partial class CEAmbientMusicSystem
{
    /// <summary>
    /// Hooked from <see cref="Initialize"/> in the main partial.
    /// </summary>
    private void InitializeChunkMusic()
    {
        SubscribeLocalEvent<CEChunkAmbientMusicComponent, AfterAutoHandleStateEvent>(OnChunkMusicState);
    }

    private void OnChunkMusicState(Entity<CEChunkAmbientMusicComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        if (ent.Owner != _player.LocalEntity)
            return;

        SetSourceTheme(CEAmbientMusicSource.Chunk, ent.Comp.Theme);
    }
}
