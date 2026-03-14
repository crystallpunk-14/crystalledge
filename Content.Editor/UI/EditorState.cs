using System.IO;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Input.Binding;
using Robust.Shared.Prototypes;

namespace Content.Editor.UI;

/// <summary>
/// Main editor state that assembles four independent UI controls
/// (PrototypeBrowser, TabBar, EditorView, StatusBar) and wires them
/// together through events.
/// </summary>
public sealed class EditorState : State
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

    private BoxContainer _root = default!;
    private PrototypeBrowserControl _browser = default!;
    private TabBarControl _tabBar = default!;
    private EditorViewControl _editorView = default!;
    private StatusBarControl _statusBar = default!;

    protected override void Startup()
    {
        _browser = new PrototypeBrowserControl();
        _tabBar = new TabBarControl();
        _editorView = new EditorViewControl();
        _statusBar = new StatusBarControl();

        // Right side: tabs + editor + status
        var rightColumn = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rightColumn.AddChild(_tabBar);
        rightColumn.AddChild(_editorView);
        rightColumn.AddChild(_statusBar);

        // Root layout: [sidebar | right]
        _root = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        _root.AddChild(_browser);
        _root.AddChild(rightColumn);

        _uiManager.StateRoot.AddChild(_root);
        LayoutContainer.SetAnchorPreset(_root, LayoutContainer.LayoutPreset.Wide);

        // Wire events
        _browser.OnFileSelected += OnFileSelected;
        _tabBar.OnTabSelected += OnFileSelected;
        _tabBar.OnTabClosed += OnTabClosed;
        _editorView.OnDirtyChanged += OnDirtyChanged;
        _prototypeManager.PrototypesReloaded += OnPrototypesReloaded;

        // Wire keybinds
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorSave,
            InputCmdHandler.FromDelegate(_ => OnSaveRequested()));
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorUndo,
            InputCmdHandler.FromDelegate(_ => OnUndoRequested()));
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorRedo,
            InputCmdHandler.FromDelegate(_ => OnRedoRequested()));
    }

    protected override void Shutdown()
    {
        _browser.OnFileSelected -= OnFileSelected;
        _tabBar.OnTabSelected -= OnFileSelected;
        _tabBar.OnTabClosed -= OnTabClosed;
        _editorView.OnDirtyChanged -= OnDirtyChanged;

        _inputManager.SetInputCommand(EditorKeyFunctions.EditorSave, null);
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorUndo, null);
        _inputManager.SetInputCommand(EditorKeyFunctions.EditorRedo, null);

        _prototypeManager.PrototypesReloaded -= OnPrototypesReloaded;

        _uiManager.StateRoot.RemoveChild(_root);
    }

    // ── File navigation ──

    private void OnFileSelected(string filePath)
    {
        _tabBar.OpenTab(filePath);
        _browser.SetSelectedFile(filePath);

        try
        {
            _editorView.OpenFile(filePath);
            UpdateStatusForFile(filePath);
        }
        catch (Exception ex)
        {
            _statusBar.ShowError(ex.Message);
        }
    }

    private void OnTabClosed(string filePath)
    {
        _tabBar.CloseTab(filePath);
        _editorView.CloseSession(filePath);

        var next = _tabBar.ActivePath;
        if (next != null)
        {
            OnFileSelected(next);
        }
        else
        {
            _editorView.Clear();
            _browser.SetSelectedFile(null);
            _statusBar.ShowMessage("Ready");
        }
    }

    // ── Dirty state ──

    private void OnDirtyChanged(string filePath, bool isDirty)
    {
        // Update tab display to show dirty indicator
        _tabBar.SetDirty(filePath, isDirty);
        UpdateStatusForFile(filePath);
    }

    // ── Keybind handlers ──

    private void OnSaveRequested()
    {
        var session = _editorView.CurrentSession;
        if (session == null || !session.IsDirty)
            return;

        if (PrototypeYamlWriter.Save(session.FilePath, session))
        {
            session.MarkSaved();

            // Rebuild cards from the current in-memory prototypes (which already
            // have the correct values).  Do NOT re-parse from disk because the
            // prototype manager's resolved mapping is stale until a hot-reload.
            _editorView.RefreshCards();
            _statusBar.ShowMessage($"Saved {Path.GetFileName(session.FilePath)}");
        }
        else
        {
            _statusBar.ShowError("Failed to save file");
        }
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

    // ── Status bar ──

    private void UpdateStatusForFile(string filePath)
    {
        var fileName = Path.GetFileName(filePath);
        var session = _editorView.GetSession(filePath);
        var dirty = session?.IsDirty == true ? " *" : "";
        _statusBar.ShowMessage($"{fileName}{dirty}");
    }

    // ── Prototype reload ──

    /// <summary>
    /// Called by the engine when prototypes are hot-reloaded.
    /// Refreshes the currently open file if its prototypes were affected.
    /// </summary>
    private void OnPrototypesReloaded(PrototypesReloadedEventArgs args)
    {
        if (CurrentPath == null)
            return;

        // Refresh the current view so it picks up any reloaded definitions
        var session = _editorView.CurrentSession;
        if (session != null && !session.IsDirty)
        {
            _editorView.OpenFile(CurrentPath);
        }
    }

    private string? CurrentPath => _editorView.CurrentPath;
}
