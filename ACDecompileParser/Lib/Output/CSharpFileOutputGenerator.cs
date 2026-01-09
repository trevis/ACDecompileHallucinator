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

    public void GenerateCSharpFiles(List<TypeModel> typeModels, string outputDir = "./cs/",
        ITypeRepository? repository = null, IProgressReporter? reporter = null)
    {
        _repository = repository;
        var sw = System.Diagnostics.Stopwatch.StartNew();

        // Filter out ignored types
        var filteredTypeModels = typeModels.Where(t => !t.IsIgnored).ToList();
        Console.WriteLine($"[Profiling] Filtered types: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Ensure output directory exists
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        Directory.CreateDirectory(outputDir);
        Console.WriteLine($"[Profiling] Directory setup: {sw.ElapsedMilliseconds}ms");
        sw.Restart();

        // Write manual helper files to Manual/ subdirectory
        WriteManualHelpers(outputDir);
        Console.WriteLine($"[Profiling] Manual helpers: {sw.ElapsedMilliseconds}ms");
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
        }

        sw.Restart();

        reporter?.Start("Generating C# Binding Files", groupedTypes.Count);
        int processedCount = 0;
        long totalGenerateMs = 0;
        long totalWriteMs = 0;

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

            // Create the file path
            string csPath = Path.Combine(dirPath, $"{fileName}.cs");

            // Generate C# content using CSharpGroupProcessor
            sw.Restart();
            var groupProcessor = new CSharpGroupProcessor(_repository, lookupCache);
            string csContent = groupProcessor.GenerateGroupContent(types, includeNamespace: true);
            totalGenerateMs += sw.ElapsedMilliseconds;

            // Write to file
            sw.Restart();
            File.WriteAllText(csPath, csContent);
            totalWriteMs += sw.ElapsedMilliseconds;

            processedCount++;
            reporter?.Report(processedCount);
        }

        reporter?.Finish();

        Console.WriteLine(
            $"[Profiling] Content generation total: {totalGenerateMs}ms (avg: {totalGenerateMs / groupedTypes.Count}ms per file)");
        Console.WriteLine(
            $"[Profiling] File I/O total: {totalWriteMs}ms (avg: {totalWriteMs / groupedTypes.Count}ms per file)");
        Console.WriteLine($"[Profiling] Total files generated: {processedCount}");
    }

    private static void WriteManualHelpers(string outputDir)
    {
        string manualDir = Path.Combine(outputDir, "Manual");
        Directory.CreateDirectory(manualDir);

        foreach (var (className, content) in ACDecompileParser.Shared.Lib.Constants.ManualHelpers.Helpers)
        {
            string filePath = Path.Combine(manualDir, $"{className}.cs");
            File.WriteAllText(filePath, content);
        }
    }
}
