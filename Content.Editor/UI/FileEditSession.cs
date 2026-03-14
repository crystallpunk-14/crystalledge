namespace Content.Editor.UI;

/// <summary>
/// Represents a single edit operation on a prototype field.
/// Used for undo/redo history tracking.
/// </summary>
public sealed class FieldEdit
{
    /// <summary>
    /// Prototype ID this edit belongs to.
    /// </summary>
    public string PrototypeId { get; init; } = "";

    /// <summary>
    /// The YAML field key that was changed.
    /// </summary>
    public string FieldName { get; init; } = "";

    /// <summary>
    /// Value before the edit.
    /// </summary>
    public string OldValue { get; init; } = "";

    /// <summary>
    /// Value after the edit.
    /// </summary>
    public string NewValue { get; init; } = "";

    /// <summary>
    /// Whether the field was overridden before this edit.
    /// </summary>
    public bool WasOverridden { get; init; }

    /// <summary>
    /// Whether this edit is a "reset to inherited" operation.
    /// </summary>
    public bool IsReset { get; init; }
}

/// <summary>
/// Tracks in-memory edits, dirty state, and undo/redo history for a single file.
/// </summary>
public sealed class FileEditSession(string filePath)
{
    private readonly List<FieldEdit> _undoStack = new();
    private readonly List<FieldEdit> _redoStack = new();

    /// <summary>
    /// The absolute file path this session belongs to.
    /// </summary>
    public string FilePath { get; } = filePath;

    /// <summary>
    /// The parsed prototypes for visually refreshing the editor.
    /// </summary>
    public List<ParsedPrototype> Prototypes { get; set; } = new();

    /// <summary>
    /// Local field overrides: (protoId, fieldName) → current value.
    /// Tracks all edits the user has made since last save.
    /// </summary>
    public Dictionary<(string ProtoId, string FieldName), string> PendingEdits { get; } = new();

    /// <summary>
    /// Fields that the user has explicitly reset (removed local override).
    /// </summary>
    public HashSet<(string ProtoId, string FieldName)> PendingResets { get; } = new();

    /// <summary>
    /// Whether the session has unsaved changes.
    /// </summary>
    public bool IsDirty => _undoStack.Count > 0;

    /// <summary>
    /// Whether undo is available.
    /// </summary>
    public bool CanUndo => _undoStack.Count > 0;

    /// <summary>
    /// Whether redo is available.
    /// </summary>
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Fired when dirty state changes.
    /// </summary>
    public event Action<bool>? OnDirtyChanged;

    /// <summary>
    /// Records a field value edit.
    /// </summary>
    public void RecordEdit(string protoId, string fieldName, string oldValue, string newValue, bool wasOverridden)
    {
        var edit = new FieldEdit
        {
            PrototypeId = protoId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            WasOverridden = wasOverridden,
            IsReset = false,
        };

        var wasDirty = IsDirty;
        _undoStack.Add(edit);
        _redoStack.Clear();

        var key = (protoId, fieldName);
        PendingResets.Remove(key);
        PendingEdits[key] = newValue;

        if (!wasDirty)
            OnDirtyChanged?.Invoke(true);
    }

    /// <summary>
    /// Records a field reset (remove local override).
    /// </summary>
    public void RecordReset(string protoId, string fieldName, string oldValue, bool wasOverridden)
    {
        var edit = new FieldEdit
        {
            PrototypeId = protoId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = "",
            WasOverridden = wasOverridden,
            IsReset = true,
        };

        var wasDirty = IsDirty;
        _undoStack.Add(edit);
        _redoStack.Clear();

        var key = (protoId, fieldName);
        PendingEdits.Remove(key);
        PendingResets.Add(key);

        if (!wasDirty)
            OnDirtyChanged?.Invoke(true);
    }

    /// <summary>
    /// Undoes the last edit. Returns the edit that was undone, or null.
    /// </summary>
    public FieldEdit? Undo()
    {
        if (_undoStack.Count == 0)
            return null;

        var edit = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(edit);

        // Restore previous state
        var key = (edit.PrototypeId, edit.FieldName);

        if (edit.IsReset)
        {
            // Undo a reset → restore the old value edit
            PendingResets.Remove(key);
            if (edit.WasOverridden)
                PendingEdits[key] = edit.OldValue;
        }
        else
        {
            // Undo a value edit → restore previous value or remove pending
            if (!edit.WasOverridden)
            {
                PendingEdits.Remove(key);
            }
            else
            {
                PendingEdits[key] = edit.OldValue;
            }
        }

        if (!IsDirty)
            OnDirtyChanged?.Invoke(false);

        return edit;
    }

    /// <summary>
    /// Redoes the last undone edit. Returns the edit that was redone, or null.
    /// </summary>
    public FieldEdit? Redo()
    {
        if (_redoStack.Count == 0)
            return null;

        var edit = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(edit);

        var key = (edit.PrototypeId, edit.FieldName);

        if (edit.IsReset)
        {
            PendingEdits.Remove(key);
            PendingResets.Add(key);
        }
        else
        {
            PendingResets.Remove(key);
            PendingEdits[key] = edit.NewValue;
        }

        if (_undoStack.Count == 1)
            OnDirtyChanged?.Invoke(true);

        return edit;
    }

    /// <summary>
    /// Clears all history after a save operation.
    /// </summary>
    public void MarkSaved()
    {
        var wasDirty = IsDirty;
        _undoStack.Clear();
        _redoStack.Clear();
        PendingEdits.Clear();
        PendingResets.Clear();

        if (wasDirty)
            OnDirtyChanged?.Invoke(false);
    }
}
