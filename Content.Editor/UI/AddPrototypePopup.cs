using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Editor.UI;

/// <summary>
/// Popup window for adding a new prototype to the current file.
/// Shows a searchable list of all registered prototype types.
/// </summary>
public sealed class AddPrototypePopup : DefaultWindow
{
    /// <summary>
    /// Fired when the user selects a prototype type. Argument is the YAML type string.
    /// </summary>
    public event Action<string>? OnTypeSelected;

    private readonly LineEdit _searchBox;
    private readonly BoxContainer _typeList;
    private readonly List<(string TypeName, Button Button)> _allTypes = new();

    public AddPrototypePopup(IPrototypeManager prototypeManager)
    {
        Title = "Add Prototype";
        SetSize = new Vector2(400, 500);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 8,
        };

        _searchBox = new LineEdit
        {
            PlaceHolder = "Search prototype type...",
            HorizontalExpand = true,
        };
        _searchBox.OnTextChanged += OnSearchChanged;
        vbox.AddChild(_searchBox);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _typeList = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };

        // Populate with all known prototype kinds
        var kinds = new List<(string name, Type type)>();
        foreach (var kindType in prototypeManager.EnumeratePrototypeKinds())
        {
            var attr = (PrototypeAttribute?) Attribute.GetCustomAttribute(kindType, typeof(PrototypeAttribute));
            if (attr?.Type != null)
                kinds.Add((attr.Type, kindType));
        }

        kinds.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));

        foreach (var (name, type) in kinds)
        {
            var supportsInheritance = typeof(IInheritingPrototype).IsAssignableFrom(type);
            var labelText = supportsInheritance ? $"{name}  (inheriting)" : name;

            var btn = new Button
            {
                Text = labelText,
                TextAlign = Label.AlignMode.Left,
                HorizontalExpand = true,
            };

            var typeName = name;
            btn.OnPressed += _ =>
            {
                OnTypeSelected?.Invoke(typeName);
                Close();
            };

            _typeList.AddChild(btn);
            _allTypes.Add((name, btn));
        }

        scroll.AddChild(_typeList);
        vbox.AddChild(scroll);
        Contents.AddChild(vbox);
    }

    private void OnSearchChanged(LineEdit.LineEditEventArgs args)
    {
        var filter = args.Text.Trim();
        foreach (var (name, btn) in _allTypes)
        {
            btn.Visible = string.IsNullOrEmpty(filter)
                       || name.Contains(filter, StringComparison.OrdinalIgnoreCase);
        }
    }
}
