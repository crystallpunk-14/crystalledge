using Content.Shared._CE.Procedural.Admin;
using Robust.Client.UserInterface;

namespace Content.Client._CE.Procedural.Admin;

public sealed class CEAdminDungeonOverviewBoundUserInterface : BoundUserInterface
{
    private CEAdminDungeonOverviewWindow? _window;

    public CEAdminDungeonOverviewBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CEAdminDungeonOverviewWindow>();
        _window.OnTeleportRequested += target => SendMessage(new CEAdminDungeonOverviewTeleportMsg(target));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CEAdminDungeonOverviewState overviewState)
            _window?.UpdateState(overviewState);
    }
}
