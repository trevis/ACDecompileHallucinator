using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Output;

namespace ACDecompileParser.Lib.Output;

using ACDecompileParser.Shared.Lib.Utilities;

public class FileOutputGenerator
{
    private ITypeRepository? _repository;
    private readonly ITypeHierarchyService _hierarchyService;

    public FileOutputGenerator(ITypeHierarchyService? hierarchyService = null)
    {
        _hierarchyService = hierarchyService ?? new TypeHierarchyService();
    }

    public void GenerateHeaderFiles(List<TypeModel> typeModels, string outputDir = "./include/",
        ITypeRepository? repository = null, IProgressReporter? reporter = null)
    {
        _repository = repository;

        // Filter out ignored types before processing - use the IsIgnored property instead of the ignore filter
        var filteredTypeModels = typeModels.Where(t => !t.IsIgnored).ToList();

        // Ensure output directory exists
        if (Directory.Exists(outputDir))
        {
            Directory.Delete(outputDir, true);
        }

        Directory.CreateDirectory(outputDir);

        // Create and load the lookup cache once for all header files (performance optimization)
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

        reporter?.Start("Generating Header Files", groupedTypes.Count);
        int processedCount = 0;

        // Process each group to create header files
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

            // Create the header file name
            // Use outputFileName which already accounts for rules and namespace stripping
            string fileName = outputFileName.Contains("::")
                ? outputFileName.Substring(outputFileName.LastIndexOf("::") + 2)
                : outputFileName;

            // Create the header file path
            string headerPath = Path.Combine(dirPath, $"{fileName}.h");

            // Generate header content using TypeGroupProcessor with cache for efficient type resolution
            var groupProcessor = new TypeGroupProcessor(_repository, lookupCache);
            string headerContent = groupProcessor.GenerateGroupContent(types);

            // Write to file
            File.WriteAllText(headerPath, headerContent);

            processedCount++;
            reporter?.Report(processedCount);
        }

        reporter?.Finish();
    }
}
