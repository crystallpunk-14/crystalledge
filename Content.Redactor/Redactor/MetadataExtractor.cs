using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Content.Redactor.Redactor;

/// <summary>
/// Scans compiled assemblies via MetadataLoadContext to extract
/// all IPrototype types, IComponent types and their [DataField] metadata.
/// Outputs Redactor/metadata.json consumed by the web editor.
/// </summary>
public static class MetadataExtractor
{
    public static void Extract(string solutionRoot)
    {
        var outputDir = Path.Combine(solutionRoot, "Redactor");
        Directory.CreateDirectory(outputDir);

        var serverBinDir = Path.Combine(solutionRoot, "bin", "Content.Server");
        var clientBinDir = Path.Combine(solutionRoot, "bin", "Content.Client");

        // Collect all bin directories to scan (server + client)
        var binDirs = new List<string>();
        if (Directory.Exists(serverBinDir)) binDirs.Add(serverBinDir);
        if (Directory.Exists(clientBinDir)) binDirs.Add(clientBinDir);

        if (binDirs.Count == 0)
        {
            Console.Error.WriteLine($"[Redactor] ERROR: No bin directories found.");
            Console.Error.WriteLine("[Redactor] Build Content.Server and Content.Client first (dotnet build).");
            return;
        }

        Console.WriteLine($"[Redactor] Scanning {binDirs.Count} bin directories: {string.Join(", ", binDirs.Select(Path.GetFileName))}");
        Console.WriteLine("[Redactor] Extracting prototype metadata...");

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeDlls = Directory.GetFiles(runtimeDir, "*.dll");

        // Collect DLLs from all bin directories, dedup by filename (server takes precedence for shared DLLs)
        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in runtimeDlls)
            pathMap[Path.GetFileName(p)] = p;
        foreach (var dir in binDirs)
            foreach (var p in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
                pathMap.TryAdd(Path.GetFileName(p), p);

        var resolver = new PathAssemblyResolver(pathMap.Values);
        using var mlc = new MetadataLoadContext(resolver, "System.Runtime");

        // Load XML documentation from all bin directories (accumulates across calls)
        var xmlDocs = new XmlDocReader();
        foreach (var dir in binDirs)
            xmlDocs.LoadFromDirectory(dir);
        if (xmlDocs.Count > 0)
            Console.WriteLine($"[Redactor] Loaded {xmlDocs.Count} XML doc entries");
        else
            Console.WriteLine("[Redactor] No XML documentation files found (summaries will be empty).");

        var dataDefinitions = new Dictionary<string, DataDefinitionMetadata>();
        var fieldExtractor = new FieldExtractor(xmlDocs, dataDefinitions);

        var prototypes = new Dictionary<string, PrototypeMetadata>();
        var components = new Dictionary<string, ComponentMetadata>();
        // baseFullName -> [concreteFullName,...] for polymorphic !type: picking.
        var polymorphicTypes = new Dictionary<string, List<string>>();
        var skippedAssemblies = 0;
        var skippedTypes = 0;

        // Scan unique DLLs from all bin directories (avoid scanning the same DLL twice)
        var scannedDlls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dir in binDirs)
        {
            foreach (var dllPath in Directory.GetFiles(dir, "*.dll", SearchOption.TopDirectoryOnly))
            {
                var fileName = Path.GetFileName(dllPath);
                if (!scannedDlls.Add(fileName)) continue; // already scanned from another dir

                try
                {
                    var assembly = mlc.LoadFromAssemblyPath(dllPath);
                    ScanAssembly(assembly, prototypes, components, dataDefinitions, polymorphicTypes, fieldExtractor, xmlDocs, ref skippedTypes);
                }
                catch (Exception ex)
                {
                    skippedAssemblies++;
                    Console.Error.WriteLine($"[Redactor] Warning: Could not load assembly {fileName}: {ex.Message}");
                }
            }
        }

        var metadata = new MetadataRoot
        {
            Prototypes = prototypes,
            Components = components,
            DataDefinitions = dataDefinitions,
            PolymorphicTypes = polymorphicTypes,
        };

        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        var json = JsonSerializer.Serialize(metadata, options);
        var outputPath = Path.Combine(outputDir, "metadata.json");
        File.WriteAllText(outputPath, json);

