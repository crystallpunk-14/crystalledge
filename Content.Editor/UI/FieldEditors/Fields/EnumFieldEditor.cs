using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Enum field editor using an <see cref="OptionButton"/> dropdown.
/// Lists all enum values from the C# type for direct selection.
/// </summary>
public sealed class EnumFieldEditor : FieldEditorBase
{
    private readonly OptionButton _optionButton;
    private readonly string[] _names;

    public override Control Control => _optionButton;

    /// <summary>
    /// Creates an enum editor populated with all values of the given enum type.
    /// </summary>
    public EnumFieldEditor(Type enumType)
    {
        _names = Enum.GetNames(enumType);

        _optionButton = new OptionButton
        {
            HorizontalExpand = true,
        };

        for (var i = 0; i < _names.Length; i++)
        {
            _optionButton.AddItem(_names[i], i);
        }

        if (_names.Length > 0)
            _optionButton.Select(0);

        _optionButton.OnItemSelected += args =>
        {
            _optionButton.SelectId(args.Id);
            var idx = _optionButton.GetIdx(args.Id);
            if (idx >= 0 && idx < _names.Length)
                RaiseValueChanged(_names[idx]);
        };
    }

    public override string GetValue()
    {
        var id = _optionButton.SelectedId;
        var idx = _optionButton.GetIdx(id);
        return idx >= 0 && idx < _names.Length ? _names[idx] : "";
    }

    protected override void SetValueCore(string value)
    {
        for (var i = 0; i < _names.Length; i++)
        {
            if (string.Equals(_names[i], value, StringComparison.OrdinalIgnoreCase))
            {
                _optionButton.Select(i);
                return;
            }
        }

        // If value not found in enum, select first item
        if (_names.Length > 0)
            _optionButton.Select(0);
    }
}
