using Content.Server._CE.Skills.Blessing.Components;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Robust.Shared.Physics.Events;

namespace Content.Server._CE.Skills.Blessing;

/// <summary>
/// Handles trigger zone collisions (enter/exit) and blessing spawning/cleanup.
/// </summary>
public sealed partial class CEBlessingSystem
{
    private void InitializeTrigger()
    {
        SubscribeLocalEvent<CEBlessingTriggerComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<CEBlessingTriggerComponent, EndCollideEvent>(OnTriggerExit);
        SubscribeLocalEvent<CEBlessingComponent, CEBlessingClaimedEvent>(OnBlessingClaimed);
    }

    private void OnTriggerEnter(
        Entity<CEBlessingTriggerComponent> ent,
        ref StartCollideEvent args)
    {
        if (args.OurFixtureId != "trigger")
            return;

        if (ent.Comp.LinkedStatue is not { } statueUid)
            return;

        if (!TryComp<CEBlessingStatueComponent>(statueUid, out var statue))
            return;

        var player = args.OtherEntity;

        if (!HasComp<CEBlessingReceiverComponent>(player))
            return;

        // Already claimed a blessing from this statue
        if (statue.PlayersBlessed.Contains(player))
            return;

        // Another player is currently active
        if (statue.ActivePlayer is not null && statue.ActivePlayer != player)
            return;

        // Already active for this player (shouldn't double-spawn)
        if (statue.ActivePlayer == player)
            return;

        SpawnBlessings(statueUid, statue, player);
    }

    private void OnTriggerExit(
        Entity<CEBlessingTriggerComponent> ent,
        ref EndCollideEvent args)
    {
        if (args.OurFixtureId != "trigger")
            return;

        if (ent.Comp.LinkedStatue is not { } statueUid)
            return;

        if (!TryComp<CEBlessingStatueComponent>(statueUid, out var statue))
            return;

        var player = args.OtherEntity;

        if (statue.ActivePlayer != player)
            return;

        CleanupBlessings((statueUid, statue));
    }

    private void OnBlessingClaimed(
        Entity<CEBlessingComponent> ent,
        ref CEBlessingClaimedEvent args)
    {
        if (ent.Comp.SourceStatue is not { } statueUid)
            return;

        if (!TryComp<CEBlessingStatueComponent>(statueUid, out var statue))
            return;

        // Mark player as blessed — they can no longer use this statue
        statue.PlayersBlessed.Add(args.Player);

        // Remove all other active blessings (the claimed one is deleted by shared system)
        CleanupBlessings((statueUid, statue));
    }

    private void SpawnBlessings(
        EntityUid statueUid,
        CEBlessingStatueComponent statue,
        EntityUid player)
    {
        statue.ActivePlayer = player;
        statue.ActiveBlessings.Clear();

        foreach (var table in statue.LinkedTables)
        {
            if (!Exists(table))
                continue;

            var coords = Transform(table).Coordinates;
            var blessing = Spawn(statue.BlessingPrototype, coords);

            if (TryComp<CEBlessingComponent>(blessing, out var blessingComp))
            {
                blessingComp.ForPlayer = player;
                blessingComp.SourceStatue = statueUid;
                Dirty(blessing, blessingComp);
            }

            statue.ActiveBlessings.Add(blessing);
        }
    }

    private void CleanupBlessings(Entity<CEBlessingStatueComponent> statue)
    {
        foreach (var blessing in statue.Comp.ActiveBlessings)
        {
            if (Exists(blessing))
                QueueDel(blessing);
        }

        statue.Comp.ActiveBlessings.Clear();
        statue.Comp.ActivePlayer = null;
    }
}
