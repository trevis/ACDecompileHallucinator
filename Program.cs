using ACDecompileParser.Lib.Parser;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Models;
using System.IO;

// Create a simple test to verify union parsing
string testInput = File.ReadAllText("test_union_input.h");

var sourceParser = new SourceParser([testInput]);
sourceParser.Parse();

Console.WriteLine($"Parsed {sourceParser.StructModels.Count} structs");
Console.WriteLine($"Parsed {sourceParser.EnumModels.Count} enums");
Console.WriteLine($"Parsed {sourceParser.UnionModels.Count} unions");

// Print out the union information
foreach (var unionModel in sourceParser.UnionModels)
{
    Console.WriteLine($"Union: {unionModel.FullyQualifiedName}");
    Console.WriteLine($"  Members: {unionModel.Members.Count}");
    foreach (var member in unionModel.Members)
    {
        Console.WriteLine($"    {member.TypeString} {member.Name}");
    }
    Console.WriteLine();
}

// Test saving to database
var context = new TypeContext("Data Source=unions_test.db");
var repository = new TypeRepository(context);
sourceParser.SaveToDatabase(repository);

Console.WriteLine("Successfully parsed unions and saved to database!");
