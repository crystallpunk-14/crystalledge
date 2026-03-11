using Robust.Shared.Prototypes;

namespace Content.Editor.Prototype;

/// <summary>
/// How a field value was resolved for display.
/// </summary>
public enum FieldSource
{
    /// <summary>Field is explicitly defined in this prototype's YAML.</summary>
    Local,

    /// <summary>Field is inherited from a parent prototype.</summary>
    Inherited,

    /// <summary>Field uses the C# default value (not in YAML at all).</summary>
    Default,
}

/// <summary>
/// Classification of a field's C# type for control dispatch.
/// </summary>
public enum FieldKind
{
    Boolean,
    Integer,
    Float,
    Text,
    Color,
    Enum,
    Flags,
    ProtoId,
    List,
    Map,
    DataDefinition,
    Vector2,
    Vector3,
    Vector4,
    ComponentList,
    Unknown,
}

/// <summary>
/// Metadata about a single [DataField]-annotated member on a prototype type.
/// Extracted via reflection by <see cref="PrototypeReflector"/>.
/// </summary>
public sealed class FieldMetadata
{
    /// <summary>YAML key name (from DataField.Tag or inferred from member name).</summary>
    public string Tag = "";

    /// <summary>The C# declared type of the field.</summary>
    public Type FieldType = typeof(object);

    /// <summary>Default value from a default-constructed instance, or null.</summary>
    public object? DefaultValue;

    /// <summary>Classification for control dispatch.</summary>
    public FieldKind Kind;

    /// <summary>If true, the field is read-only and should not be editable.</summary>
    public bool IsReadOnly;

    /// <summary>If true, the field is server-only.</summary>
    public bool IsServerOnly;

    /// <summary>Enum value names (for Enum/Flags kinds).</summary>
    public string[]? EnumValues;

    /// <summary>Element type for List fields, or value type for Map fields.</summary>
    public Type? ElementType;

    /// <summary>Key type for Map fields.</summary>
    public Type? KeyType;

    /// <summary>The prototype type argument for ProtoId fields.</summary>
    public Type? ProtoIdType;

    /// <summary>If true, this field holds a ComponentRegistry.</summary>
    public bool IsComponentRegistry;
}
