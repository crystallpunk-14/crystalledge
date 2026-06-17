using Content.Server._CE.WorldGen;
using Content.Shared._CE.Music;
using Robust.Shared.Prototypes;

namespace Content.Server._CE.Music;

/// <summary>
/// Server side of per-chunk ambient music: as a player crosses into a new chunk type, copies that
/// type's <see cref="Content.Shared._CE.WorldGen.Prototypes.CEWorldChunkTypePrototype.AmbientMusic"/>
/// onto the player's networked <see cref="CEChunkAmbientMusicComponent"/>, which the client plays.
/// The per-chunk analog of the map-level <see cref="CEMapAmbientMusicThemeComponent"/>.
/// </summary>
public sealed partial class CEChunkAmbientMusicSystem : EntitySystem
{
    [Dependency] private IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CEChunkAmbientMusicComponent, CEMoveToNewLocation>(OnMoveToNewLocation);
    }

    private void OnMoveToNewLocation(Entity<CEChunkAmbientMusicComponent> ent, ref CEMoveToNewLocation args)
    {
        var theme = _proto.TryIndex(args.To, out var type) ? type.AmbientMusic : null;
        if (ent.Comp.Theme == theme)
            return;

        ent.Comp.Theme = theme;
        Dirty(ent);
    }
}
