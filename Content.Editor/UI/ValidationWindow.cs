using System.Linq;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;

namespace Content.Editor.UI;

/// <summary>
/// Window showing validation results for prototypes.
/// Uses the engine's real SerializationManager.ValidateNode() for accurate validation.
/// </summary>
public sealed class ValidationWindow : DefaultWindow
{
    private readonly ISerializationManager _serializationManager;
    private readonly BoxContainer _errorsContainer;
    private readonly Label _summaryLabel;

    public ValidationWindow(
        ISerializationManager serializationManager
    )
    {
        _serializationManager = serializationManager;

        Title = "Prototype Validation";
        SetSize = new Vector2(700, 400);

        var vbox = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 4,
        };

        _summaryLabel = new Label
        {
            Text = "No validation results.",
            Margin = new Thickness(4),
        };
        vbox.AddChild(_summaryLabel);

        var scroll = new ScrollContainer
        {
            VerticalExpand = true,
            HorizontalExpand = true,
        };

        _errorsContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        scroll.AddChild(_errorsContainer);
        vbox.AddChild(scroll);

        Contents.AddChild(vbox);
    }

    /// <summary>
    /// Validates a set of prototype entries using the engine's validation pipeline.
    /// </summary>
    public void ValidateEntries(IReadOnlyList<Prototype.PrototypeEntry> entries, string fileName)
    {
        _errorsContainer.RemoveAllChildren();
        var totalErrors = 0;

        foreach (var entry in entries)
        {
            if (entry.PrototypeType == null)
            {
                AddError(entry.Id ?? "(no id)", $"Unknown prototype type: '{entry.TypeString}'", true);
                totalErrors++;
                continue;
            }

            if (entry.Mapping == null)
                continue;

            try
            {
                var result = _serializationManager.ValidateNode(entry.PrototypeType, entry.Mapping);
                var errors = result.GetErrors().ToList();

                foreach (var error in errors)
                {
                    AddError(entry.Id ?? "(no id)", error.ErrorReason, error.AlwaysRelevant);
                    totalErrors++;
                }
            }
            catch (Exception ex)
            {
                AddError(entry.Id ?? "(no id)", $"Validation exception: {ex.Message}", true);
                totalErrors++;
            }
        }

        _summaryLabel.Text = totalErrors == 0
            ? $"\u2713 {fileName}: All {entries.Count} prototypes valid"
            : $"\u26a0 {fileName}: {totalErrors} error(s) in {entries.Count} prototypes";
    }

    private void AddError(string protoId, string message, bool isCritical)
    {
        var row = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Margin = new Thickness(4, 1),
        };

        var icon = new Label
        {
            Text = isCritical ? "\u274c" : "\u26a0",
            MinSize = new Vector2(20, 0),
        };
        row.AddChild(icon);

        var idLabel = new Label
        {
            Text = protoId,
            MinSize = new Vector2(150, 0),
            StyleClasses = { "LabelKeyText" },
        };
        row.AddChild(idLabel);

        var msgLabel = new Label
        {
            Text = message,
            HorizontalExpand = true,
        };
        row.AddChild(msgLabel);

        _errorsContainer.AddChild(row);
    }
}
