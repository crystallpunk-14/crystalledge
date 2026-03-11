using System.IO;
using System.Linq;
using Content.Editor.Prototype;
using Robust.Client.Graphics;
using Robust.Client.Input;
using Robust.Client.ResourceManagement;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Sequence;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Utility;

namespace Content.Editor.UI;

/// <summary>
/// Main application state for the prototype editor.
/// Manages file browsing, prototype loading, editing, validation, and saving.
/// </summary>
public sealed class EditorMainScreen : State
{
    [Dependency] private readonly IUserInterfaceManager _uiManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly ISerializationManager _serializationManager = default!;
    [Dependency] private readonly IResourceManager _resourceManager = default!;
    [Dependency] private readonly IResourceCache _resourceCache = default!;
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;

    private EditorMainControl _mainControl = default!;
    private ISawmill _sawmill = default!;

    /// <summary>
    /// Currently open file tabs: relative path -> parsed data.
    /// </summary>
    private readonly Dictionary<string, OpenFileState> _openFiles = new();

    /// <summary>
    /// Currently active tab path.
    /// </summary>
    private string? _activeTab;

    /// <summary>
    /// The resolved root path to the Resources/ folder on disk.
    /// </summary>
    private string? _resourcesRootPath;

    protected override void Startup()
    {
        _sawmill = _logManager.GetSawmill("editor");
        _sawmill.Info("SS14 Prototype Editor starting up...");

        _mainControl = new EditorMainControl();
        _uiManager.StateRoot.AddChild(_mainControl);

        // Resolve disk path for saving
        ResolveResourcesRoot();

        // Wire up events
        _mainControl.RefreshButton.OnPressed += OnRefreshPressed;
        _mainControl.SearchBox.OnTextChanged += OnSearchTextChanged;
        _mainControl.FileTree.OnFileSelected += OnFileSelected;
        _mainControl.SaveButton.OnPressed += _ => SaveCurrentFile();

        // Ctrl+S keyboard shortcut
        _inputManager.FirstChanceOnKeyEvent += OnRawKeyEvent;

        // Populate file tree
        RefreshFileTree();

        UpdateStatus("Ready -- select a YAML file to edit");
    }

    protected override void Shutdown()
    {
        _inputManager.FirstChanceOnKeyEvent -= OnRawKeyEvent;
        _mainControl.Dispose();
    }

    /// <summary>
    /// Finds the Resources/ folder on disk by walking up from the binary directory.
    /// </summary>
    private void ResolveResourcesRoot()
    {
        // Walk up from binary directory to find the workspace root containing Resources/Prototypes/
        var dir = AppContext.BaseDirectory;

        for (var i = 0; i < 8; i++)
        {
            var candidate = Path.Combine(dir, "Resources", "Prototypes");

            if (Directory.Exists(candidate))
            {
                _resourcesRootPath = Path.Combine(dir, "Resources");
                _sawmill.Info($"Resources root resolved: {_resourcesRootPath}");
                return;
            }

            var parent = Directory.GetParent(dir);

            if (parent == null)
                break;

            dir = parent.FullName;
        }

        _sawmill.Warning("Could not resolve Resources root path. Saving will be disabled.");
    }

    #region Keyboard Shortcut

    private void OnRawKeyEvent(KeyEventArgs keyEvent, KeyEventType type)
    {
        // Only react on key-down, not repeat or up
        if (type != KeyEventType.Down)
            return;

        if (keyEvent.Key == Keyboard.Key.S && keyEvent.Control)
        {
            keyEvent.Handle();
            SaveCurrentFile();
        }
    }

    #endregion

    #region File Tree

    private void RefreshFileTree()
    {
        var prototypesPath = new ResPath("/Prototypes");
        _mainControl.FileTree.PopulateFromResources(_resourceManager, prototypesPath);
        UpdateStatus("File tree loaded");
    }

    private void OnRefreshPressed(BaseButton.ButtonEventArgs args)
    {
        RefreshFileTree();
    }

