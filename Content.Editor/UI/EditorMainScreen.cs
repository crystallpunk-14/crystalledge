using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Content.Editor.Prototype;
using Robust.Client.Input;
using Robust.Client.State;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.ContentPack;
using Robust.Shared.Input;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Mapping;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Timing;
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
    [Dependency] private readonly ILogManager _logManager = default!;
    [Dependency] private readonly IInputManager _inputManager = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;

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
    /// Active prototype cards in the editor, keyed by entry reference for error highlighting.
    /// </summary>
    private readonly Dictionary<PrototypeEntry, PrototypeCard> _activeCards = new();

    /// <summary>
    /// The resolved root path to the Resources/ folder on disk.
    /// </summary>
    private string? _resourcesRootPath;

    /// <summary>
    /// Watches the Prototypes directory for external file changes.
    /// </summary>
    private FileSystemWatcher? _fileWatcher;

    /// <summary>
    /// Thread-safe queue of relative paths changed externally.
    /// Populated by the FileSystemWatcher callback (background thread),
    /// consumed in FrameUpdate (main thread).
    /// </summary>
    private readonly ConcurrentQueue<string> _pendingExternalChanges = new();

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
        _mainControl.FileTree.OnItemRightClick += OnFileTreeRightClick;
        _mainControl.SaveButton.OnPressed += _ => SaveCurrentFile();

        // Editor area right-click: collapse/expand all
        _mainControl.EditorPanel.OnKeyBindDown += OnEditorAreaRightClick;

        // Ctrl+S keyboard shortcut
        _inputManager.FirstChanceOnKeyEvent += OnRawKeyEvent;

        // Populate file tree
        RefreshFileTree();

        // Start watching the Prototypes directory for external changes
        StartFileWatcher();

        UpdateStatus("Ready -- select a YAML file to edit");
    }

    public override void FrameUpdate(FrameEventArgs e)
    {
        ProcessExternalChanges();
    }

    protected override void Shutdown()
    {
        _inputManager.FirstChanceOnKeyEvent -= OnRawKeyEvent;
        StopFileWatcher();
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

    #region File Watcher

    /// <summary>
    /// Creates a FileSystemWatcher on Resources/Prototypes/ to detect external edits.
    /// </summary>
    private void StartFileWatcher()
    {
        if (_resourcesRootPath == null)
            return;

        var protosDir = Path.Combine(_resourcesRootPath, "Prototypes");

        if (!Directory.Exists(protosDir))
            return;

        try
        {
            _fileWatcher = new FileSystemWatcher(protosDir)
            {
                Filter = "*.yml",
                IncludeSubdirectories = true,
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                EnableRaisingEvents = true,
            };

            _fileWatcher.Changed += OnExternalFileChanged;
            _fileWatcher.Created += OnExternalFileChanged;
            _fileWatcher.Renamed += (_, e) => EnqueueExternalChange(e.FullPath);

            _sawmill.Info($"File watcher started on: {protosDir}");
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to start file watcher: {ex.Message}");
        }
    }

    /// <summary>
    /// Stops and disposes the file watcher.
    /// </summary>
    private void StopFileWatcher()
    {
        if (_fileWatcher != null)
        {
            _fileWatcher.EnableRaisingEvents = false;
            _fileWatcher.Dispose();
            _fileWatcher = null;
        }
    }

    /// <summary>
    /// Callback from FileSystemWatcher — runs on a thread pool thread.
    /// Queues the relative path for main-thread processing.
    /// </summary>
    private void OnExternalFileChanged(object sender, FileSystemEventArgs e)
    {
        EnqueueExternalChange(e.FullPath);
    }

    /// <summary>
    /// Converts a full disk path to a relative Prototypes/ path and enqueues it.
    /// </summary>
    private void EnqueueExternalChange(string fullPath)
    {
        if (_resourcesRootPath == null)
            return;

        var protosRoot = Path.GetFullPath(Path.Combine(_resourcesRootPath, "Prototypes"));
        var normalizedFull = Path.GetFullPath(fullPath);

        if (!normalizedFull.StartsWith(protosRoot, StringComparison.OrdinalIgnoreCase))
            return;

        // Convert to the same relative path format used by _openFiles keys
        var relativePath = normalizedFull[(protosRoot.Length + 1)..].Replace('\\', '/');
        _pendingExternalChanges.Enqueue(relativePath);
    }

    /// <summary>
    /// Runs on the main thread via FrameUpdate.
    /// Reloads any externally changed files that are currently open and unmodified.
    /// </summary>
    private void ProcessExternalChanges()
    {
        // Deduplicate: a single external save can fire multiple events
        var changedPaths = new HashSet<string>();

        while (_pendingExternalChanges.TryDequeue(out var path))
        {
            changedPaths.Add(path);
        }

        foreach (var relativePath in changedPaths)
        {
            if (!_openFiles.TryGetValue(relativePath, out var fileState))
                continue;

            // If the user has unsaved local changes, skip — don't overwrite their work
            if (fileState.IsModified)
            {
                _sawmill.Debug($"Skipping external reload of {relativePath} (has local modifications)");
                continue;
            }

            _sawmill.Info($"Reloading externally changed file: {relativePath}");
            ReloadFileFromDisk(relativePath, fileState);
        }
    }

    /// <summary>
    /// Re-reads a file from disk and re-parses its prototypes.
    /// If it's the active tab, re-renders the editor.
    /// </summary>
    private void ReloadFileFromDisk(string relativePath, OpenFileState fileState)
    {
        try
        {
            var diskPath = Path.Combine(_resourcesRootPath!, "Prototypes", relativePath);

            if (!File.Exists(diskPath))
                return;

            var yamlContent = File.ReadAllText(diskPath);

            // Re-parse
            using var reader = new StringReader(yamlContent);
            var documents = DataNodeParser.ParseYamlStream(reader).ToList();

            fileState.RawYaml = yamlContent;
            fileState.Documents = documents;
            fileState.Entries = PrototypeParser.ExtractEntries(documents, _prototypeManager);
            fileState.IsModified = false;
            fileState.ValidationErrors = new List<string>();

            UpdateTabModifiedIndicator(relativePath);

            // Re-render if this is the active tab
            if (_activeTab == relativePath)
                RenderPrototypes(relativePath);

            UpdateStatus($"Reloaded {relativePath} (external change)");
        }
        catch (Exception ex)
        {
            _sawmill.Warning($"Failed to reload {relativePath}: {ex.Message}");
        }
    }

    #endregion

    #region Keyboard Shortcut

    private void OnRawKeyEvent(KeyEventArgs keyEvent, KeyEventType type)
    {
        // Only react on key-down, not repeat or up
        if (type != KeyEventType.Down)
            return;

        if (keyEvent is { Key: Keyboard.Key.S, Control: true })
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
                // Extract prototype entries from the parsed documents
                Entries = PrototypeParser.ExtractEntries(documents, _prototypeManager),
            };

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

        // Right-click tab context menu
        tabButton.OnKeyBindDown += args =>
        {
            if (args.Function != EngineKeyFunctions.UIRightClick)
                return;

            args.Handle();

            var menu = new EditorContextMenu();
            menu.AddItem("Close", () => CloseTab(relativePath));
            menu.AddItem("Close Others",
                () =>
                {
                    var toClose = _openFiles.Keys.Where(k => k != relativePath).ToList();
                    foreach (var path in toClose)
                    {
                        CloseTab(path);
                    }
                });
            menu.AddItem("Close All",
                () =>
                {
                    var all = _openFiles.Keys.ToList();
                    foreach (var path in all)
                    {
                        CloseTab(path);
                    }
                });

            _uiManager.ModalRoot.AddChild(menu);
            menu.OpenAtPosition(args.PointerLocation.Position);
        };

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
        _activeCards.Clear();

        if (!_openFiles.TryGetValue(relativePath, out var fileState))
            return;

        foreach (var entry in fileState.Entries)
        {
            var card = new PrototypeCard(entry, _prototypeManager, _serializationManager, _componentFactory);

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
                _sawmill.Debug(
                    $"Parents changed for {entry.Id ?? "(new)"}: [{string.Join(", ", newParents ?? Array.Empty<string>())}]");
            };
            // Wire delete event
            card.OnDeleteRequested += () =>
            {
                fileState.Entries.Remove(entry);
                MarkFileModified(relativePath);
                RenderPrototypes(relativePath);
                _sawmill.Info($"Deleted prototype {entry.Id ?? "(new)"}");
            };
            _activeCards[entry] = card;
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
        _activeCards.Clear();
        _mainControl.ValidationLabel.Text = "";
        UpdateStatus("Ready -- select a YAML file to edit");
    }

    private void OnEditorAreaRightClick(GUIBoundKeyEventArgs args)
    {
        if (args.Function != EngineKeyFunctions.UIRightClick)
            return;

        args.Handle();

        var menu = new EditorContextMenu();
        menu.AddItem("Collapse all prototypes",
            () =>
            {
                foreach (var card in _activeCards.Values)
                {
                    card.BodyContainer.Visible = false;
                    card.CollapseButton.Text = ">";
                }
            });
        menu.AddItem("Expand all prototypes",
            () =>
            {
                foreach (var card in _activeCards.Values)
                {
                    card.BodyContainer.Visible = true;
                    card.CollapseButton.Text = "v";
                }
            });

        _uiManager.ModalRoot.AddChild(menu);
        menu.OpenAtPosition(args.PointerLocation.Position);
    }

    private void OnFileTreeRightClick(string relativePath, bool isDirectory, Vector2 screenPos)
    {
        var menu = new EditorContextMenu();

        if (isDirectory)
        {
            menu.AddItem("New YAML file...", () =>
            {
                // TODO: Implement new file dialog
                _sawmill.Info($"New file in: {relativePath}");
            });
        }
        else
        {
            menu.AddItem("Open", () =>
            {
                OnFileSelected(relativePath);
            });

            menu.AddItem("Open in external editor", () =>
            {
                var absPath = ResolveAbsolutePath(relativePath);
                if (absPath != null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = absPath,
                            UseShellExecute = true,
                        });
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"Failed to open file externally: {ex.Message}");
                    }
                }
            });

            menu.AddSeparator();

            menu.AddItem("Reveal in Explorer", () =>
            {
                var absPath = ResolveAbsolutePath(relativePath);
                if (absPath != null)
                {
                    try
                    {
                        System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{absPath}\"");
                    }
                    catch (Exception ex)
                    {
                        _sawmill.Error($"Failed to reveal in explorer: {ex.Message}");
                    }
                }
            });
        }

        _uiManager.ModalRoot.AddChild(menu);
        menu.OpenAtPosition(screenPos);
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
                var name = PrototypeParser.ResolvePrototypeTypeName(kindType);

                if (name == typeString)
                {
                    protoType = kindType;
                    break;
                }
            }

            // Create new entry with minimal mapping
            var mapping = new MappingDataNode();
            mapping["type"] = new ValueDataNode(typeString);
            mapping["id"] = new ValueDataNode("NewPrototype");

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

    private void OnPrototypeFieldChanged(string filePath,
        PrototypeEntry entry,
        string fieldTag,
        object? newValue)
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

        // Validate before saving to prevent engine hot-reload crash
        ValidateFile(fileState);

        if (fileState.ValidationErrors.Count > 0)
        {
            UpdateStatus($"Cannot save: {fileState.ValidationErrors.Count} validation error(s). Fix errors first.");
            _sawmill.Warning($"Save blocked for {_activeTab}: {fileState.ValidationErrors.Count} errors");
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

            // Suppress watcher while writing to avoid self-triggered reload
            if (_fileWatcher != null)
                _fileWatcher.EnableRaisingEvents = false;

            try
            {
                YamlWriter.SaveToFile(diskPath, yamlContent);
            }
            finally
            {
                if (_fileWatcher != null)
                    _fileWatcher.EnableRaisingEvents = true;
            }

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
        var entryErrors = new Dictionary<PrototypeEntry, List<string>>();
        var entryFieldErrors = new Dictionary<PrototypeEntry, Dictionary<string, string>>();

        foreach (var entry in fileState.Entries)
        {
            var entryErrs = new List<string>();
            var fieldErrs = new Dictionary<string, string>();

            if (entry.PrototypeType == null)
            {
                entryErrs.Add($"Unknown prototype type: '{entry.TypeString}'");
            }
            else if (entry.Mapping != null)
            {
                // Use the engine's built-in validation.
                // Strip "type" meta-key before validation (matches engine behavior).
                try
                {
                    var validationMapping = entry.Mapping.Copy() as MappingDataNode
                                            ?? entry.Mapping;
                    validationMapping.Remove("type");

                    var validationResult = _serializationManager.ValidateNode(
                        entry.PrototypeType,
                        validationMapping);

                    // Extract per-field errors from ValidatedMappingNode
                    if (validationResult is ValidatedMappingNode mappingResult)
                    {
                        foreach (var (keyNode, valueNode) in mappingResult.Mapping)
                        {
                            if (keyNode.Valid && valueNode.Valid)
                                continue;

                            // Try to extract the field name from the key validation node
                            var fieldName = ExtractFieldName(keyNode);
                            var fieldErrorList = valueNode.GetErrors()
                                .Concat(keyNode.GetErrors())
                                .Select(e => e.ErrorReason)
                                .ToList();

                            if (fieldErrorList.Count > 0)
                            {
                                if (fieldName != null)
                                {
                                    fieldErrs[fieldName] = string.Join("\n", fieldErrorList);
                                }

                                foreach (var err in fieldErrorList)
                                {
                                    entryErrs.Add(fieldName != null
                                        ? $"[{fieldName}] {err}"
                                        : err);
                                }
                            }
                        }
                    }
                    else
                    {
                        // Fallback: just collect all errors
                        foreach (var error in validationResult.GetErrors())
                        {
                            entryErrs.Add(error.ErrorReason);
                        }
                    }
                }
                catch (Exception ex)
                {
                    entryErrs.Add($"Validation error: {ex.Message}");
                }

                // Check required fields that have no value
                if (entry.PrototypeType != null && !entry.IsAbstract)
                {
                    var metadata = PrototypeReflector.GetFields(entry.PrototypeType);
                    var parentData = InheritanceResolver.ResolveParentData(entry, _prototypeManager);

                    foreach (var meta in metadata)
                    {
                        if (!meta.IsRequired)
                            continue;

                        // Skip special keys handled elsewhere (id, type, parent, abstract)
                        if (meta.Tag is "id" or "type" or "parent" or "abstract" or "components")
                            continue;

                        // Check if the field has a value in local mapping or inherited data
                        var hasLocal = entry.Mapping?.Has(meta.Tag) ?? false;
                        var hasInherited = parentData?.Has(meta.Tag) ?? false;

                        if (!hasLocal && !hasInherited)
                        {
                            var msg = $"Required field '{meta.Tag}' is not set";
                            if (!fieldErrs.ContainsKey(meta.Tag))
                                fieldErrs[meta.Tag] = msg;
                            entryErrs.Add($"[{meta.Tag}] {msg}");
                        }
                    }
                }
            }

            entryErrors[entry] = entryErrs;
            entryFieldErrors[entry] = fieldErrs;

            foreach (var err in entryErrs)
            {
                errors.Add($"[{entry.Id ?? "?"}] {err}");
            }
        }

        fileState.ValidationErrors = errors;

        // Highlight cards with per-field error details
        foreach (var (entry, card) in _activeCards)
        {
            var hasErrors = entryErrors.TryGetValue(entry, out var cardErrors) && cardErrors.Count > 0;
            var fieldErrs = entryFieldErrors.GetValueOrDefault(entry);

            card.SetHasErrors(
                hasErrors,
                hasErrors ? string.Join("\n", cardErrors!) : null,
                fieldErrs);
        }

        var errorCount = errors.Count;
        _mainControl.ValidationLabel.Text = errorCount == 0
            ? "\u2713 No errors"
            : $"\u26a0 {errorCount} error(s)";

        // Set tooltip on validation label with error list
        _mainControl.ValidationLabel.ToolTip = errorCount == 0
            ? null
            : string.Join("\n", errors.Take(20));
    }

    /// <summary>
    /// Attempts to extract a field name string from a validation key node.
    /// </summary>
    private static string? ExtractFieldName(ValidationNode keyNode)
    {
        // ValidatedValueNode wraps the original DataNode — extract text from it
        if (keyNode is ValidatedValueNode validatedValue
            && validatedValue.DataNode is ValueDataNode valueNode)
        {
            return valueNode.Value;
        }

        // ErrorNode also has the DataNode reference
        if (keyNode is ErrorNode errorNode
            && errorNode.Node is ValueDataNode errValueNode)
        {
            return errValueNode.Value;
        }

        return null;
    }

    #endregion

    #region Status

    private void UpdateStatus(string text)
    {
        _mainControl.StatusLabel.Text = text;
    }

    #endregion

    #region Utilities

    /// <summary>
    /// Resolves a prototype-relative path to an absolute file system path.
    /// Returns null if the resources root is not resolved.
    /// </summary>
    private string? ResolveAbsolutePath(string relativePath)
    {
        if (_resourcesRootPath == null)
            return null;

        return Path.Combine(_resourcesRootPath, "Prototypes", relativePath);
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