        Console.WriteLine($"[Redactor] Extracted {prototypes.Count} prototypes, {components.Count} components, {dataDefinitions.Count} data definitions");
        if (skippedAssemblies > 0)
            Console.WriteLine($"[Redactor] Skipped {skippedAssemblies} unloadable assemblies (native libs, etc.)");
        if (skippedTypes > 0)
            Console.WriteLine($"[Redactor] Skipped {skippedTypes} problematic types");
        Console.WriteLine($"[Redactor] Metadata written to: {outputPath}");
    }

    private static void ScanAssembly(
        Assembly assembly,
        Dictionary<string, PrototypeMetadata> prototypes,
        Dictionary<string, ComponentMetadata> components,
        Dictionary<string, DataDefinitionMetadata> dataDefinitions,
        Dictionary<string, List<string>> polymorphicTypes,
        FieldExtractor fieldExtractor,
        XmlDocReader xmlDocs,
        ref int skippedTypes)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
            Console.Error.WriteLine($"[Redactor] Warning: Partial type load for {assembly.GetName().Name} ({types.Length} types loaded)");
        }

        foreach (var type in types)
        {
            try
            {
                ScanType(type, prototypes, components, dataDefinitions, polymorphicTypes, fieldExtractor, xmlDocs);
            }
            catch (Exception ex)
            {
                skippedTypes++;
                Console.Error.WriteLine($"[Redactor] Warning: Could not scan type {type.FullName}: {ex.Message}");
            }
        }
    }

    private static void ScanType(
        Type type,
        Dictionary<string, PrototypeMetadata> prototypes,
        Dictionary<string, ComponentMetadata> components,
        Dictionary<string, DataDefinitionMetadata> dataDefinitions,
        Dictionary<string, List<string>> polymorphicTypes,
        FieldExtractor fieldExtractor,
        XmlDocReader xmlDocs)
    {
        // Scan DataDefinition types (BOTH abstract bases and concrete
        // implementors – abstract bases are needed so the editor can resolve
        // `List<TBase>` element types, and to pick !type: subtypes).
        //
        // A type counts as a DataDefinition if it has [DataDefinition] /
        // [ImplicitDataDefinitionForInheritors] on itself OR if any ancestor
        // has [ImplicitDataDefinitionForInheritors] (because that attribute
        // implicitly opts every subclass in – the concrete subclasses of
        // e.g. CEEntityEffect do not redeclare the attribute themselves).
        static bool HasDirectDataDefAttr(Type t) => t.CustomAttributes
            .Any(a => a.AttributeType.Name is "DataDefinitionAttribute"
                or "ImplicitDataDefinitionForInheritorsAttribute");

        static bool HasImplicitDataDefAncestor(Type t)
        {
            var b = t.BaseType;
            while (b != null && b.FullName != "System.Object")
            {
                if (b.CustomAttributes.Any(a => a.AttributeType.Name == "ImplicitDataDefinitionForInheritorsAttribute"))
                    return true;
                b = b.BaseType;
            }
            return false;
        }

        var hasDataDef = HasDirectDataDefAttr(type) || HasImplicitDataDefAncestor(type);

        if (hasDataDef)
        {
            var fullName = type.FullName ?? type.Name;
            if (!dataDefinitions.ContainsKey(fullName))
            {
                var fields = fieldExtractor.ExtractDataFields(type);
                // Abstract bases may have zero declared fields – keep them
                // anyway so we know the polymorphic key exists.
                dataDefinitions[fullName] = new DataDefinitionMetadata
                {
                    ClassName = fullName,
                    ShortName = type.Name,
                    Summary = xmlDocs.GetTypeSummary(type),
                    Fields = fields,
                };
            }

            // Walk base chain – if any ancestor is also a DataDefinition,
            // register this concrete type as an implementor of that base.
            // Both abstract and concrete types contribute (a concrete
            // intermediate may itself be a !type: target with further
            // subclasses).
            if (!type.IsAbstract)
            {
                var baseT = type.BaseType;
                while (baseT != null && baseT.FullName != "System.Object")
                {
                    var baseHasDD = baseT.CustomAttributes
                        .Any(a => a.AttributeType.Name is "DataDefinitionAttribute"
                            or "ImplicitDataDefinitionForInheritorsAttribute");
                    if (baseHasDD)
                    {
                        var baseFull = baseT.FullName ?? baseT.Name;
                        if (!polymorphicTypes.TryGetValue(baseFull, out var impls))
                            polymorphicTypes[baseFull] = impls = new List<string>();
                        if (!impls.Contains(fullName))
                            impls.Add(fullName);
                    }
                    baseT = baseT.BaseType;
                }
            }
        }

        // Scan Prototype types
        var protoAttr = type.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.Name is "PrototypeAttribute" or "PrototypeRecordAttribute");

        if (protoAttr != null)
        {
            var yamlType = InferPrototypeYamlType(protoAttr, type);
            var inheriting = type.GetInterfaces().Any(i => i.Name == "IInheritingPrototype");
            var fields = fieldExtractor.ExtractDataFields(type);

            prototypes.TryAdd(yamlType, new PrototypeMetadata
            {
                ClassName = type.FullName ?? type.Name,
                YamlType = yamlType,
                Inheriting = inheriting,
                Summary = xmlDocs.GetTypeSummary(type),
                Fields = fields,
            });
        }

        // Scan Component types
        var compAttr = type.CustomAttributes
            .FirstOrDefault(a => a.AttributeType.Name == "RegisterComponentAttribute");

        if (compAttr != null)
        {
            var compName = InferComponentName(type);
            var fields = fieldExtractor.ExtractDataFields(type);

            components.TryAdd(compName, new ComponentMetadata
            {
                ClassName = type.FullName ?? type.Name,
                Name = compName,
                Summary = xmlDocs.GetTypeSummary(type),
                Fields = fields,
            });
        }
    }

    private static string InferPrototypeYamlType(CustomAttributeData attr, Type type)
    {
        if (attr.ConstructorArguments.Count > 0 &&
            attr.ConstructorArguments[0].Value is string name &&
            !string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var typeName = type.Name;
        if (typeName.EndsWith("Prototype"))
            typeName = typeName[..^"Prototype".Length];

        return char.ToLowerInvariant(typeName[0]) + typeName[1..];
    }

    private static string InferComponentName(Type type)
    {
        var name = type.Name;
        if (name.EndsWith("Component"))
            name = name[..^"Component".Length];
        return name;
    }
}
