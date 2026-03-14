using Content.Client.Stylesheets;
using Content.Editor.UI;
using JetBrains.Annotations;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;

namespace Content.Editor;

[UsedImplicitly]
public sealed class EditorEntryPoint : GameClient
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IStylesheetManager _stylesheetManager = default!; //For some reason - if we remove it, editor crashes
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

    public override void Init()
    {
        base.Init();
        Dependencies.BuildGraph();
        Dependencies.InjectDependencies(this);
    }

    public override void PostInit()
    {
        base.PostInit();

        _client.StartSinglePlayer();
        _uiManager.MainViewport.Visible = false;
        _stateManager.RequestStateChange<EditorState>();
    }
}
