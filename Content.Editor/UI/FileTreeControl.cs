using System.Linq;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Utility;

namespace Content.Editor.UI;

/// <summary>
/// A file tree control that displays YAML prototype files from the Resources directory.
/// Flat-styled tree items with indentation, expand arrows, and hover highlight.
/// </summary>
public sealed class FileTreeControl : BoxContainer
{
    /// <summary>
    /// Fired when a file (not a folder) is selected. Argument is the relative path from the prototypes root.
    /// </summary>
    public event Action<string>? OnFileSelected;

    private readonly Dictionary<string, TreeEntry> _allEntries = new();
    private string _filterText = "";
    private string? _selectedPath;

    // Flat style boxes for tree item states
    private static readonly StyleBoxFlat NormalStyle = new()
    {
        BackgroundColor = Color.Transparent,
        ContentMarginLeftOverride = 4,
        ContentMarginRightOverride = 4,
        ContentMarginTopOverride = 1,
        ContentMarginBottomOverride = 1,
    };

    private static readonly StyleBoxFlat HoverStyle = new()
    {
        BackgroundColor = EditorMainControl.BgSurfaceHover,
        ContentMarginLeftOverride = 4,
        ContentMarginRightOverride = 4,
        ContentMarginTopOverride = 1,
        ContentMarginBottomOverride = 1,
    };

    private static readonly StyleBoxFlat SelectedStyle = new()
    {
        BackgroundColor = EditorMainControl.AccentDim,
        ContentMarginLeftOverride = 4,
        ContentMarginRightOverride = 4,
        ContentMarginTopOverride = 1,
        ContentMarginBottomOverride = 1,
    };

    public FileTreeControl()
    {
        Orientation = LayoutOrientation.Vertical;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 0;
    }

    /// <summary>
    /// Populates the tree from the resource manager's virtual filesystem.
    /// </summary>
    public void PopulateFromResources(IResourceManager resourceManager, ResPath rootPath)
    {
        _allEntries.Clear();
        RemoveAllChildren();

        BuildTreeRecursive(resourceManager, rootPath, "", 0);
    }

    private void BuildTreeRecursive(IResourceManager resourceManager, ResPath dirPath,
        string relativePath, int depth)
    {
        var dirs = new List<string>();
        var files = new List<string>();

        foreach (var entry in resourceManager.ContentGetDirectoryEntries(dirPath))
        {
            var entryPath = dirPath / entry;

            if (entry.EndsWith('/') || resourceManager.ContentGetDirectoryEntries(entryPath).Any())
            {
                dirs.Add(entry.TrimEnd('/'));
            }
            else if (entry.EndsWith(".yml") || entry.EndsWith(".yaml"))
            {
                files.Add(entry);
            }
        }

        dirs.Sort(StringComparer.OrdinalIgnoreCase);
        files.Sort(StringComparer.OrdinalIgnoreCase);

        foreach (var dir in dirs)
        {
            var dirRelative = string.IsNullOrEmpty(relativePath)
                ? dir
                : $"{relativePath}/{dir}";

            var folderEntry = CreateFolderEntry(dir, dirRelative, depth);
            AddChild(folderEntry.Container);
            _allEntries[dirRelative] = folderEntry;

            BuildTreeRecursive(resourceManager, dirPath / dir, dirRelative, depth + 1);
        }

        foreach (var file in files)
        {
            var fileRelative = string.IsNullOrEmpty(relativePath)
                ? file
                : $"{relativePath}/{file}";

            var fileEntry = CreateFileEntry(file, fileRelative, depth);
            AddChild(fileEntry.Container);
            _allEntries[fileRelative] = fileEntry;
        }
    }

    private TreeEntry CreateFolderEntry(string name, string relativePath, int depth)
    {
        var indent = 4 + depth * 16;

        var btn = new ContainerButton
        {
            HorizontalExpand = true,
            StyleBoxOverride = NormalStyle,
        };

        var hbox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(indent, 0, 0, 0),
        };

        var arrow = new Label
        {
            Text = ">",
            FontColorOverride = EditorMainControl.TextMuted,
            MinSize = new Vector2(12, 0),
        };

        var nameLabel = new Label
        {
            Text = name,
            FontColorOverride = EditorMainControl.TextSecondary,
        };

        hbox.AddChild(arrow);
        hbox.AddChild(nameLabel);
        btn.AddChild(hbox);

