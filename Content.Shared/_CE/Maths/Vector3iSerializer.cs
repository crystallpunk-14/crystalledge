using System.Globalization;
using Robust.Shared.IoC;
using Robust.Shared.Serialization;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.Manager.Attributes;
using Robust.Shared.Serialization.Markdown;
using Robust.Shared.Serialization.Markdown.Validation;
using Robust.Shared.Serialization.Markdown.Value;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Serialization.TypeSerializers.Interfaces;
using Robust.Shared.Utility;

namespace Content.Shared._CE.Maths;

[TypeSerializer]
public sealed class Vector3iSerializer : ITypeSerializer<Vector3i, ValueDataNode>, ITypeCopyCreator<Vector3i>
{
    public Vector3i Read(ISerializationManager serializationManager, ValueDataNode node,
        IDependencyCollection dependencies,
        SerializationHookContext hookCtx,
        ISerializationContext? context = null,
        ISerializationManager.InstantiationDelegate<Vector3i>? instanceProvider = null)
    {
        if (!VectorSerializerUtility.TryParseArgs(node.Value, 3, out var args))
            throw new InvalidMappingException($"Could not parse {nameof(Vector3i)}: '{node.Value}'");

        var x = int.Parse(args[0], CultureInfo.InvariantCulture);
        var y = int.Parse(args[1], CultureInfo.InvariantCulture);
        var z = int.Parse(args[2], CultureInfo.InvariantCulture);
        return new Vector3i(x, y, z);
    }

    public ValidationNode Validate(ISerializationManager serializationManager, ValueDataNode node,
        IDependencyCollection dependencies,
        ISerializationContext? context = null)
    {
        if (!VectorSerializerUtility.TryParseArgs(node.Value, 3, out var args))
            return new ErrorNode(node, "Failed parsing values for Vector3i.");

        return int.TryParse(args[0], NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
               int.TryParse(args[1], NumberStyles.Any, CultureInfo.InvariantCulture, out _) &&
               int.TryParse(args[2], NumberStyles.Any, CultureInfo.InvariantCulture, out _)
            ? new ValidatedValueNode(node)
            : new ErrorNode(node, "Failed parsing values for Vector3i.");
    }

    public DataNode Write(ISerializationManager serializationManager, Vector3i value,
        IDependencyCollection dependencies, bool alwaysWrite = false,
        ISerializationContext? context = null)
    {
        return new ValueDataNode(
            $"{value.X.ToString(CultureInfo.InvariantCulture)}," +
            $"{value.Y.ToString(CultureInfo.InvariantCulture)}," +
            $"{value.Z.ToString(CultureInfo.InvariantCulture)}");
    }

    public Vector3i CreateCopy(ISerializationManager serializationManager, Vector3i source,
        IDependencyCollection dependencies, SerializationHookContext hookCtx, ISerializationContext? context = null)
    {
        return new(source.X, source.Y, source.Z);
    }
}