    private void OnSearchTextChanged(LineEdit.LineEditEventArgs args)
    {
        _mainControl.FileTree.FilterByText(args.Text);
    }

    #endregion

    #region File Opening / Tabs

    private void OnFileSelected(string relativePath)
    {
        if (_openFiles.ContainsKey(relativePath))
        {
            SwitchToTab(relativePath);
            return;
        }

        OpenFile(relativePath);
    }

    private void OpenFile(string relativePath)
    {
        try
        {
            var resPath = new ResPath("/Prototypes") / relativePath;

            if (!_resourceManager.ContentFileExists(resPath))
            {
                UpdateStatus($"File not found: {relativePath}");
                return;
            }

            List<DataNodeDocument> documents;
            string yamlContent;
            using (var stream = _resourceManager.ContentFileRead(resPath))
            using (var reader = new StreamReader(stream))
            {
                yamlContent = reader.ReadToEnd();
            }

            // Parse using the engine's DataNodeParser (takes TextReader)
            using (var stream2 = _resourceManager.ContentFileRead(resPath))
            using (var reader2 = new StreamReader(stream2))
            {
                documents = DataNodeParser.ParseYamlStream(reader2).ToList();
            }

            var fileState = new OpenFileState
            {
                RelativePath = relativePath,
                ResPath = resPath,
                RawYaml = yamlContent,
                Documents = documents,
            };

            // Extract prototype entries from the parsed documents
            fileState.Entries = PrototypeParser.ExtractEntries(documents, _prototypeManager);

            _openFiles[relativePath] = fileState;
            AddTab(relativePath);
            SwitchToTab(relativePath);

            _sawmill.Info($"Opened file: {relativePath} ({fileState.Entries.Count} prototypes)");
            UpdateStatus($"Opened {relativePath} -- {fileState.Entries.Count} prototypes");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to open {relativePath}: {ex}");
            UpdateStatus($"Error opening {relativePath}: {ex.Message}");
        }
    }

    private void AddTab(string relativePath)
    {
        var fileName = Path.GetFileName(relativePath);

        var tabButton = new Button
        {
            Text = fileName,
            ToolTip = relativePath,
            ToggleMode = true,
            MinSize = new Vector2(80, 24),
        };
        tabButton.OnPressed += _ => SwitchToTab(relativePath);

        // Close button
        var closeBtn = new Button
        {
            Text = "x",
            MinSize = new Vector2(20, 24),
            ToolTip = "Close tab",
        };
        closeBtn.OnPressed += _ => CloseTab(relativePath);

        var tabContainer = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Horizontal,
            SeparationOverride = 0,
        };
        tabContainer.AddChild(tabButton);
        tabContainer.AddChild(closeBtn);

