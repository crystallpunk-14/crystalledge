using Robust.Shared.Input;

namespace Content.Editor;

/// <summary>
/// Keyboard shortcut functions specific to the prototype editor.
/// </summary>
[KeyFunctions]
public static class EditorKeyFunctions
{
    public static readonly BoundKeyFunction EditorSave = "EditorSave";
    public static readonly BoundKeyFunction EditorUndo = "EditorUndo";
    public static readonly BoundKeyFunction EditorRedo = "EditorRedo";
}
