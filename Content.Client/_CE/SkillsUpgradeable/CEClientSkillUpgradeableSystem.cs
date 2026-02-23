using Content.Client._CE.SkillsUpgradeable.UI;
using Content.Shared._CE.Skill.Upgrade;
using Content.Shared._CE.Skills.Prototypes;
using Content.Shared._CE.SkillsUpgrade;
using Robust.Shared.Prototypes;

namespace Content.Client._CE.SkillsUpgradeable;

public sealed partial class CEClientSkillUpgradeableSystem : CESharedSkillUpgradeableSystem
{
    private CESkillUpgradeWindow? _window;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent, Shared._CE.Skill.Upgrade.CESkillUpgradeAlertEvent>(OnAlertClicked);
        SubscribeLocalEvent<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent, AfterAutoHandleStateEvent>(OnStateUpdated);
    }

    private void OnStateUpdated(Entity<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        // If the window is open and we got a state update, refresh the window contents
        if (_window is not { IsOpen: true })
            return;

        if (ent.Comp.CurrentUpgradeSelection.Count > 0)
        {
            _window.Populate(ent.Comp.CurrentUpgradeSelection, ent.Comp.Level + 1);
        }
        else
        {
            CloseWindow();
        }
    }

    private void OnAlertClicked(Entity<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent> ent, ref Shared._CE.Skill.Upgrade.CESkillUpgradeAlertEvent args)
    {
        args.Handled = true;
        OpenWindow(ent);
    }

    private void OnShutdown(Entity<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent> ent, ref ComponentShutdown args)
    {
        CloseWindow();
    }

    public void OpenWindow(Entity<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent> target)
    {
        if (target.Comp.CurrentUpgradeSelection.Count == 0)
            return;

        CloseWindow();

        _window = new CESkillUpgradeWindow();
        _window.OnSkillSelected += skill => RequestLearnSkill(target, skill);
        _window.OnClose += CloseWindow;
        _window.Populate(target.Comp.CurrentUpgradeSelection, target.Comp.Level + 1);
        _window.OpenCentered();
    }

    public void CloseWindow()
    {
        if (_window == null)
            return;

        if (_window.IsOpen)
            _window.Close();

        _window = null;
    }

    public void RequestLearnSkill(Entity<Shared._CE.Skill.Upgrade.CESkillUpgradeableComponent> target, ProtoId<CESkillPrototype> skill)
    {
        if (!target.Comp.CurrentUpgradeSelection.Contains(skill))
            return;

        var netEv = new CETryLearnSkillMessage(GetNetEntity(target), skill);
        RaiseNetworkEvent(netEv);
        // Window will be refreshed or closed by OnStateUpdated when server responds
    }
}
