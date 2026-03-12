using System.Linq;
using Content.Editor.Prototype;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Prototypes;

namespace Content.Editor.UI;

/// <summary>
/// Window that displays the inheritance tree for a prototype.
/// Shows parent chain, children, and field overrides at each level.
/// Uses the engine's real IPrototypeManager for accurate hierarchy data.
/// </summary>
public sealed class InheritanceWindow : DefaultWindow
{
    private readonly IPrototypeManager _prototypeManager;
    private readonly BoxContainer _treeContainer;
    private readonly RichTextLabel _detailsLabel;

    public InheritanceWindow(IPrototypeManager prototypeManager)
    {
        _prototypeManager = prototypeManager;

        Title = "Prototype Inheritance";
        SetSize = new Vector2(600, 500);

        var split = new SplitContainer
        {
            Orientation = SplitContainer.SplitOrientation.Horizontal,
            SplitCenter = 0.4f,
        };

        // Left: tree view
        var leftScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _treeContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            SeparationOverride = 2,
        };
        leftScroll.AddChild(_treeContainer);
        split.AddChild(leftScroll);

        // Right: details
        var rightScroll = new ScrollContainer
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };

        _detailsLabel = new RichTextLabel
        {
            HorizontalExpand = true,
            VerticalExpand = true,
        };
        rightScroll.AddChild(_detailsLabel);
        split.AddChild(rightScroll);

        Contents.AddChild(split);
    }

    /// <summary>
    /// Shows the inheritance hierarchy for the given prototype entry.
    /// </summary>
    public void ShowForPrototype(PrototypeEntry entry)
    {
        _treeContainer.RemoveAllChildren();

        if (entry.PrototypeType == null || entry.Id == null)
        {
            _treeContainer.AddChild(new Label { Text = "Cannot resolve prototype type." });
            return;
        }

        // Build parent chain (bottom-up)
        var chain = new List<(string id, int depth)>();
        BuildParentChain(entry.Id, entry.Parents, chain, 0);

        // Render as an indented tree
        foreach (var (id, depth) in chain)
        {
            var indent = depth * 20;
            var prefix = depth == 0 ? "> " : "  ";
            var label = new Label
            {
                Text = $"{prefix}{id}",
                Margin = new Thickness(indent, 0, 0, 0),
            };

            if (id == entry.Id)
            {
                label.StyleClasses.Add("LabelKeyText");
            }

            var button = new Button
            {
                HorizontalExpand = true,
                TextAlign = Label.AlignMode.Left,
            };
            button.AddChild(label);

            button.OnPressed += _ => ShowDetails(id, entry.PrototypeType);
            _treeContainer.AddChild(button);
        }

        // Also show children
        var childHeader = new Label
        {
            Text = "--- Children ---",
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 8, 0, 4),
            StyleClasses = { "LabelSubText" },
        };
        _treeContainer.AddChild(childHeader);

        var children = FindChildren(entry.Id, entry.PrototypeType);
        if (children.Count == 0)
        {
            _treeContainer.AddChild(new Label
            {
                Text = "(no children)",
                StyleClasses = { "LabelSubText" },
            });
        }
        else
        {
            foreach (var childId in children)
            {
                var childBtn = new Button
                {
                    Text = $"  {childId}",
                    HorizontalExpand = true,
                    TextAlign = Label.AlignMode.Left,
                };
                childBtn.OnPressed += _ => ShowDetails(childId, entry.PrototypeType);
                _treeContainer.AddChild(childBtn);
            }
        }

        // Show initial details
        ShowDetails(entry.Id, entry.PrototypeType);
    }

    private void BuildParentChain(string id, string[]? parents, List<(string, int)> chain, int depth)
    {
        chain.Insert(0, (id, 0));

        if (parents == null || parents.Length == 0)
            return;

        // We'll show the first parent's chain (multi-parent is rare but possible)
        foreach (var parentId in parents.Reverse())
        {
            // Try to find the parent's parents through the prototype manager
            string[]? grandParents = null;
            try
            {
                if (_prototypeManager.TryIndex(typeof(EntityPrototype), parentId, out var parentProto) &&
                    parentProto is IInheritingPrototype inheriting)
                {
                    grandParents = inheriting.Parents;
                }
            }
            catch
            {
                // Not all types support this lookup
            }

            BuildParentChain(parentId, grandParents, chain, depth + 1);
        }

        // Re-index depths
        for (var i = 0; i < chain.Count; i++)
        {
            chain[i] = (chain[i].Item1, i);
        }
    }

    private List<string> FindChildren(string parentId, Type protoType)
    {
        var children = new List<string>();

        try
        {
            foreach (var proto in _prototypeManager.EnumeratePrototypes(protoType))
            {
                if (proto is IInheritingPrototype inheriting &&
                    inheriting.Parents != null &&
                    inheriting.Parents.Contains(parentId))
                {
                    children.Add(proto.ID);
                }
            }
        }
        catch
        {
            // Type may not support enumeration
        }

        children.Sort();
        return children;
    }

    private void ShowDetails(string protoId, Type protoType)
    {
        try
        {
            if (_prototypeManager.TryIndex(protoType, protoId, out var proto))
            {
                _detailsLabel.SetMessage($"[bold]{protoId}[/bold]\nType: {protoType.Name}\n" +
                    $"Abstract: {(proto is IInheritingPrototype inh ? inh.Abstract.ToString() : "N/A")}");
            }
            else
            {
                _detailsLabel.SetMessage($"[bold]{protoId}[/bold]\n(not indexed — may be in another file)");
            }
        }
        catch (Exception ex)
        {
            _detailsLabel.SetMessage($"[bold]{protoId}[/bold]\nError: {ex.Message}");
        }
    }
}
