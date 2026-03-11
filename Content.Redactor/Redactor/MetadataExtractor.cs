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
        if (!Directory.Exists(serverBinDir))
        {
            Console.Error.WriteLine($"[Redactor] ERROR: Server bin directory not found: {serverBinDir}");
            Console.Error.WriteLine("[Redactor] Build Content.Server first (dotnet build).");
            return;
        }

        Console.WriteLine("[Redactor] Extracting prototype metadata...");

        var runtimeDir = RuntimeEnvironment.GetRuntimeDirectory();
        var runtimeDlls = Directory.GetFiles(runtimeDir, "*.dll");
        var projectDlls = Directory.GetFiles(serverBinDir, "*.dll", SearchOption.TopDirectoryOnly);

        // Build path map: project DLLs take precedence over runtime
        var pathMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in runtimeDlls)
            pathMap[Path.GetFileName(p)] = p;
        foreach (var p in projectDlls)
            pathMap[Path.GetFileName(p)] = p;

        var resolver = new PathAssemblyResolver(pathMap.Values);
        using var mlc = new MetadataLoadContext(resolver, "System.Runtime");

        // Load XML documentation (optional — gracefully handle missing docs)
        var xmlDocs = new XmlDocReader();
        var xmlFiles = Directory.GetFiles(serverBinDir, "*.xml", SearchOption.TopDirectoryOnly);
        if (xmlFiles.Length > 0)
        {
            xmlDocs.LoadFromDirectory(serverBinDir);
            Console.WriteLine($"[Redactor] Loaded {xmlDocs.Count} XML doc entries");
        }
        else
        {
            Console.WriteLine("[Redactor] No XML documentation files found (summaries will be empty).");
            Console.WriteLine("[Redactor] To enable summaries, add <GenerateDocumentationFile>true</GenerateDocumentationFile> to server .csproj");
        }

        var dataDefinitions = new Dictionary<string, DataDefinitionMetadata>();
        var fieldExtractor = new FieldExtractor(xmlDocs, dataDefinitions);

        var prototypes = new Dictionary<string, PrototypeMetadata>();
        var components = new Dictionary<string, ComponentMetadata>();
        var skippedAssemblies = 0;
        var skippedTypes = 0;

        foreach (var dllPath in projectDlls)
        {
            try
            {
                var assembly = mlc.LoadFromAssemblyPath(dllPath);
                ScanAssembly(assembly, prototypes, components, dataDefinitions, fieldExtractor, xmlDocs, ref skippedTypes);
            }
            catch (Exception ex)
            {
                skippedAssemblies++;
                Console.Error.WriteLine($"[Redactor] Warning: Could not load assembly {Path.GetFileName(dllPath)}: {ex.Message}");
            }
        }

        var metadata = new MetadataRoot
        {
            Prototypes = prototypes,
            Components = components,
            DataDefinitions = dataDefinitions,
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
                ScanType(type, prototypes, components, dataDefinitions, fieldExtractor, xmlDocs);
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
        FieldExtractor fieldExtractor,
        XmlDocReader xmlDocs)
    {
        // Scan DataDefinition types
        var hasDataDef = type.CustomAttributes
            .Any(a => a.AttributeType.Name is "DataDefinitionAttribute"
                or "ImplicitDataDefinitionForInheritorsAttribute");

        if (hasDataDef && !type.IsAbstract)
        {
            var fullName = type.FullName ?? type.Name;
            if (!dataDefinitions.ContainsKey(fullName))
            {
                var fields = fieldExtractor.ExtractDataFields(type);
                if (fields.Count > 0)
                {
                    dataDefinitions[fullName] = new DataDefinitionMetadata
                    {
                        ClassName = fullName,
                        ShortName = type.Name,
                        Summary = xmlDocs.GetTypeSummary(type),
                        Fields = fields,
                    };
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
