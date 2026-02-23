using System.Linq;
using Content.Server._CE.Skills;
using Content.Shared._CE.Skill.Core.Components;
using Content.Shared._CE.Skill.Core.Prototypes;
using Content.Shared._CE.Skill.Upgrade;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.SkillsUpgradeable;

public sealed partial class CESkillUpgradeableSystem : CESharedSkillUpgradeableSystem
{
    [Dependency] private readonly CESkillSystem _skill = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _proto = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<CETryLearnSkillMessage>(OnClientRequestLearnSkill);
    }

    private void OnClientRequestLearnSkill(CETryLearnSkillMessage ev, EntitySessionEventArgs args)
    {
        var entity = GetEntity(ev.Entity);

        if (args.SenderSession.AttachedEntity != entity)
            return;

        if (!TryComp<CESkillUpgradeableComponent>(entity, out var upgradeComp))
            return;

        if (!upgradeComp.CurrentUpgradeSelection.Contains(ev.Skill))
            return;

        if (upgradeComp.PendingLevels <= 0)
            return;

        if (!_skill.TryAddSkill(entity, ev.Skill))
            return;

        upgradeComp.Level++;
        upgradeComp.PendingLevels--;
        Dirty(entity, upgradeComp);

        // If there are still pending levels, reroll for the next one
        if (upgradeComp.PendingLevels > 0)
        {
            RerollSelection((entity, upgradeComp));
        }
        else
        {
            ClearSelection((entity, upgradeComp));
        }
    }

    /// <summary>
    /// Triggers a level up for the target entity, giving them skill upgrade options.
    /// Stacks with existing pending levels.
    /// </summary>
    public void TriggerLevelUp(Entity<CESkillUpgradeableComponent> ent)
    {
        ent.Comp.PendingLevels++;

        // Only reroll if there's no active selection already
        if (ent.Comp.CurrentUpgradeSelection.Count == 0)
            RerollSelection(ent);
        else
            Dirty(ent);
    }

    private void RerollSelection(Entity<CESkillUpgradeableComponent> ent)
    {
        ent.Comp.CurrentUpgradeSelection.Clear();

        var availableSkills = ent.Comp.PossibleSkills.Count;
        if (availableSkills == 0)
        {
            RepopulatePossibleSkills(ent);
            availableSkills = ent.Comp.PossibleSkills.Count;
        }
        var targetSelectionCount = Math.Min(ent.Comp.MaxUpgradeSelection, availableSkills);
        while (ent.Comp.CurrentUpgradeSelection.Count < targetSelectionCount)
        {
            var skill = GetNextSkill(ent);
            ent.Comp.CurrentUpgradeSelection.Add(skill);
        }

        Dirty(ent);
        EnableUpgradeAlert(ent);
    }

    private void ClearSelection(Entity<CESkillUpgradeableComponent> ent)
    {
        ent.Comp.CurrentUpgradeSelection.Clear();
        Dirty(ent);
        DisableUpgradeAlert(ent);
    }

    private void RepopulatePossibleSkills(Entity<CESkillUpgradeableComponent> ent)
    {
        ent.Comp.PossibleSkills = GetLearnableSkills(ent.Owner);

        // Remove skills that are already in the current selection
        ent.Comp.PossibleSkills.RemoveAll(s => ent.Comp.CurrentUpgradeSelection.Contains(s));

        ent.Comp.PossibleSkills.Shuffle();
        Dirty(ent);
    }

    private ProtoId<CESkillPrototype> GetNextSkill(Entity<CESkillUpgradeableComponent> ent)
    {
        if (ent.Comp.PossibleSkills.Count == 0)
            RepopulatePossibleSkills(ent);
        if (ent.Comp.PossibleSkills.Count == 0)
            Log.Error($"No skills available to learn for {ent.Owner}.");

        var skill = _random.PickAndTake(ent.Comp.PossibleSkills);
        Dirty(ent);
        return skill;
    }

    /// <summary>
    ///  Checks if the player can learn the specified skill.
    /// </summary>
    public bool CanLearnSkill(
        EntityUid target,
        CESkillPrototype skill,
        CESkillStorageComponent? component = null)
    {
        if (!Resolve(target, ref component, false))
            return false;

        // Check if already learned
        if (_skill.HaveSkill(target, skill, component))
            return false;

        //Restrictions check
        foreach (var req in skill.Restrictions)
        {
            if (!req.Check(EntityManager, target))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Returns a list of all skills the entity can currently learn.
    /// </summary>
    public List<ProtoId<CESkillPrototype>> GetLearnableSkills(Entity<CESkillStorageComponent?> ent)
    {
        var skills = new List<ProtoId<CESkillPrototype>>();

        if (!Resolve(ent, ref ent.Comp, false))
            return skills;

        foreach (var skill in _proto.EnumeratePrototypes<CESkillPrototype>())
        {
            if (!CanLearnSkill(ent.Owner, skill, ent.Comp))
                continue;

            skills.Add(skill);
        }

        return skills;
    }
}
