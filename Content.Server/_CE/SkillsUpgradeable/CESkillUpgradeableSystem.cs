using System.Linq;
using Content.Server._CE.Skills;
using Content.Shared._CE.Skills.Prototypes;
using Content.Shared._CE.SkillsUpgrade;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._CE.SkillsUpgradeable;

public sealed partial class CESkillUpgradeableSystem : CESharedSkillUpgradeableSystem
{
    [Dependency] private readonly CESkillSystem _skill = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CESkillUpgradeableComponent, MapInitEvent>(OnMapInit);

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

    private void OnMapInit(Entity<CESkillUpgradeableComponent> ent, ref MapInitEvent args)
    {
        RepopulatePossibleSkills(ent);
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
        ent.Comp.PossibleSkills = _skill.GetLearnableSkills(ent.Owner);

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
}
