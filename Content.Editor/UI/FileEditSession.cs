namespace Content.Editor.UI;

/// <summary>
/// The kind of edit operation stored in the undo/redo stack.
/// </summary>
public enum EditKind
{
    FieldEdit,
    FieldReset,
}

/// <summary>
/// Represents a single edit operation on a prototype field or component.
/// Used for undo/redo history tracking.
/// </summary>
public sealed class FieldEdit
{
    public EditKind Kind { get; init; }

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

    // Legacy convenience
    public bool IsReset => Kind == EditKind.FieldReset;
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

    // ── Prototype-level field edits ──

    /// <summary>
    /// Local field overrides: (protoId, fieldName) → current value.
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

    public bool CanUndo => _undoStack.Count > 0;
    public bool CanRedo => _redoStack.Count > 0;

    /// <summary>
    /// Fired when dirty state changes.
    /// </summary>
    public event Action<bool>? OnDirtyChanged;

    // ── Prototype field operations ──

    public void RecordEdit(string protoId, string fieldName, string oldValue, string newValue, bool wasOverridden)
    {
        var edit = new FieldEdit
        {
            Kind = EditKind.FieldEdit,
            PrototypeId = protoId,
            FieldName = fieldName,
            OldValue = oldValue,
            NewValue = newValue,
            WasOverridden = wasOverridden,
        };

        Push(edit);

        var key = (protoId, fieldName);
        PendingResets.Remove(key);
        PendingEdits[key] = newValue;
    }

    public void RecordReset(string protoId, string fieldName, string oldValue, bool wasOverridden)
    {
        var edit = new FieldEdit
        {
            Kind = EditKind.FieldReset,
            PrototypeId = protoId,
            FieldName = fieldName,
            OldValue = oldValue,
            WasOverridden = wasOverridden,
        };

        Push(edit);

        var key = (protoId, fieldName);
        PendingEdits.Remove(key);
        PendingResets.Add(key);
    }

    // ── Undo / Redo ──

    public FieldEdit? Undo()
    {
        if (_undoStack.Count == 0)
            return null;

        var edit = _undoStack[^1];
        _undoStack.RemoveAt(_undoStack.Count - 1);
        _redoStack.Add(edit);

        UndoEdit(edit);

        if (!IsDirty)
            OnDirtyChanged?.Invoke(false);

        return edit;
    }

    public FieldEdit? Redo()
    {
        if (_redoStack.Count == 0)
            return null;

        var edit = _redoStack[^1];
        _redoStack.RemoveAt(_redoStack.Count - 1);
        _undoStack.Add(edit);

        RedoEdit(edit);

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

    // ── Private ──

    private void Push(FieldEdit edit)
    {
        var wasDirty = IsDirty;
        _undoStack.Add(edit);
        _redoStack.Clear();
        if (!wasDirty)
            OnDirtyChanged?.Invoke(true);
    }

    private void UndoEdit(FieldEdit edit)
    {
        switch (edit.Kind)
        {
            case EditKind.FieldEdit:
            {
                var key = (edit.PrototypeId, edit.FieldName);
                if (!edit.WasOverridden)
                    PendingEdits.Remove(key);
                else
                    PendingEdits[key] = edit.OldValue;
                break;
            }
            case EditKind.FieldReset:
            {
                var key = (edit.PrototypeId, edit.FieldName);
                PendingResets.Remove(key);
                if (edit.WasOverridden)
                    PendingEdits[key] = edit.OldValue;
                break;
            }
        }
    }

    private void RedoEdit(FieldEdit edit)
    {
        switch (edit.Kind)
        {
            case EditKind.FieldEdit:
            {
                var key = (edit.PrototypeId, edit.FieldName);
                PendingResets.Remove(key);
                PendingEdits[key] = edit.NewValue;
                break;
            }
            case EditKind.FieldReset:
            {
                var key = (edit.PrototypeId, edit.FieldName);
                PendingEdits.Remove(key);
                PendingResets.Add(key);
                break;
            }
        }
    }
}
