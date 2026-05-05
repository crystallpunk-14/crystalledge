using System.Linq;
using Content.Server._CE.Skills.Blessing.Components;
using Content.Shared._CE.Skill.Blessing;
using Content.Shared._CE.Skill.Blessing.Components;
using Content.Shared._CE.Skill.Core;
using Content.Shared._CE.Skill.Core.Components;
using Content.Shared._CE.Skill.Core.Effects;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared._CE.Soul;
using Content.Shared._CE.Soul.Components;
using Content.Shared.Interaction;
using Content.Shared.Popups;
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
    [Dependency] private readonly CESharedSoulSystem _souls = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CEBlessingStatueComponent, ActivateInWorldEvent>(OnActivate);
        SubscribeLocalEvent<CEBlessingStatueComponent, CESoulSpentEvent>(OnSoulReceived);
        SubscribeLocalEvent<CEBlessingStatueComponent, StartCollideEvent>(OnTriggerEnter);
        SubscribeLocalEvent<CEBlessingStatueComponent, EndCollideEvent>(OnTriggerExit);
        SubscribeLocalEvent<CEBlessingComponent, CEBlessingClaimedEvent>(OnBlessingClaimed);
    }

    private void OnActivate(Entity<CEBlessingStatueComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        var player = args.User;

        if (!HasComp<CEBlessingReceiverComponent>(player))
            return;

        // Statue is currently displaying blessings to a different player — busy.
        // Lock is based on whether blessings are visibly offered (ActivePlayer != null),
        // not on cached offerings, so other players can use the statue while this
        // player's offer is dormant (they walked away without picking a skill).
        if (ent.Comp.ActivePlayer is { } active && active != player)
        {
            _popup.PopupEntity(Loc.GetString("ce-blessing-statue-busy"), ent, player);
            return;
        }

        EnsureLinkedTables(ent);

        // Re-entry path: this player walked away earlier without picking a skill,
        // their offer is still cached. Re-show it without charging again.
        if (ent.Comp.OfferedSkills.ContainsKey(player))
        {
            // Already showing for this player — clicking again is a no-op so the
            // statue doesn't keep stacking duplicate blessing entities.
            if (ent.Comp.ActivePlayer == player && ent.Comp.ActiveBlessings.Count > 0)
            {
                args.Handled = true;
                return;
            }

            ent.Comp.ActivePlayer = player;
            args.Handled = true;
            SpawnBlessings(ent, player);
            return;
        }

        // Fresh interaction: charge souls. The soul system starts a delayed transfer
        // (animation) and raises CESoulReceivedEvent -> OnSoulReceived -> SpawnBlessings
        // only after the animation finishes. We tentatively lock the statue to this
        // player up-front so concurrent clicks from other players see "busy" while
        // the animation plays; we revert the lock if TrySpendSouls fails.
        if (!HasComp<CESoulReceiverComponent>(ent))
            return;

        var previousActive = ent.Comp.ActivePlayer;
        ent.Comp.ActivePlayer = player;

        if (_souls.TrySpendSouls(ent.Owner, player))
            args.Handled = true;
        else
            ent.Comp.ActivePlayer = previousActive;
    }

    private void OnSoulReceived(
        Entity<CEBlessingStatueComponent> ent,
        ref CESoulSpentEvent args)
    {
        // ActivePlayer was already set by OnActivate to keep the statue locked during
        // the soul-transfer animation. By the time this event fires the lock should
        // already match args.Player.
        SpawnBlessings(ent, args.Player);
    }

    private void EnsureLinkedTables(Entity<CEBlessingStatueComponent> ent)
    {
        if (ent.Comp.StatueInitialized)
            return;

        var entities = new HashSet<EntityUid>();
        _lookup.GetEntitiesInRange(ent.Owner, ent.Comp.LinkRadius, entities);

        foreach (var uid in entities)
        {
            if (HasComp<CEBlessingTableComponent>(uid))
                ent.Comp.LinkedTables.Add(uid);
        }

        ent.Comp.StatueInitialized = true;
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

        // Don't tear down state while the soul system's transfer animation is still
        // running on the player — let it finish and SpawnBlessings on completion.
        // The statue stays locked to this player throughout the animation so other
        // players can't barge in.
        if (HasComp<CESoulTransferComponent>(player))
            return;

        // Delete spawned entities but keep OfferedSkills cache for re-entry
        CleanupBlessings(ent);
    }

    /// <summary>
    /// Re-entry trigger: a player who already paid (cached offering exists) walks back
    /// into proximity. Re-show their blessings automatically without charging — only if
    /// the statue is currently free.
    /// </summary>
    private void OnTriggerEnter(
        Entity<CEBlessingStatueComponent> ent,
        ref StartCollideEvent args)
    {
        if (args.OurFixtureId != ent.Comp.TriggerFixtureId)
            return;

        var player = args.OtherEntity;

        // Only re-show if this player has a cached offer.
        if (!ent.Comp.OfferedSkills.ContainsKey(player))
            return;

        // Statue is currently busy — don't override (also covers the same player still
        // having the blessings spawned, in which case ActivePlayer == player already).
        if (ent.Comp.ActivePlayer is not null)
            return;

        ent.Comp.ActivePlayer = player;
        SpawnBlessings(ent, player);
    }

    private void OnBlessingClaimed(
        Entity<CEBlessingComponent> ent,
        ref CEBlessingClaimedEvent args)
    {
        if (ent.Comp.SourceStatue is not { } statueUid)
            return;

        if (!TryComp<CEBlessingStatueComponent>(statueUid, out var statue))
            return;

        if (ent.Comp.Skill is { } chosenSkill)
            TrackChosen(args.Player, chosenSkill);

        // Clear the offer cache for this player and free the statue so it (and the
        // player) can be used again. Statues are reusable as long as the player
        // keeps paying souls.
        statue.OfferedSkills.Remove(args.Player);
        statue.ActiveBlessings.Clear();
        statue.ActivePlayer = null;
    }

    private void SpawnBlessings(
        Entity<CEBlessingStatueComponent> statue,
        EntityUid player)
    {
        statue.Comp.ActivePlayer = player;

        // Defensive: despawn any leftover blessings to keep the function idempotent.
        // Without this, calling SpawnBlessings while entities are still alive would
        // leak them and stack duplicate visuals on the pedestals.
        foreach (var leftover in statue.Comp.ActiveBlessings)
        {
            if (Exists(leftover))
                QueueDel(leftover);
        }
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
            TrackOffered(player, statueOffering);

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

        // The soul-transfer animation may have finished while the player was running away.
        // If they are now outside the trigger radius, cache the offering (so re-entry still
        // works) but do NOT spawn blessing entities — they would be unreachable and stick
        // around forever. Free the statue so other players can use it.
        var statuePos = Transform(statue.Owner).MapPosition;
        var playerPos = Transform(player).MapPosition;
        if (statuePos.MapId != playerPos.MapId ||
            (statuePos.Position - playerPos.Position).Length() > statue.Comp.TriggerRadius)
        {
            statue.Comp.ActivePlayer = null;
            return;
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
