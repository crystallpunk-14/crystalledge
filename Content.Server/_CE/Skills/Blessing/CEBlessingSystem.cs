using System.Linq;
using Content.Server._CE.Skills.Blessing.Components;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core;
using Content.Shared._CE.Skill.Core.Components;
using Content.Shared._CE.Skill.Core.Effects;
using Content.Shared._CE.Skill.Core.Prototypes;
using Robust.Shared.Physics.Events;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Utility;

namespace Content.Server._CE.Skills.Blessing;

public sealed partial class CEBlessingSystem : CESharedBlessingSystem
{
    [Dependency] private readonly IPrototypeManager _proto = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly CESharedSkillSystem _skill = default!;
    [Dependency] private readonly MetaDataSystem _metaData = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBlessingStatueComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<CEBlessingStatueComponent, EndCollideEvent>(OnTriggerExit);
        SubscribeLocalEvent<CEBlessingComponent, CEBlessingClaimedEvent>(OnBlessingClaimed);
    }

    private void OnTriggerEnter(
        Entity<CEBlessingStatueComponent> ent,
        ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.TriggerFixtureId)
            return;

        if (!ent.Comp.StatueInitialized)
        {
            var entities = new HashSet<EntityUid>();
            _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.LinkRadius, entities);

            foreach (var uid in entities)
            {
                if (HasComp<CEBlessingTableComponent>(uid))
                    ent.Comp.LinkedTables.Add(uid);
            }

            ent.Comp.StatueInitialized = true;
        }

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
        if (args.OurFixtureId != ent.Comp.TriggerFixtureId)
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

        // We dont have cached offering, so generate new ones for this statue & player
        if (!statue.Comp.OfferedSkills.TryGetValue(player, out var statueOffering))
        {
            statueOffering = new List<ProtoId<CESkillPrototype>>();

            foreach (var _ in statue.Comp.LinkedTables)
            {
                var skill = GetNextSkill((player, receiver), statueOffering);
                if (skill is not null)
                    statueOffering.Add(skill.Value);
            }

            statue.Comp.OfferedSkills[player] = statueOffering;

            // Immediately mark all generated skills as proposed to prevent
            // other statues from duplicating them this session.
            var dirty = false;
            foreach (var skill in statueOffering)
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

            if (tableIndex >= statueOffering.Count)
                break;

            var skill = statueOffering[tableIndex];
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
        List<ProtoId<CESkillPrototype>> alreadyOfferedByStatue)
    {
        if (!TryComp<CESkillStorageComponent>(receiver.Owner, out var storage))
            return null;

        var candidates = GetSkillCandidates((receiver, receiver.Comp, storage), alreadyOfferedByStatue);

        if (candidates.Count == 0)
        {
            // Pool exhausted — clear proposed list and retry
            receiver.Comp.ProposedSkills.Clear();
            Dirty(receiver);
            candidates = GetSkillCandidates((receiver, receiver.Comp, storage), alreadyOfferedByStatue);
        }

        if (candidates.Count == 0)
        {
            // Still exhausted after clearing proposed — no valid skills remain
            return null;
        }

        // Weighted random selection: skills with higher Weight appear more often
        var totalWeight = candidates.Sum(c => c.Weight);
        var roll = _random.NextFloat() * totalWeight;
        var accumulated = 0f;
        foreach (var candidate in candidates)
        {
            accumulated += candidate.Weight;
            if (roll < accumulated)
                return candidate.ID;
        }

        return candidates[^1].ID;
    }

    private List<CESkillPrototype> GetSkillCandidates(
        Entity<CEBlessingReceiverComponent, CESkillStorageComponent> receiver,
        List<ProtoId<CESkillPrototype>> alreadyOfferedByStatue)
    {
        var candidates = new List<CESkillPrototype>();

        var filterType = receiver.Comp1.SkillTypeOrder.TryGetValue(receiver.Comp2.LearnedSkills.Count, out var filterActives);

        foreach (var proto in _proto.EnumeratePrototypes<CESkillPrototype>())
        {
            if (proto.Abstract)
                continue;

            if (filterType)
            {
                if (filterActives && proto.Effect is not AddAction)
                    continue;

                if (!filterActives && proto.Effect is not AddStatusEffect)
                    continue;
            }

            if (proto.Unique && _skill.HaveSkill(receiver, proto))
                continue;

            if (alreadyOfferedByStatue.Contains(proto.ID))
                continue;

            if (receiver.Comp1.ProposedSkills.Contains(proto.ID))
                continue;

            var restrictionPassed = true;
            foreach (var restriction in proto.Restrictions)
            {
                if (!restriction.Check(EntityManager, receiver))
                {
                    restrictionPassed = false;
                    break;
                }
            }

            if (!restrictionPassed)
                continue;


            candidates.Add(proto);
        }

        return candidates;
    }
}
