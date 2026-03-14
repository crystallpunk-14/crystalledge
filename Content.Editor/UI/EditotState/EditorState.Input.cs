using Robust.Client.Input;
using Robust.Shared.Input.Binding;

namespace Content.Editor.UI.EditotState;

public sealed partial class EditorState
{
    [Dependency] private readonly IInputManager _inputManager = default!;

    private void StartupInput()
    {
        // Wire keybinds
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorSave,
            InputCmdHandler.FromDelegate(_ => OnSaveRequested()));
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorUndo,
            InputCmdHandler.FromDelegate(_ => OnUndoRequested()));
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorRedo,
            InputCmdHandler.FromDelegate(_ => OnRedoRequested()));
    }

    private void ShutdownInput()
    {
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorSave, null);
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorUndo, null);
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorRedo, null);
    }

    private void OnSaveRequested()
    {
        var session = _editorView.CurrentSession;
        if (session == null || !session.IsDirty)
            return;

        // TODO: Write changes back to YAML file on disk
        // For now, just mark as saved and clear dirty state
        session.MarkSaved();
        _statusBar.ShowMessage("Saved (in-memory only — file write not yet implemented)");
    }

    private void OnUndoRequested()
    {
        if (_editorView.Undo())
            _statusBar.ShowMessage("Undo");
    }

    private void OnRedoRequested()
    {
        if (_editorView.Redo())
            _statusBar.ShowMessage("Redo");
    }
}
