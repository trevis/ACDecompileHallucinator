using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Lib.Utilities;
using ACDecompileParser.Shared.Lib.Models;
using Microsoft.EntityFrameworkCore;

namespace ACDecompileParser;

class Program
{
    static void Main(string[] args)
    {
        if (args.Length == 0)
        {
            PrintUsage();
            return;
        }

        if (args[0] == "headers")
        {
            RunHeaders(args.Skip(1).ToArray());
        }
        else if (args[0] == "csbindings")
        {
            RunCSharpBindings(args.Skip(1).ToArray());
        }
        else
        {
            RunParse(args);
        }
    }

    static void PrintUsage()
    {
        Console.WriteLine("Usage:");
        Console.WriteLine("  Parse source files and save to database:");
        Console.WriteLine(
            "    dotnet run <source_file1> [source_file2] ... [--output-dir <directory_path>] [--statics-file <statics.txt>]");
        Console.WriteLine("");
        Console.WriteLine("  Generate header files from an existing database:");
        Console.WriteLine("    dotnet run headers [--output-dir <directory_path>]");
    }

    static void RunHeaders(string[] args)
    {
        string outputDir = "./out/";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output-dir" && i + 1 < args.Length)
            {
                outputDir = args[i + 1];
                i++;
            }
        }

        string dbPath = Path.Combine(outputDir, "types.db");
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Error: Database not found at {dbPath}");
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        Console.WriteLine($"Loading types from {dbPath}...");
        var types = repo.GetAllTypes(includeIgnored: true);
        if (types.Count == 0)
        {
            Console.WriteLine("No types found in database.");
            return;
        }

        string includeDir = Path.Combine(outputDir, "include");
        Console.WriteLine($"Generating header files for {types.Count} types to {includeDir}...");

        var generator = new FileOutputGenerator();
        var reporter = new ConsoleProgressReporter();
        generator.GenerateHeaderFiles(types, includeDir, repo, reporter);

        Console.WriteLine("Header generation completed.");
    }

    static void RunParse(string[] args)
    {
        var sourceFileContents = new List<string>();
        var filePaths = new List<string>();
        string outputDir = "./out/";
        string staticsFile = "statics.txt";

        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output-dir" && i + 1 < args.Length)
            {
                outputDir = args[i + 1];
                i++;
            }
            else if (args[i] == "--statics-file" && i + 1 < args.Length)
            {
                staticsFile = args[i + 1];
                i++;
            }
            else if (!args[i].StartsWith("--"))
            {
                var filePath = args[i];
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"Error: File '{filePath}' does not exist.");
                    return;
                }

                try
                {
                    var content = File.ReadAllText(filePath);
                    sourceFileContents.Add(content);
                    filePaths.Add(filePath);
                    Console.WriteLine($"Loaded file: {filePath}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error reading file '{filePath}': {ex.Message}");
                    return;
                }
            }
            else
            {
                Console.WriteLine($"Unknown option: {args[i]}");
                PrintUsage();
                return;
            }
        }

        if (filePaths.Count == 0)
        {
            Console.WriteLine("Error: No source files specified.");
            PrintUsage();
            return;
        }

        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }

        string dbPath = Path.Combine(outputDir, "types.db");
        var reporter = new ConsoleProgressReporter();
        var parser = new SourceParser(sourceFileContents, filePaths, reporter);
        parser.Parse();

        Console.WriteLine(
            $"Parsed {parser.EnumModels.Count} enum(s), {parser.StructModels.Count} struct(s), and {parser.UnionModels.Count} union(s).");

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        using var context = new TypeContext(optionsBuilder.Options);
        context.Database.EnsureCreated();
        using var repo = new TypeRepository(context);

        parser.SaveToDatabase(repo);
        Console.WriteLine($"Saved {parser.TypeModels.Count} types to {dbPath}");

        var resolutionService = new ACDecompileParser.Shared.Lib.Services.TypeResolutionService(repo, reporter);
        resolutionService.ResolveTypeReferences();

        var offsetCalculationService =
            new ACDecompileParser.Shared.Lib.Services.OffsetCalculationService(repo, reporter);
        offsetCalculationService.CalculateAndApplyOffsets();

        repo.SaveChanges();
        Console.WriteLine("Database update completed (Resolution & Offsets).");

        if (File.Exists(staticsFile))
        {
            Console.WriteLine($"Parsing statics from {staticsFile}...");
            var statics = StaticsParser.ParseFile(staticsFile);
            Console.WriteLine($"Parsed {statics.Count} static variables.");

            // Extract values from source files
            StaticValueParser.ParseValues(statics, parser.SourceFileContents, parser.SourceFilePaths, reporter);

            Console.WriteLine("Resolving static variable parents and saving...");

            // Build parent lookup
            var typeLookup = repo.GetTypeLookupData()
                .Where(x => x.StoredFqn != null)
                .GroupBy(x => x.StoredFqn!)
                .ToDictionary(g => g.Key, g => g.First().Id);

            var typeRefsToInsert = new List<TypeReference>();

            // Link parents and prepare type references
            foreach (var s in statics)
            {
                // Create type reference for the static variable's type
                var tr = new TypeReference { TypeString = s.TypeString };
                s.TypeReference = tr;
                typeRefsToInsert.Add(tr);

                if (s.Name.Contains("::"))
                {
                    int lastIndex = s.Name.LastIndexOf("::");
                    string parentFqn = s.Name.Substring(0, lastIndex);
                    string shortName = s.Name.Substring(lastIndex + 2);

                    if (typeLookup.TryGetValue(parentFqn, out int parentId))
                    {
                        s.ParentTypeId = parentId;
                        s.Name = shortName; // Update to short name if linked
                    }
                }
            }

            repo.InsertTypeReferences(typeRefsToInsert);
            repo.InsertStaticVariables(statics);
            repo.SaveChanges();
            Console.WriteLine("Statics saved to database.");
        }
    }

    static void RunCSharpBindings(string[] args)
    {
        string outputDir = "./out/";
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--output-dir" && i + 1 < args.Length)
            {
                outputDir = args[i + 1];
                i++;
            }
        }

        string dbPath = Path.Combine(outputDir, "types.db");
        if (!File.Exists(dbPath))
        {
            Console.WriteLine($"Error: Database not found at {dbPath}");
            return;
        }

        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseSqlite($"Data Source={dbPath}");

        using var context = new TypeContext(optionsBuilder.Options);
        using var repo = new TypeRepository(context);

        Console.WriteLine($"Loading types from {dbPath}...");
        var types = repo.GetAllTypes(includeIgnored: true);
        if (types.Count == 0)
        {
            Console.WriteLine("No types found in database.");
            return;
        }

        string csDir = Path.Combine(outputDir, "cs");
        Console.WriteLine($"Generating C# binding files for {types.Count} types to {csDir}...");

        var generator = new CSharpFileOutputGenerator();
        var reporter = new ConsoleProgressReporter();
        generator.GenerateCSharpFiles(types, csDir, repo, reporter);

        Console.WriteLine("C# binding generation completed.");
    }
}
