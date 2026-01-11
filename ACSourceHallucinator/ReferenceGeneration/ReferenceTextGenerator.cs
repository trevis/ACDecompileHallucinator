using System.Text;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;

namespace ACSourceHallucinator.ReferenceGeneration;

public class ReferenceTextGenerator : IReferenceTextGenerator
{
    private readonly TypeContext _typeDb; // From shared library
    private readonly IStageResultRepository _resultRepo;
    private readonly SqlTypeRepository _repository;
    private readonly ClassOutputGenerator _classGenerator;
    private readonly EnumOutputGenerator _enumGenerator;

    public ReferenceTextGenerator(TypeContext typeDb, IStageResultRepository resultRepo)
    {
        _typeDb = typeDb;
        _resultRepo = resultRepo;

        // Wrap context in repository for generators
        _repository = new SqlTypeRepository(typeDb);
        _classGenerator = new ClassOutputGenerator(_repository);
        _enumGenerator = new EnumOutputGenerator(_repository);
    }

    public async Task<string> GenerateReferencesForFunctionAsync(
        string fullyQualifiedName, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.Parameters)
            .ThenInclude(p => p.TypeReference)
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.ReturnTypeReference)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.FullyQualifiedName == fullyQualifiedName, ct);

        if (function == null)
            throw new ArgumentException($"Function body {fullyQualifiedName} not found");

        var referencedTypeNames = new HashSet<string>();
        var sections = new List<string>();

        // Collect parent struct
        if (function.ParentType != null)
        {
            referencedTypeNames.Add(function.ParentType.StoredFullyQualifiedName);
        }

        // Collect return type
        if (function.FunctionSignature?.ReturnTypeReference?.ReferencedType != null)
        {
            referencedTypeNames.Add(function.FunctionSignature.ReturnTypeReference.ReferencedType
                .StoredFullyQualifiedName);
        }

        // Collect parameter types
        foreach (var param in function.FunctionSignature?.Parameters ?? Enumerable.Empty<FunctionParamModel>())
        {
            if (param.TypeReference?.ReferencedType != null)
            {
                referencedTypeNames.Add(param.TypeReference.ReferencedType.StoredFullyQualifiedName);
            }
        }

        // Recursively collect base types
        if (options.IncludeBaseTypes)
        {
            await CollectBaseTypesRecursivelyAsync(referencedTypeNames, ct);
        }

        // Generate sections
        if (function.ParentType != null)
        {
            var parentRef =
                await GenerateStructReferenceAsync(function.ParentType.StoredFullyQualifiedName, options, ct);
            sections.Add($"=== PARENT STRUCT ===\n{parentRef}");
        }

        var paramTypeNames = function.FunctionSignature?.Parameters
            .Where(p => p.TypeReference?.ReferencedType != null)
            .Select(p => p.TypeReference!.ReferencedType!.StoredFullyQualifiedName)
            .Distinct()
            .Where(name => name != function.ParentType?.StoredFullyQualifiedName) // Don't duplicate parent
            .ToList() ?? new List<string>();

        if (paramTypeNames.Any())
        {
            var paramRefs = new List<string>();
            foreach (var name in paramTypeNames)
            {
                paramRefs.Add(await GenerateTypeReferenceAsync(name, options, ct));
            }

            sections.Add($"=== PARAMETER TYPES ===\n{string.Join("\n\n", paramRefs)}");
        }

        var returnTypeName = function.FunctionSignature?.ReturnTypeReference?.ReferencedType?.StoredFullyQualifiedName;
        if (returnTypeName != null && returnTypeName != function.ParentType?.StoredFullyQualifiedName &&
            !paramTypeNames.Contains(returnTypeName))
        {
            var returnRef = await GenerateTypeReferenceAsync(returnTypeName, options, ct);
            sections.Add($"=== RETURN TYPE ===\n{returnRef}");
        }

        // Base types (excluding already-shown types)
        var baseTypeNames = referencedTypeNames
            .Except(paramTypeNames)
            .Where(name => name != function.ParentType?.StoredFullyQualifiedName && name != returnTypeName)
            .ToList();

        if (baseTypeNames.Any() && options.IncludeBaseTypes)
        {
            var baseRefs = new List<string>();
            foreach (var name in baseTypeNames)
            {
                baseRefs.Add(await GenerateTypeReferenceAsync(name, options, ct));
            }

            sections.Add($"=== BASE TYPES ===\n{string.Join("\n\n", baseRefs)}");
        }

        return string.Join("\n\n", sections);
    }

    private async Task<string> GenerateTypeReferenceAsync(
        string fullyQualifiedName, ReferenceOptions options, CancellationToken ct)
    {
        var type = await _typeDb.Types
            .FirstOrDefaultAsync(t => t.StoredFullyQualifiedName == fullyQualifiedName, ct);

        if (type == null) return $"// Type {fullyQualifiedName} not found";

        return type.Type switch
        {
            TypeType.Struct or TypeType.Class => await GenerateStructReferenceAsync(fullyQualifiedName, options, ct),
            TypeType.Enum => await GenerateEnumReferenceAsync(fullyQualifiedName, options, ct),
            _ => $"// {type.FullyQualifiedName} ({type.Type})"
        };
    }

    public async Task<string> GenerateStructReferenceAsync(
        string fullyQualifiedName, ReferenceOptions options, CancellationToken ct = default)
    {
        var structType = _repository.GetTypeByFullyQualifiedName(fullyQualifiedName);

        if (structType == null)
            return $"// Struct {fullyQualifiedName} not found";

        var header = await GetCommentHeaderAsync(options, EntityType.Struct, fullyQualifiedName);

        if (options.IncludeDefinition)
        {
            if (!string.IsNullOrEmpty(structType.Source))
            {
                return header + structType.Source;
            }

            // Pre-load members if not already loaded by GetTypeByFullyQualifiedName
            if (!structType.StructMembers.Any())
            {
                structType.StructMembers = _repository.GetStructMembers(structType.Id);
            }

            var tokens = _classGenerator.Generate(structType);
            return header + TokensToString(tokens);
        }

        return header;
    }

    public async Task<string> GenerateEnumReferenceAsync(
        string fullyQualifiedName, ReferenceOptions options, CancellationToken ct = default)
    {
        var enumType = _repository.GetTypeByFullyQualifiedName(fullyQualifiedName);

        if (enumType == null)
            return $"// Enum {fullyQualifiedName} not found";

        var sb = new StringBuilder();
        var header = await GetCommentHeaderAsync(options, EntityType.Enum, fullyQualifiedName);
        sb.Append(header);

        if (options.IncludeDefinition)
        {
            if (!string.IsNullOrEmpty(enumType.Source))
            {
                sb.Append(enumType.Source);
            }
            else
            {
                var tokens = _enumGenerator.Generate(enumType);
                sb.Append(TokensToString(tokens));
            }
        }

        // Add referencing functions
        if (options.IncludeReferencingFunctions)
        {
            var members = await _typeDb.EnumMembers
                .Where(m => m.EnumTypeId == enumType.Id)
                .Select(m => m.Name)
                .ToListAsync(ct);

            var searchTerms = new List<string> { enumType.BaseName };
            searchTerms.AddRange(members);
            searchTerms = searchTerms.Distinct().Where(t => !string.IsNullOrWhiteSpace(t)).ToList();

            if (searchTerms.Any())
            {
                // Parametric raw SQL to avoid injection and handle parameters properly
                var sql = new StringBuilder();
                sql.Append("SELECT * FROM FunctionBodies WHERE ");
                sql.Append(string.Join(" OR ", searchTerms.Select((_, i) => $"BodyText LIKE @p{i}")));
                sql.Append(" ORDER BY (");
                sql.Append(string.Join(" + ",
                    searchTerms.Select((_, i) => $"CASE WHEN BodyText LIKE @p{i} THEN 1 ELSE 0 END")));
                sql.Append(") DESC LIMIT 20");

                var sqlString = sql.ToString();
                var parameters = searchTerms.Select((t, i) => (object)new SqliteParameter($"@p{i}", $"%{t}%"))
                    .ToArray();

                var referencingFunctions = await _typeDb.FunctionBodies
                    .FromSqlRaw(sqlString, parameters)
                    .ToListAsync(ct);

                if (referencingFunctions.Any())
                {
                    sb.AppendLine();
                    sb.AppendLine();
                    sb.AppendLine("=== REFERENCING FUNCTIONS ===");
                    foreach (var func in referencingFunctions)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"// From {func.FullyQualifiedName}");
                        sb.AppendLine(func.BodyText);
                    }
                }
            }
        }

        return sb.ToString();
    }

    private async Task<string> GetCommentHeaderAsync(ReferenceOptions options, EntityType entityType,
        string fullyQualifiedName)
    {
        if (options.IncludeComments && options.CommentsFromStage != null)
        {
            var comment = await _resultRepo.GetSuccessfulResultAsync(
                options.CommentsFromStage, entityType, fullyQualifiedName);

            if (comment != null)
            {
                return $"// {comment.GeneratedContent}\n";
            }
        }

        return string.Empty;
    }

    private string TokensToString(IEnumerable<CodeToken> tokens)
    {
        var sb = new StringBuilder();

        foreach (var token in tokens)
        {
            sb.Append(token.Text);
        }

        return sb.ToString();
    }

    public async Task<string> GenerateFunctionReferenceAsync(
        string fullyQualifiedName, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.Parameters)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.FullyQualifiedName == fullyQualifiedName, ct);

        if (function == null)
            return $"// Function {fullyQualifiedName} not found";

        var sb = new StringBuilder();

        // Add comment if available
        sb.Append(await GetCommentHeaderAsync(options, EntityType.StructMethod, fullyQualifiedName));

        sb.AppendLine(function.BodyText); // Full function source

        return sb.ToString();
    }

    private async Task CollectBaseTypesRecursivelyAsync(HashSet<string> fullyQualifiedNames, CancellationToken ct)
    {
        var toProcess = new Queue<string>(fullyQualifiedNames);

        while (toProcess.Count > 0)
        {
            var fqn = toProcess.Dequeue();

            var type = await _typeDb.Types
                .Include(t => t.BaseTypes)
                .ThenInclude(i => i.RelatedType)
                .FirstOrDefaultAsync(t => t.StoredFullyQualifiedName == fqn, ct);

            if (type == null) continue;

            foreach (var baseType in type.BaseTypes)
            {
                if (baseType.RelatedType != null &&
                    fullyQualifiedNames.Add(baseType.RelatedType.StoredFullyQualifiedName))
                {
                    toProcess.Enqueue(baseType.RelatedType.StoredFullyQualifiedName);
                }
            }
        }
    }
}
