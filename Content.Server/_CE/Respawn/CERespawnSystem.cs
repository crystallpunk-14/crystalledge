using Content.Server.GameTicking;
using Content.Shared._CE.Respawn;
using Content.Shared.Ghost;
using Robust.Shared.Player;

namespace Content.Server._CE.Respawn;

public sealed partial class CERespawnSystem : EntitySystem
{
    [Dependency] private readonly GameTicker _gameTicker = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<GhostComponent, CERespawnAction>(OnRespawnAction);
    }

    private void OnRespawnAction(Entity<GhostComponent> ent, ref CERespawnAction args)
    {
        if (!TryComp<ActorComponent>(ent, out var actor))
            return;

        _gameTicker.Respawn(actor.PlayerSession);
    }
}
