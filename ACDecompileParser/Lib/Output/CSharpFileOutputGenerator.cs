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

        // Filter out ignored types
        var filteredTypeModels = typeModels.Where(t => !t.IsIgnored).ToList();

        // Ensure output directory exists
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }
        Directory.CreateDirectory(outputDir);

        // Create lookup cache
        TypeLookupCache? lookupCache = null;
        if (_repository != null)
        {
            lookupCache = new TypeLookupCache(_repository);
            lookupCache.EnsureLoaded();
        }

        // Initialize hierarchy rule engine
        HierarchyRuleEngine? ruleEngine = null;
        if (_repository != null)
        {
            var graph = InheritanceGraphBuilder.Build(_repository);
            ruleEngine = new HierarchyRuleEngine(graph);
            ruleEngine.RegisterRules(DefaultHierarchyRules.GetDefaultRules());
        }

        // Group types by their base name and physical path
        var groupedTypes = _hierarchyService.GroupTypesByBaseNameAndNamespace(filteredTypeModels, ruleEngine);

        reporter?.Start("Generating C# Binding Files", groupedTypes.Count);
        int processedCount = 0;

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
            string fileName = outputFileName.Contains("::")
            ? outputFileName.Substring(outputFileName.LastIndexOf("::") + 2)
            : outputFileName;

            // Create the file path
            string csPath = Path.Combine(dirPath, $"{fileName}.cs");

            // Generate C# content using CSharpGroupProcessor
            var groupProcessor = new CSharpGroupProcessor(_repository, lookupCache);
            string csContent = groupProcessor.GenerateGroupContent(types, includeNamespace: true);

            // Write to file
            File.WriteAllText(csPath, csContent);

            processedCount++;
            reporter?.Report(processedCount);
        }

        reporter?.Finish();
    }
}
