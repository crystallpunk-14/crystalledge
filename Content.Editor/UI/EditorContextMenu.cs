using Robust.Client.Graphics;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI;

/// <summary>
/// A simple right-click context menu for the editor.
/// Uses a Popup with styled Button items that dismiss on click.
/// </summary>
public sealed class EditorContextMenu : Popup
{
    private readonly BoxContainer _itemContainer;

    public EditorContextMenu()
    {
        var panel = new PanelContainer();
        panel.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = EditorMainControl.BgTertiary,
            BorderColor = EditorMainControl.Border,
            BorderThickness = new Thickness(1),
            ContentMarginLeftOverride = 2,
            ContentMarginRightOverride = 2,
            ContentMarginTopOverride = 4,
            ContentMarginBottomOverride = 4,
        };

        _itemContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 0,
        };

        panel.AddChild(_itemContainer);
        AddChild(panel);
    }

    /// <summary>
    /// Adds a clickable menu item.
    /// </summary>
    public void AddItem(string label, Action onClick, bool danger = false)
    {
        var btn = new ContainerButton
        {
            HorizontalExpand = true,
            MinSize = new Vector2(160, 0),
        };

        var normalStyle = new StyleBoxFlat
        {
            BackgroundColor = Color.Transparent,
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };

        var hoverStyle = new StyleBoxFlat
        {
            BackgroundColor = danger
                ? Color.FromHex("#3d1f1f")
                : EditorMainControl.BgSurfaceHover,
            ContentMarginLeftOverride = 8,
            ContentMarginRightOverride = 8,
            ContentMarginTopOverride = 3,
            ContentMarginBottomOverride = 3,
        };

        btn.StyleBoxOverride = normalStyle;

        var lbl = new Label
        {
            Text = label,
            FontColorOverride = danger
                ? EditorMainControl.ErrorColor
                : EditorMainControl.TextPrimary,
        };

        btn.AddChild(lbl);

        btn.OnMouseEntered += _ => btn.StyleBoxOverride = hoverStyle;
        btn.OnMouseExited += _ => btn.StyleBoxOverride = normalStyle;

        btn.OnPressed += _ =>
        {
            onClick();
            Close();
        };

        _itemContainer.AddChild(btn);
    }

    /// <summary>
    /// Adds a visual separator line.
    /// </summary>
    public void AddSeparator()
    {
        var sep = new PanelContainer
        {
            MinSize = new Vector2(0, 1),
            HorizontalExpand = true,
            Margin = new Thickness(4, 3, 4, 3),
        };
        sep.PanelOverride = new StyleBoxFlat
        {
            BackgroundColor = EditorMainControl.Border,
        };
        _itemContainer.AddChild(sep);
    }

    /// <summary>
    /// Opens the context menu at the given screen position (physical pixels).
    /// Converts to virtual pixels for correct positioning at any UI scale.
    /// </summary>
    public void OpenAtPosition(Vector2 screenPos)
    {
        // Convert from physical screen pixels to virtual UI pixels
        var virtualPos = screenPos / UIScale;
        var box = UIBox2.FromDimensions(virtualPos, Vector2.One);
        Open(box);
    }
}
