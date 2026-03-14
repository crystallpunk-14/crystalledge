using System.Linq;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.Editor.UI.FieldEditors.Fields;

/// <summary>
/// Field editor for <see cref="ProtoId{T}"/>, <see cref="EntProtoId"/>,
/// and <see cref="EntProtoId{T}"/> fields.
/// Displays a filterable dropdown populated with all prototype IDs of the
/// matching kind, so users can search through potentially thousands of entries.
/// </summary>
public sealed class ProtoIdFieldEditor : FieldEditorBase
{
    private readonly OptionButton _optionButton;
    private readonly List<string> _ids;

    public override Control Control => _optionButton;

    /// <summary>
    /// Creates a proto-id editor populated with all prototype IDs for the
    /// given prototype kind type (e.g. <c>typeof(EntityPrototype)</c>).
    /// </summary>
    /// <param name="protoKind">
    /// The <see cref="IPrototype"/> implementation type whose instances should be listed.
    /// When null, only a free-text fallback is shown.
    /// </param>
    public ProtoIdFieldEditor(Type? protoKind)
    {
        var protoManager = IoCManager.Resolve<IPrototypeManager>();

        _ids = new List<string>();

        if (protoKind != null)
        {
            try
            {
                _ids = protoManager.EnumeratePrototypes(protoKind)
                    .Select(p => p.ID)
                    .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                    .ToList();
            }
            catch
            {
                // Prototype kind may not be registered — leave list empty.
            }
        }

        _optionButton = new OptionButton
        {
            HorizontalExpand = true,
            Filterable = true,
        };

        // Always add an empty entry so the user can clear the value.
        _optionButton.AddItem("", 0);

        for (var i = 0; i < _ids.Count; i++)
        {
            _optionButton.AddItem(_ids[i], i + 1);
        }

        _optionButton.Select(0);

        _optionButton.OnItemSelected += args =>
        {
            _optionButton.SelectId(args.Id);

            if (args.Id == 0)
            {
                RaiseValueChanged("");
            }
            else
            {
                var idx = args.Id - 1;
                if (idx >= 0 && idx < _ids.Count)
                    RaiseValueChanged(_ids[idx]);
            }
        };
    }

    public override string GetValue()
    {
        var id = _optionButton.SelectedId;
        if (id == 0)
            return "";

        var idx = id - 1;
        return idx >= 0 && idx < _ids.Count ? _ids[idx] : "";
    }

    protected override void SetValueCore(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            _optionButton.Select(0);
            return;
        }

        for (var i = 0; i < _ids.Count; i++)
        {
            if (string.Equals(_ids[i], value, StringComparison.OrdinalIgnoreCase))
            {
                _optionButton.SelectId(i + 1);
                return;
            }
        }

        // Value not in the list — still select empty, the raw value
        // is preserved in the session's pending edits.
        _optionButton.Select(0);
    }
}