        var entry = new TreeEntry
        {
            Container = btn,
            RelativePath = relativePath,
            IsDirectory = true,
            DisplayName = name,
            Depth = depth,
            ArrowLabel = arrow,
            NameLabel = nameLabel,
        };

        btn.OnPressed += _ =>
        {
            entry.IsExpanded = !entry.IsExpanded;
            arrow.Text = entry.IsExpanded ? "v" : ">";
            UpdateVisibility();
        };

        btn.OnMouseEntered += _ =>
        {
            if (_selectedPath != relativePath)
                btn.StyleBoxOverride = HoverStyle;
        };

        btn.OnMouseExited += _ =>
        {
            if (_selectedPath != relativePath)
                btn.StyleBoxOverride = NormalStyle;
        };

        return entry;
    }

    private TreeEntry CreateFileEntry(string name, string relativePath, int depth)
    {
        var indent = 4 + depth * 16;

        var btn = new ContainerButton
        {
            HorizontalExpand = true,
            StyleBoxOverride = NormalStyle,
        };

        var hbox = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 6,
            Margin = new Thickness(indent + 12, 0, 0, 0), // Extra for missing arrow
        };

        var nameLabel = new Label
        {
            Text = name,
            FontColorOverride = EditorMainControl.TextSecondary,
        };

        hbox.AddChild(nameLabel);
        btn.AddChild(hbox);

        var entry = new TreeEntry
        {
            Container = btn,
            RelativePath = relativePath,
            IsDirectory = false,
            DisplayName = name,
            Depth = depth,
            NameLabel = nameLabel,
        };

        btn.OnPressed += _ =>
        {
            // Deselect previous
            if (_selectedPath != null && _allEntries.TryGetValue(_selectedPath, out var prev))
                ((ContainerButton) prev.Container).StyleBoxOverride = NormalStyle;

            _selectedPath = relativePath;
            btn.StyleBoxOverride = SelectedStyle;
            nameLabel.FontColorOverride = EditorMainControl.TextPrimary;

            OnFileSelected?.Invoke(relativePath);
        };

        btn.OnMouseEntered += _ =>
        {
            if (_selectedPath != relativePath)
                btn.StyleBoxOverride = HoverStyle;
        };

        btn.OnMouseExited += _ =>
        {
            if (_selectedPath != relativePath)
                btn.StyleBoxOverride = NormalStyle;
        };

        return entry;
    }

    /// <summary>
    /// Filters the file tree to show only entries matching the given text.
    /// </summary>
    public void FilterByText(string text)
    {
        _filterText = text.Trim();
        UpdateVisibility();
    }

    private void UpdateVisibility()
    {
        var hasFilter = !string.IsNullOrEmpty(_filterText);
        var visiblePaths = new HashSet<string>();

        if (hasFilter)
        {
            foreach (var (path, entry) in _allEntries)
            {
                if (entry.IsDirectory)
                    continue;

                if (entry.DisplayName.Contains(_filterText, StringComparison.OrdinalIgnoreCase) ||
                    entry.RelativePath.Contains(_filterText, StringComparison.OrdinalIgnoreCase))
                {
                    visiblePaths.Add(path);

                    var parts = path.Split('/');
                    var parentPath = "";
                    for (var i = 0; i < parts.Length - 1; i++)
                    {
                        parentPath = i == 0 ? parts[i] : $"{parentPath}/{parts[i]}";
                        visiblePaths.Add(parentPath);
                    }
                }
            }
        }

        foreach (var (path, entry) in _allEntries)
        {
            if (hasFilter)
            {
                entry.Container.Visible = visiblePaths.Contains(path);
            }
            else
            {
                entry.Container.Visible = IsEntryVisible(path);
            }
        }
    }

    private bool IsEntryVisible(string path)
    {
        var lastSlash = path.LastIndexOf('/');
        if (lastSlash < 0)
            return true;

        var parentPath = path[..lastSlash];
        if (_allEntries.TryGetValue(parentPath, out var parent))
        {
            return parent.IsExpanded && IsEntryVisible(parentPath);
        }

        return true;
    }

    private sealed class TreeEntry
    {
        public Control Container = default!;
        public string RelativePath = "";
        public string DisplayName = "";
        public bool IsDirectory;
        public bool IsExpanded;
        public int Depth;
        public Label? ArrowLabel;
        public Label? NameLabel;
    }
}
