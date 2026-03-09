using Content.Server._CE.Skills.Blessing.Components;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.Skills.Blessing;

/// <summary>
/// Handles trigger zone collisions (enter/exit), blessing spawning/cleanup,
/// skill selection and proposed-skills tracking.
/// </summary>
public sealed partial class CEBlessingSystem
{
    private const string TriggerFixtureId = "trigger";

    private void InitializeTrigger()
    {
        SubscribeLocalEvent<CEBlessingStatueComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<CEBlessingStatueComponent, EndCollideEvent>(OnTriggerExit);
        SubscribeLocalEvent<CEBlessingComponent, CEBlessingClaimedEvent>(OnBlessingClaimed);
    }

    private void OnTriggerEnter(
        Entity<CEBlessingStatueComponent> ent,
        ref StartCollideEvent args)
    {
        if (args.OurFixtureId != TriggerFixtureId)
            return;

        var player = args.OtherEntity;

        if (!HasComp<CEBlessingReceiverComponent>(player))
            return;

        // Already claimed a blessing from this statue
        if (ent.Comp.PlayersBlessed.Contains(player))
            return;

        // Another player is currently active
        if (ent.Comp.ActivePlayer is not null && ent.Comp.ActivePlayer != player)
            return;

        // Already active for this player (shouldn't double-spawn)
        if (ent.Comp.ActivePlayer == player)
            return;

        SpawnBlessings(ent, player);
    }

    private void OnTriggerExit(
        Entity<CEBlessingStatueComponent> ent,
        ref EndCollideEvent args)
    {
        if (args.OurFixtureId != TriggerFixtureId)
            return;

        var player = args.OtherEntity;

        if (ent.Comp.ActivePlayer != player)
            return;

        // Delete spawned entities but keep OfferedSkills cache for re-entry
        CleanupBlessings(ent);
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
        statue.OfferedSkills.Remove(args.Player);

        // Clear active state (blessing entities already predicted-deleted by shared system)
        statue.ActiveBlessings.Clear();
        statue.ActivePlayer = null;
    }

    private void SpawnBlessings(
        Entity<CEBlessingStatueComponent> statue,
        EntityUid player)
    {
        statue.Comp.ActivePlayer = player;
        statue.Comp.ActiveBlessings.Clear();

        if (!TryComp<CEBlessingReceiverComponent>(player, out var receiver))
            return;

        // Use cached offerings or generate new ones for this statue
        if (!statue.Comp.OfferedSkills.TryGetValue(player, out var skills))
        {
            skills = new List<ProtoId<CESkillPrototype>>();

            foreach (var _ in statue.Comp.LinkedTables)
            {
                var skill = GetNextSkill((player, receiver), skills);
                if (skill is not null)
                    skills.Add(skill.Value);
            }

            statue.Comp.OfferedSkills[player] = skills;

            // Immediately mark all generated skills as proposed to prevent
            // other statues from duplicating them this session.
            var dirty = false;
            foreach (var skill in skills)
            {
                if (!receiver.ProposedSkills.Contains(skill))
                {
                    receiver.ProposedSkills.Add(skill);
                    dirty = true;
                }
            }

            if (dirty)
                Dirty(player, receiver);
        }

        var blessingEntities = new List<EntityUid>();
        var tableIndex = 0;

        foreach (var table in statue.Comp.LinkedTables)
        {
            if (!Exists(table))
            {
                tableIndex++;
                continue;
            }

            if (tableIndex >= skills.Count)
                break;

            var skill = skills[tableIndex];
            var coords = Transform(table).Coordinates;
            var blessing = Spawn(statue.Comp.BlessingPrototype, coords);

            if (TryComp<CEBlessingComponent>(blessing, out var blessingComp))
            {
                blessingComp.ForPlayer = player;
                blessingComp.SourceStatue = statue.Owner;
                blessingComp.Skill = skill;
                // Set entity name to the skill name so it shows correctly on examine
                _metaData.SetEntityName(blessing, _skill.GetSkillName(skill));
            }

            blessingEntities.Add(blessing);
            statue.Comp.ActiveBlessings.Add(blessing);
            tableIndex++;
        }

        // Cross-reference siblings so shared system can predicted-delete all at once
        foreach (var blessingUid in blessingEntities)
        {
            if (!TryComp<CEBlessingComponent>(blessingUid, out var comp))
                continue;

            foreach (var other in blessingEntities)
            {
                if (other != blessingUid)
                    comp.SiblingBlessings.Add(other);
            }

            Dirty(blessingUid, comp);
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
        // NOTE: OfferedSkills NOT cleared — same skills reappear on re-entry
    }

    /// <summary>
    /// Picks the next skill for the player, filtering already-learned, already-in-batch,
    /// and proposed skills. Clears proposed list if no candidates remain.
    /// </summary>
    private ProtoId<CESkillPrototype>? GetNextSkill(
        Entity<CEBlessingReceiverComponent> receiver,
        List<ProtoId<CESkillPrototype>> alreadyPicked)
    {
        var candidates = GetSkillCandidates(receiver, alreadyPicked, filterProposed: true);

        if (candidates.Count == 0)
        {
            // Pool exhausted — clear proposed list and retry
            receiver.Comp.ProposedSkills.Clear();
            Dirty(receiver);
            candidates = GetSkillCandidates(receiver, alreadyPicked, filterProposed: false);
        }

        if (candidates.Count == 0)
        {
            // Still exhausted after clearing proposed — no valid skills remain
            return null;
        }

        return _random.Pick(candidates);
    }

    private List<ProtoId<CESkillPrototype>> GetSkillCandidates(
        Entity<CEBlessingReceiverComponent> receiver,
        List<ProtoId<CESkillPrototype>> alreadyPicked,
        bool filterProposed)
    {
        var candidates = new List<ProtoId<CESkillPrototype>>();

        foreach (var proto in _proto.EnumeratePrototypes<CESkillPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (_skill.HaveSkill(receiver, proto))
                continue;

            if (alreadyPicked.Contains(proto.ID))
                continue;

            if (filterProposed && receiver.Comp.ProposedSkills.Contains(proto.ID))
                continue;

            if (!CheckRestrictions(proto, receiver))
                continue;

            candidates.Add(proto.ID);
        }

        return candidates;
    }

    private bool CheckRestrictions(CESkillPrototype proto, EntityUid player)
    {
        foreach (var restriction in proto.Restrictions)
        {
            if (!restriction.Check(EntityManager, player))
                return false;
        }

        return true;
    }
}