        _mainControl.TabBar.AddChild(tabContainer);
    }

    private void SwitchToTab(string relativePath)
    {
        _activeTab = relativePath;
        UpdateTabHighlights();
        RenderPrototypes(relativePath);
    }

    private void CloseTab(string relativePath)
    {
        _openFiles.Remove(relativePath);

        // Remove tab from bar
        for (var i = _mainControl.TabBar.ChildCount - 1; i >= 0; i--)
        {
            var child = _mainControl.TabBar.GetChild(i);

            if (child is BoxContainer box && box.ChildCount > 0 &&
                box.GetChild(0) is Button btn && btn.ToolTip == relativePath)
            {
                _mainControl.TabBar.RemoveChild(child);
                break;
            }
        }

        if (_activeTab == relativePath)
        {
            _activeTab = _openFiles.Keys.FirstOrDefault();

            if (_activeTab != null)
                SwitchToTab(_activeTab);
            else
                ClearEditor();
        }
    }

    private void UpdateTabHighlights()
    {
        for (var i = 0; i < _mainControl.TabBar.ChildCount; i++)
        {
            var child = _mainControl.TabBar.GetChild(i);

            if (child is BoxContainer box && box.ChildCount > 0 &&
                box.GetChild(0) is Button btn)
            {
                btn.Pressed = btn.ToolTip == _activeTab;
            }
        }
    }

    #endregion

    #region Prototype Rendering

    private void RenderPrototypes(string relativePath)
    {
        _mainControl.EditorContent.RemoveAllChildren();

        if (!_openFiles.TryGetValue(relativePath, out var fileState))
            return;

        foreach (var entry in fileState.Entries)
        {
            var card = new PrototypeCard(entry, _prototypeManager, _serializationManager);

            // Wire field change event
            card.OnFieldChanged += (fieldTag, newValue) =>
            {
                OnPrototypeFieldChanged(relativePath, entry, fieldTag, newValue);
            };

            // Wire field reset event
            card.OnFieldReset += fieldTag =>
            {
                MarkFileModified(relativePath);
                _sawmill.Debug($"Field '{fieldTag}' reset in {entry.Id ?? "(new)"}");
            };

            // Wire parent changed event — re-render this card's fields
            card.OnParentChanged += newParents =>
            {
                MarkFileModified(relativePath);
                _sawmill.Debug($"Parents changed for {entry.Id ?? "(new)"}: [{string.Join(", ", newParents ?? Array.Empty<string>())}]");
            };

            _mainControl.EditorContent.AddChild(card);
        }

        // Add "+ Add Prototype" button at the bottom
        var addBtn = new Button
        {
            Text = "+ Add Prototype",
            HorizontalAlignment = Control.HAlignment.Center,
            MinSize = new Vector2(200, 36),
            ToolTip = "Add a new prototype entry to this file",
        };
        addBtn.OnPressed += _ => OnAddPrototype(relativePath, fileState);
        _mainControl.EditorContent.AddChild(addBtn);

        // Validate the file
        ValidateFile(fileState);
    }

    private void ClearEditor()
    {
        _mainControl.EditorContent.RemoveAllChildren();
        _mainControl.ValidationLabel.Text = "";
        UpdateStatus("Ready -- select a YAML file to edit");
    }

    #endregion

    #region Add Prototype

    private void OnAddPrototype(string relativePath, OpenFileState fileState)
    {
        var popup = new AddPrototypePopup(_prototypeManager);

        popup.OnTypeSelected += typeString =>
        {
            // Resolve the C# type
            Type? protoType = null;

            foreach (var kindType in _prototypeManager.EnumeratePrototypeKinds())
            {
                var attr = (PrototypeAttribute?)Attribute.GetCustomAttribute(
                    kindType, typeof(PrototypeAttribute));

                if (attr?.Type == typeString)
                {
                    protoType = kindType;
                    break;
                }
            }

            // Create new entry with minimal mapping
            var mapping = new MappingDataNode();
            mapping["type"] = new Robust.Shared.Serialization.Markdown.Value.ValueDataNode(typeString);
            mapping["id"] = new Robust.Shared.Serialization.Markdown.Value.ValueDataNode("NewPrototype");

            var entry = new PrototypeEntry
            {
                TypeString = typeString,
                PrototypeType = protoType,
                Id = "NewPrototype",
                Mapping = mapping,
            };

            fileState.Entries.Add(entry);
            MarkFileModified(relativePath);

            // Re-render
            RenderPrototypes(relativePath);

            _sawmill.Info($"Added new {typeString} prototype to {relativePath}");
            UpdateStatus($"Added new {typeString} prototype");
        };

        popup.OpenCentered();
    }

    #endregion

    #region Editing

    private void OnPrototypeFieldChanged(string filePath, PrototypeEntry entry,
        string fieldTag, object? newValue)
    {
        MarkFileModified(filePath);

        // Re-validate
        if (_openFiles.TryGetValue(filePath, out var fileState))
            ValidateFile(fileState);

        _sawmill.Debug($"Field '{fieldTag}' changed in {entry.Id ?? "(new)"}");
    }

    private void MarkFileModified(string relativePath)
    {
        if (!_openFiles.TryGetValue(relativePath, out var fileState))
            return;

        fileState.IsModified = true;
        UpdateTabModifiedIndicator(relativePath);
    }

    private void UpdateTabModifiedIndicator(string relativePath)
    {
        for (var i = 0; i < _mainControl.TabBar.ChildCount; i++)
        {
            var child = _mainControl.TabBar.GetChild(i);

            if (child is BoxContainer box && box.ChildCount > 0 &&
                box.GetChild(0) is Button btn && btn.ToolTip == relativePath)
            {
                var fileName = Path.GetFileName(relativePath);
                var isModified = _openFiles.TryGetValue(relativePath, out var fs) && fs.IsModified;
                btn.Text = isModified ? $"* {fileName}" : fileName;
                break;
            }
        }
    }

    #endregion

    #region Saving

    private void SaveCurrentFile()
    {
        if (_activeTab == null || !_openFiles.TryGetValue(_activeTab, out var fileState))
        {
            UpdateStatus("No file is open");
            return;
        }

        if (_resourcesRootPath == null)
        {
            UpdateStatus("Cannot save: Resources root not found");
            _sawmill.Error("Save failed: _resourcesRootPath is null");
            return;
        }

        try
        {
            var diskPath = Path.Combine(_resourcesRootPath, "Prototypes", fileState.RelativePath);
            diskPath = Path.GetFullPath(diskPath);

            // Safety: ensure the path is inside Resources/Prototypes/
            var protosRoot = Path.GetFullPath(Path.Combine(_resourcesRootPath, "Prototypes"));

            if (!diskPath.StartsWith(protosRoot))
            {
                UpdateStatus("Save blocked: path escapes Prototypes directory");
                return;
            }

            // Ensure directory exists
            var dir = Path.GetDirectoryName(diskPath);

            if (dir != null)
                Directory.CreateDirectory(dir);

            // Serialize
            var yamlContent = YamlWriter.SerializeToYaml(fileState.Entries);
            YamlWriter.SaveToFile(diskPath, yamlContent);

            fileState.IsModified = false;
            UpdateTabModifiedIndicator(_activeTab);

            _sawmill.Info($"Saved: {diskPath}");
            UpdateStatus($"Saved {fileState.RelativePath}");
        }
        catch (Exception ex)
        {
            _sawmill.Error($"Failed to save {_activeTab}: {ex}");
            UpdateStatus($"Save failed: {ex.Message}");
        }
    }

    #endregion

    #region Validation

    private void ValidateFile(OpenFileState fileState)
    {
        var errors = new List<string>();

        foreach (var entry in fileState.Entries)
        {
            if (entry.PrototypeType == null)
            {
                errors.Add($"Unknown prototype type: '{entry.TypeString}'");
                continue;
            }

            if (entry.Mapping == null)
                continue;

            // Use the engine's built-in validation
            try
            {
                var validationResult = _serializationManager.ValidateNode(
                    entry.PrototypeType, entry.Mapping);

                var errorNodes = validationResult.GetErrors().ToList();

                foreach (var error in errorNodes)
                {
                    errors.Add($"[{entry.Id ?? "?"}] {error.ErrorReason}");
                }
            }
            catch (Exception ex)
            {
                errors.Add($"[{entry.Id ?? "?"}] Validation error: {ex.Message}");
            }
        }

        fileState.ValidationErrors = errors;

        var errorCount = errors.Count;
        _mainControl.ValidationLabel.Text = errorCount == 0
            ? "\u2713 No errors"
            : $"\u26a0 {errorCount} error(s)";
    }

    #endregion

    #region Status

    private void UpdateStatus(string text)
    {
        _mainControl.StatusLabel.Text = text;
    }

    #endregion
}

/// <summary>
/// State for an open YAML file in the editor.
/// </summary>
public sealed class OpenFileState
{
    public string RelativePath = "";
    public ResPath ResPath;
    public string RawYaml = "";
    public List<DataNodeDocument> Documents = new();
    public List<PrototypeEntry> Entries = new();
    public bool IsModified;
    public List<string> ValidationErrors = new();
}
