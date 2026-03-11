using Content.Client.Stylesheets;
using Content.Editor.UI;
using JetBrains.Annotations;
using Robust.Client;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.Configuration;
using Robust.Shared.ContentPack;

namespace Content.Editor;

/// <summary>
/// Entry point for the SS14 Prototype Editor application.
/// The engine automatically discovers and runs Content.Client's EntryPoint first,
/// which handles IoC registration, component auto-registration, and prototype ignores.
/// This entry point runs after that and switches to the editor UI.
/// </summary>
[UsedImplicitly]
public sealed class EditorEntryPoint : GameClient
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IStylesheetManager _stylesheetManager = default!;
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IConfigurationManager _configManager = default!;

    public override void Init()
    {
        base.Init();
        Dependencies.BuildGraph();
        Dependencies.InjectDependencies(this);
    }

    public override void PostInit()
    {
        base.PostInit();

        // Start in single-player mode (no server connection needed)
        _client.StartSinglePlayer();

        // Hide the main game viewport — we only show editor UI
        _uiManager.MainViewport.Visible = false;

        // Switch to the editor main screen
        _stateManager.RequestStateChange<EditorMainScreen>();
    }
}
