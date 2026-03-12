using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.GameObjects;

namespace Content.Editor.UI;

/// <summary>
/// Popup window for adding a new component to a prototype.
/// Shows a searchable list of all registered component types, excluding already-present ones.
/// </summary>
public sealed class AddComponentPopup : DefaultWindow
{
    /// <summary>
    /// Fired when the user selects a component type. Argument is the component name string.
    /// </summary>
    public event Action<string>? OnComponentSelected;

    private readonly LineEdit _searchBox;
    private readonly List<(string Name, Button Button)> _allComponents = new();

    public AddComponentPopup(IComponentFactory componentFactory, IEnumerable<string>? excludeTypes = null)
    {
        Title = "Add Component";
        SetSize = new Vector2(400, 500);

        var exclude = new HashSet<string>(excludeTypes ?? Array.Empty<string>(),
            StringComparer.OrdinalIgnoreCase);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        _searchBox = new LineEdit
        {
            PlaceHolder = "Search component...",
            HorizontalExpand = true,
        };
        _searchBox.OnTextChanged += OnSearchChanged;
        vbox.AddChild(_searchBox);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        var typeList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };

        // Populate with all registered component names
        var names = new List<string>();
        foreach (var reg in componentFactory.AllRegisteredTypes)
        {
            var name = componentFactory.GetComponentName(reg);
            if (!exclude.Contains(name))
                names.Add(name);
        }

        names.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var name in names)
        {
            var btn = new Button
            {
                Text = name,
                TextAlign = Label.AlignMode.Left,
                HorizontalExpand = true,
            };

            var compName = name;
            btn.OnPressed += _ =>
            {
                OnComponentSelected?.Invoke(compName);
                Close();
            };

            typeList.AddChild(btn);
            _allComponents.Add((name, btn));
        }

        scroll.AddChild(typeList);
        vbox.AddChild(scroll);
        Contents.AddChild(vbox);
    }

    private void OnSearchChanged(LineEdit.LineEditEventArgs args)
    {
        var filter = args.Text.Trim();
        var hasFilter = !string.IsNullOrEmpty(filter);

        foreach (var (name, btn) in _allComponents)
        {
            btn.Visible = !hasFilter
                          || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
