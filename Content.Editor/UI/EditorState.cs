using System.IO;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI;

/// <summary>
/// Main editor state that assembles four independent UI controls
/// (PrototypeBrowser, TabBar, EditorView, StatusBar) and wires them
/// together through events.
/// </summary>
public sealed class EditorState : State
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;

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
    }

    protected override void Shutdown()
    {
        _browser.OnFileSelected -= OnFileSelected;
        _tabBar.OnTabSelected -= OnFileSelected;
        _tabBar.OnTabClosed -= OnTabClosed;
        _uiManager.StateRoot.RemoveChild(_root);
    }

    /// <summary>
    /// Opens a file in the editor: creates/activates tab, shows content, updates status.
    /// </summary>
    private void OnFileSelected(string filePath)
    {
        _tabBar.OpenTab(filePath);
        _browser.SetSelectedFile(filePath);

        try
        {
            _editorView.OpenFile(filePath);
            var fileName = Path.GetFileName(filePath);
            var lineCount = File.ReadAllLines(filePath).Length;
            _statusBar.ShowMessage($"{fileName} - {lineCount} lines");
        }
        catch (Exception ex)
        {
            _statusBar.ShowError(ex.Message);
        }
    }

    private void OnTabClosed(string filePath)
    {
        _tabBar.CloseTab(filePath);

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
}
