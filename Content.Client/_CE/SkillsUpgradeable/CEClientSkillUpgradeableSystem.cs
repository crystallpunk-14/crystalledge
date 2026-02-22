using Content.Client._CE.SkillsUpgradeable.UI;
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

        SubscribeLocalEvent<CESkillUpgradeableComponent, CESkillUpgradeAlertEvent>(OnAlertClicked);
        SubscribeLocalEvent<CESkillUpgradeableComponent, ComponentShutdown>(OnShutdown);
    }

    private void OnAlertClicked(Entity<CESkillUpgradeableComponent> ent, ref CESkillUpgradeAlertEvent args)
    {
        args.Handled = true;
        OpenWindow(ent);
    }

    private void OnShutdown(Entity<CESkillUpgradeableComponent> ent, ref ComponentShutdown args)
    {
        CloseWindow();
    }

    public void OpenWindow(Entity<CESkillUpgradeableComponent> target)
    {
        if (target.Comp.CurrentUpgradeSelection.Count == 0)
            return;

        CloseWindow();

        _window = new CESkillUpgradeWindow();
        _window.OnSkillSelected += skill => RequestLearnSkill(target, skill);
        _window.OnClose += CloseWindow;
        _window.Populate(target.Comp.CurrentUpgradeSelection);
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

    public void RequestLearnSkill(Entity<CESkillUpgradeableComponent> target, ProtoId<CESkillPrototype> skill)
    {
        if (!target.Comp.CurrentUpgradeSelection.Contains(skill))
            return;

        var netEv = new CETryLearnSkillMessage(GetNetEntity(target), skill);
        RaiseNetworkEvent(netEv);
        CloseWindow();
    }
}
