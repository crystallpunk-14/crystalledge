using Content.Shared._CE.Procedural.Overview;
using Robust.Client.UserInterface;

namespace Content.Client._CE.Procedural.Overview;

public sealed class CEPlayerDungeonOverviewBoundUserInterface : BoundUserInterface
{
    private CEPlayerDungeonOverviewWindow? _window;

    public CEPlayerDungeonOverviewBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CEPlayerDungeonOverviewWindow>();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CEPlayerDungeonOverviewState s)
            _window?.UpdateState(s);
    }
}
