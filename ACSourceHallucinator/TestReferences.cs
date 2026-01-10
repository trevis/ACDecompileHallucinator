using ACDecompileParser.Shared.Lib.Storage;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.ReferenceGeneration;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Enums;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace ACSourceHallucinator;

public class TestReferences
{
    public static async Task RunAsync(string sourceDbPath)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TypeContext>();
        optionsBuilder.UseSqlite($"Data Source={sourceDbPath}");
        using var typeDb = new TypeContext(optionsBuilder.Options);

        // We can't easily instantiate StageResultRepository without a real HallucinatorDbContext,
        // so we use the mock.
        var resultRepo = new MockStageResultRepository();
        var generator = new ReferenceTextGenerator(typeDb, resultRepo);

        var enumType = await typeDb.Types.FirstOrDefaultAsync(t => t.BaseName == "PixelFormatID");
        if (enumType == null)
        {
            Console.WriteLine("Enum PixelFormatID not found");
            return;
        }

        Console.WriteLine($"Found enum: {enumType.FullyQualifiedName} (Id: {enumType.Id})");

        var references = await generator.GenerateEnumReferenceAsync(enumType.Id, new ReferenceOptions { IncludeComments = false });
        Console.WriteLine("=== GENERATED REFERENCES ===");
        Console.WriteLine(references);
        Console.WriteLine("============================");
    }
}

public class MockStageResultRepository : IStageResultRepository
{
    public Task<HashSet<(EntityType, int)>> GetCompletedEntityIdsAsync(string stageName)
        => Task.FromResult(new HashSet<(EntityType, int)>());

    public Task SaveResultAsync(StageResult result) => Task.CompletedTask;

    public Task<StageResult?> GetSuccessfulResultAsync(string stageName, EntityType entityType, int entityId)
        => Task.FromResult<StageResult?>(null);

    public Task<List<StageResult>> GetResultsWithLogsAsync(EntityType entityType, int entityId)
        => Task.FromResult(new List<StageResult>());
}