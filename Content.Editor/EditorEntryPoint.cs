using Content.Client.Stylesheets;
using Content.Editor.UI;
using JetBrains.Annotations;
using Robust.Client;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;

namespace Content.Editor;

[UsedImplicitly]
public sealed class EditorEntryPoint : GameClient
{
    [Dependency] private readonly IBaseClient _client = default!;
    [Dependency] private readonly IStateManager _stateManager = default!;
    [Dependency] private readonly IStylesheetManager _stylesheetManager = default!; //For some reason - if we remove it, editor crashes
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    public override void Init()
    {
        base.Init();
        Dependencies.BuildGraph();
        Dependencies.InjectDependencies(this);
    }

    public override void PostInit()
    {
        base.PostInit();

        // Register editor keybindings
        var ctx = _inputManager.Contexts.GetContext("common");
        ctx.AddFunction(EditorKeyFunctions.EditorSave);
        ctx.AddFunction(EditorKeyFunctions.EditorUndo);
        ctx.AddFunction(EditorKeyFunctions.EditorRedo);

        _inputManager.RegisterBinding(new KeyBindingRegistration
        {
            Function = EditorKeyFunctions.EditorSave,
            Type = KeyBindingType.State,
            BaseKey = Keyboard.Key.S,
            Mod1 = Keyboard.Key.Control,
        });
        _inputManager.RegisterBinding(new KeyBindingRegistration
        {
            Function = EditorKeyFunctions.EditorUndo,
            Type = KeyBindingType.State,
            BaseKey = Keyboard.Key.Z,
            Mod1 = Keyboard.Key.Control,
        });
        _inputManager.RegisterBinding(new KeyBindingRegistration
        {
            Function = EditorKeyFunctions.EditorRedo,
            Type = KeyBindingType.State,
            BaseKey = Keyboard.Key.Y,
            Mod1 = Keyboard.Key.Control,
        });

        _client.StartSinglePlayer();
        _uiManager.MainViewport.Visible = false;
        _stateManager.RequestStateChange<EditorState>();
    }
}
