using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using ACDecompileParser.Shared.Lib.Output.Rules;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Lib.Output;

/// <summary>
/// Generates C# binding files from parsed types.
/// </summary>
public class CSharpFileOutputGenerator
{
    private ITypeRepository? _repository;
    private readonly ITypeHierarchyService _hierarchyService;

    public CSharpFileOutputGenerator(ITypeHierarchyService? hierarchyService = null)
    {
        _hierarchyService = hierarchyService ?? new TypeHierarchyService();
    }

    public async Task GenerateCSharpFiles(List<TypeModel> typeModels, string outputDir = "./cs/",
        ITypeRepository? repository = null, ICommentProvider? commentProvider = null,
        IProgressReporter? reporter = null)
    {
        _repository = repository;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Filter out ignored types
        var filteredTypeModels = typeModels.Where(t => !t.IsIgnored).ToList();

        // Inject synthetic types for ManualHelpers that define a namespace (contain "::")
        // but don't exist in the input models. This allow us to output them to the correct
        // folder structure (e.g. lib/AC1Legacy/PSRefBufferCharData.cs) instead of Manual/.
        var existingKeys = new HashSet<string>(filteredTypeModels.Select(t =>
            !string.IsNullOrEmpty(t.Namespace) ? $"{t.Namespace}::{t.BaseName}" : t.BaseName));

        int syntheticIdCounter = -1;
        foreach (var key in ACDecompileParser.Shared.Lib.Constants.ManualHelpers.Helpers.Keys)
        {
            if (key.Contains("::") && !existingKeys.Contains(key))
            {
                var parts = key.Split(new[] { "::" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    var ns = parts[0];
                    var baseName = parts[1];

                    filteredTypeModels.Add(new TypeModel
                    {
                        Id = syntheticIdCounter--,
                        Namespace = ns,
                        BaseName = baseName,
                        Type = TypeType.Class, // Default to class
                        Source = $"// Synthetic type for manual helper: {key}"
                    });
                }
            }
        }

        Console.WriteLine(
            $"[Profiling] Filtered types (incl. synthetic): {filteredTypeModels.Count} items, {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Ensure output directory exists
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"[Profiling] Directory setup: {sw.ElapsedMilliseconds}ms");
        sw.Restart();


        sw.Restart();

        // Create lookup cache
        TypeLookupCache? lookupCache = null;
        if (_repository != null)
        {
            lookupCache = new TypeLookupCache(_repository);
            lookupCache.EnsureLoaded();
        }

        Console.WriteLine($"[Profiling] TypeLookupCache load: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Initialize hierarchy rule engine
        HierarchyRuleEngine? ruleEngine = null;
        if (_repository != null)
        {
            var graph = InheritanceGraphBuilder.Build(_repository);
            ruleEngine = new HierarchyRuleEngine(graph);
            ruleEngine.RegisterRules(DefaultHierarchyRules.GetDefaultRules());
        }

        Console.WriteLine($"[Profiling] InheritanceGraphBuilder: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Group types by their base name and physical path
        var groupedTypes = _hierarchyService.GroupTypesByBaseNameAndNamespace(filteredTypeModels, ruleEngine);
        Console.WriteLine($"[Profiling] Type grouping: {sw.ElapsedMilliseconds}ms ({groupedTypes.Count} groups)");
        sw.Restart();

        // PRE-LOAD ALL DATA ONCE for all types (critical performance optimization!)
        // This prevents calling bulk queries 1856 times (once per group)
        if (_repository != null)
        {
            var allTypeIds = filteredTypeModels.Select(t => t.Id).ToList();
            Console.WriteLine($"[Profiling] Pre-loading data for {allTypeIds.Count} types...");

            sw.Restart();
            var allBaseTypes = _repository.GetBaseTypesForMultipleTypes(allTypeIds);
            Console.WriteLine($"[Profiling] Bulk load base types: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var allMembers = _repository.GetStructMembersForMultipleTypes(allTypeIds);
            Console.WriteLine($"[Profiling] Bulk load members: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var allBodies = _repository.GetFunctionBodiesForMultipleTypes(allTypeIds);
            Console.WriteLine($"[Profiling] Bulk load bodies: {sw.ElapsedMilliseconds}ms");

            sw.Restart();
            var allStaticVariables = new Dictionary<int, List<StaticVariableModel>>();
            foreach (var typeId in allTypeIds)
            {
                var svs = _repository.GetStaticVariablesForType(typeId);
                if (svs.Any())
                {
                    allStaticVariables[typeId] = svs;
                }
            }

            Console.WriteLine(
                $"[Profiling] Bulk load static variables: {sw.ElapsedMilliseconds}ms ({allStaticVariables.Count} types with statics)");

            // Attach ALL pre-loaded data to type models
            sw.Restart();
            foreach (var type in filteredTypeModels)
            {
                if (allBaseTypes.TryGetValue(type.Id, out var baseTypes))
                    type.BaseTypes = baseTypes;
                else
                    type.BaseTypes = new List<TypeInheritance>();

                if (allMembers.TryGetValue(type.Id, out var members))
                    type.StructMembers = members;
                else
                    type.StructMembers = new List<StructMemberModel>();

                if (allBodies.TryGetValue(type.Id, out var bodies))
                    type.FunctionBodies = bodies;
                else
                    type.FunctionBodies = new List<FunctionBodyModel>();

                if (allStaticVariables.TryGetValue(type.Id, out var staticVars))
                    type.StaticVariables = staticVars;
                else
                    type.StaticVariables = new List<StaticVariableModel>();
            }

            Console.WriteLine(
                $"[Profiling] Attach data to {filteredTypeModels.Count} types: {sw.ElapsedMilliseconds}ms");

            if (commentProvider != null)
            {
                sw.Restart();
                Console.WriteLine("[Profiling] Populating comments from Hallucinator...");
                var groupProcessor = new CSharpGroupProcessor(_repository, lookupCache);
                await groupProcessor.PopulateCommentsAsync(filteredTypeModels, commentProvider);
                Console.WriteLine($"[Profiling] Populate comments: {sw.ElapsedMilliseconds}ms");
            }
        }

        sw.Restart();

        reporter?.Start("Generating C# Binding Files", groupedTypes.Count);
        int processedCount = 0;
        long totalGenerateMs = 0;
        long totalWriteMs = 0;

        // Track matched manual keys to avoid duplicating them in Manual/ folder
        var usedManualKeys = new HashSet<string>();

        // Process each group to create C# files
        foreach (var group in groupedTypes)
        {
            var (outputFileName, physicalPath) = group.Key;
            var types = group.Value;

            // Create directory path based on rule-calculated physical path
            string dirPath = Path.Combine(outputDir, physicalPath.Replace('/', Path.DirectorySeparatorChar));
            if (!Directory.Exists(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            // Create the C# file name
            string rawName = outputFileName.Contains("::")
                ? outputFileName.Substring(outputFileName.LastIndexOf("::") + 2)
                : outputFileName;

            string fileName = ACDecompileParser.Shared.Lib.Constants.PrimitiveTypeMappings.CleanTypeName(rawName);

            // Construct keys to check for manual override
            // Priority:
            // 1. Namespace::BaseName (most specific, e.g. "AC1Legacy::PStringBase")
            // 2. Full outputFileName (e.g. "AC1Legacy::PStringBase" if that's how it was grouped)
            // 3. Raw name (e.g. "PStringBase")

            string? manualContent = null;
            string matchedKey = null;

            // Check Namespace::BaseName first
            if (types.Any())
            {
                var firstType = types.First();
                if (!string.IsNullOrEmpty(firstType.Namespace))
                {
                    string nsKey = $"{firstType.Namespace}::{firstType.BaseName}";
                    if (ACDecompileParser.Shared.Lib.Constants.ManualHelpers.Helpers.TryGetValue(nsKey,
                            out manualContent))
                    {
                        matchedKey = nsKey;
                    }
                }
            }

            if (manualContent == null)
            {
                if (ACDecompileParser.Shared.Lib.Constants.ManualHelpers.Helpers.TryGetValue(outputFileName,
                        out manualContent))
                {
                    // Strict Namespace Check:
                    // If the type has a namespace, but the matched key is global (no "::"),
                    // we should IGNORE the match. This prevents "PStringBase" from overriding "AC1Legacy::PStringBase".

                    bool typeHasNamespace = types.Any() && !string.IsNullOrEmpty(types.First().Namespace);
                    bool keyHasNamespace = outputFileName.Contains("::");

                    if (typeHasNamespace && !keyHasNamespace)
                    {
                        manualContent = null;
                    }
                    else
                    {
                        matchedKey = outputFileName;
                    }
                }
            }

            // Create the file path for the file - Use the STANDARD generated path
            // This ensures "AC1Legacy::PStringBase" overrides "lib/AC1Legacy/PStringBase.cs"
            // instead of creating a new file.
            string csPath = Path.Combine(dirPath, $"{fileName}.cs");

            if (manualContent != null && matchedKey != null)
            {
                // Mark this key as used so we don't dump it into Manual/ folder
                usedManualKeys.Add(matchedKey);

                // Write manual content to the standard location
                File.WriteAllText(csPath, manualContent);
                processedCount++;
                reporter?.Report(processedCount);
                continue;
            }

            // Generate C# content using CSharpGroupProcessor
            sw.Restart();
            var groupProcessor = new CSharpGroupProcessor(_repository, lookupCache);
            string csContent = groupProcessor.GenerateGroupContent(types, includeNamespace: true, preloadData: false);
            totalGenerateMs += sw.ElapsedMilliseconds;

            // Write to file
            sw.Restart();
            File.WriteAllText(csPath, csContent);
            totalWriteMs += sw.ElapsedMilliseconds;

            processedCount++;
            reporter?.Report(processedCount);
        }

        reporter?.Finish();

        // Write manual helper files to Manual/ subdirectory (excluding ones we already used)
        WriteManualHelpers(outputDir, usedManualKeys);
        Console.WriteLine($"[Profiling] Manual helpers: {sw.ElapsedMilliseconds}ms");

        Console.WriteLine(
            $"[Profiling] Content generation total: {totalGenerateMs}ms (avg: {totalGenerateMs / groupedTypes.Count}ms per file)"
        );
        Console.WriteLine(
            $"[Profiling] File I/O total: {totalWriteMs}ms (avg: {totalWriteMs / groupedTypes.Count}ms per file)");
        Console.WriteLine($"[Profiling] Total files generated: {processedCount}");
    }

    private static void WriteManualHelpers(string outputDir, HashSet<string> excludedKeys)
    {
        string manualDir = Path.Combine(outputDir, "Manual");
        Directory.CreateDirectory(manualDir);

        foreach (var (className, content) in ACDecompileParser.Shared.Lib.Constants.ManualHelpers.Helpers)
        {
            if (excludedKeys.Contains(className))
                continue;

            // Replace :: with __ in filenames
            string fileName = className.Replace("::", "__");
            string filePath = Path.Combine(manualDir, $"{fileName}.cs");
            File.WriteAllText(filePath, content);
        }
    }
}
