using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Content.Redactor.Redactor;

public sealed class MetadataRoot
{
    public Dictionary<string, PrototypeMetadata> Prototypes { get; set; } = new();
    public Dictionary<string, ComponentMetadata> Components { get; set; } = new();
    public Dictionary<string, DataDefinitionMetadata> DataDefinitions { get; set; } = new();

    /// <summary>
    /// Maps an abstract / polymorphic DataDefinition base type's full name
    /// (e.g. <c>Content.Shared._CE.EntityEffects.CEEntityEffect</c>) to the
    /// list of concrete implementor full names that can be picked at the
    /// <c>!type:</c> YAML tag site. Empty list means "no implementors found".
    /// </summary>
    public Dictionary<string, List<string>> PolymorphicTypes { get; set; } = new();
}

public sealed class PrototypeMetadata
{
    public string ClassName { get; set; } = "";
    public string YamlType { get; set; } = "";
    public bool Inheriting { get; set; }
    public string? Summary { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class ComponentMetadata
{
    public string ClassName { get; set; } = "";
    public string Name { get; set; } = "";
    public string? Summary { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class DataDefinitionMetadata
{
    public string ClassName { get; set; } = "";
    public string ShortName { get; set; } = "";
    public string? Summary { get; set; }
    public List<FieldMetadata> Fields { get; set; } = new();
}

public sealed class FieldMetadata
{
    public string Name { get; set; } = "";
    public string Tag { get; set; } = "";
    public string Type { get; set; } = "";
    public string FullType { get; set; } = "";
    public string FieldKind { get; set; } = "text";
    public bool Required { get; set; }
    public bool IsId { get; set; }
    public bool IsParent { get; set; }
    public bool IsAbstract { get; set; }
    public bool? AlwaysPushInheritance { get; set; }
    public bool? NeverPushInheritance { get; set; }
    public string? ProtoTypeArg { get; set; }
    public string[]? EnumValues { get; set; }

    // List element info
    public string? ElementKind { get; set; }
    public string? ElementFullType { get; set; }
    public string? ElementProtoTypeArg { get; set; }
    public string[]? ElementEnumValues { get; set; }

    // Map key/value info
    public string? KeyKind { get; set; }
    public string? KeyFullType { get; set; }
    public string? KeyProtoTypeArg { get; set; }
    public string[]? KeyEnumValues { get; set; }
    public string? ValueKind { get; set; }
    public string? ValueFullType { get; set; }
    public string? ValueProtoTypeArg { get; set; }
    public string[]? ValueEnumValues { get; set; }

    // Nested element info for one level of inner generics
    // (e.g. Dictionary<K, List<V>> -> ValueElement* describes V;
    //  List<List<V>> -> ElementElement* describes V).
    public string? ValueElementKind { get; set; }
    public string? ValueElementFullType { get; set; }
    public string? ValueElementProtoTypeArg { get; set; }
    public string? ElementElementKind { get; set; }
    public string? ElementElementFullType { get; set; }
    public string? ElementElementProtoTypeArg { get; set; }

    // DataDefinition reference
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsDataDefinition { get; set; }
    public string? DataDefinitionType { get; set; }

    // XML doc summary
    public string? Summary { get; set; }
}
