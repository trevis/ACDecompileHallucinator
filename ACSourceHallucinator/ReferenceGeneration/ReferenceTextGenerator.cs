using System.Text;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.Models;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Enums;
using ACSourceHallucinator.Interfaces;
using Microsoft.EntityFrameworkCore;

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
        int functionBodyId, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.Parameters)
            .ThenInclude(p => p.TypeReference)
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.ReturnTypeReference)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.Id == functionBodyId, ct);

        if (function == null)
            throw new ArgumentException($"Function body {functionBodyId} not found");

        var referencedTypeIds = new HashSet<int>();
        var sections = new List<string>();

        // Collect parent struct
        if (function.ParentId.HasValue)
        {
            referencedTypeIds.Add(function.ParentId.Value);
        }

        // Collect return type
        if (function.FunctionSignature?.ReturnTypeReference?.ReferencedTypeId.HasValue == true)
        {
            referencedTypeIds.Add(function.FunctionSignature.ReturnTypeReference.ReferencedTypeId.Value);
        }

        // Collect parameter types
        foreach (var param in function.FunctionSignature?.Parameters ?? Enumerable.Empty<FunctionParamModel>())
        {
            if (param.TypeReference?.ReferencedTypeId.HasValue == true)
            {
                referencedTypeIds.Add(param.TypeReference.ReferencedTypeId.Value);
            }
        }

        // Recursively collect base types
        if (options.IncludeBaseTypes)
        {
            await CollectBaseTypesRecursivelyAsync(referencedTypeIds, ct);
        }

        // Generate sections
        if (function.ParentId.HasValue)
        {
            var parentRef = await GenerateStructReferenceAsync(function.ParentId.Value, options, ct);
            sections.Add($"=== PARENT STRUCT ===\n{parentRef}");
        }

        var paramTypeIds = function.FunctionSignature?.Parameters
            .Where(p => p.TypeReference?.ReferencedTypeId.HasValue == true)
            .Select(p => p.TypeReference!.ReferencedTypeId!.Value)
            .Distinct()
            .Where(id => id != function.ParentId) // Don't duplicate parent
            .ToList() ?? new List<int>();

        if (paramTypeIds.Any())
        {
            var paramRefs = new List<string>();
            foreach (var typeId in paramTypeIds)
            {
                paramRefs.Add(await GenerateTypeReferenceAsync(typeId, options, ct));
            }

            sections.Add($"=== PARAMETER TYPES ===\n{string.Join("\n\n", paramRefs)}");
        }

        var returnTypeId = function.FunctionSignature?.ReturnTypeReference?.ReferencedTypeId;
        if (returnTypeId.HasValue && returnTypeId != function.ParentId && !paramTypeIds.Contains(returnTypeId.Value))
        {
            var returnRef = await GenerateTypeReferenceAsync(returnTypeId.Value, options, ct);
            sections.Add($"=== RETURN TYPE ===\n{returnRef}");
        }

        // Base types (excluding already-shown types)
        var baseTypeIds = referencedTypeIds
            .Except(paramTypeIds)
            .Where(id => id != function.ParentId && id != returnTypeId)
            .ToList();

        if (baseTypeIds.Any() && options.IncludeBaseTypes)
        {
            var baseRefs = new List<string>();
            foreach (var typeId in baseTypeIds)
            {
                baseRefs.Add(await GenerateTypeReferenceAsync(typeId, options, ct));
            }

            sections.Add($"=== BASE TYPES ===\n{string.Join("\n\n", baseRefs)}");
        }

        return string.Join("\n\n", sections);
    }

    private async Task<string> GenerateTypeReferenceAsync(
        int typeId, ReferenceOptions options, CancellationToken ct)
    {
        var type = await _typeDb.Types
            .FirstOrDefaultAsync(t => t.Id == typeId, ct);

        if (type == null) return $"// Type {typeId} not found";

        return type.Type switch
        {
            TypeType.Struct or TypeType.Class => await GenerateStructReferenceAsync(typeId, options, ct),
            TypeType.Enum => await GenerateEnumReferenceAsync(typeId, options, ct),
            _ => $"// {type.FullyQualifiedName} ({type.Type})"
        };
    }

    public async Task<string> GenerateStructReferenceAsync(
        int structId, ReferenceOptions options, CancellationToken ct = default)
    {
        var structType = _repository.GetTypeById(structId);

        if (structType == null)
            return $"// Struct {structId} not found";

        var header = await GetCommentHeaderAsync(options, EntityType.Struct, structId);

        if (!string.IsNullOrEmpty(structType.Source))
        {
            return header + structType.Source;
        }

        // Pre-load members if not already loaded by GetTypeById
        if (structType.StructMembers == null || !structType.StructMembers.Any())
        {
            structType.StructMembers = _repository.GetStructMembers(structId);
        }

        var tokens = _classGenerator.Generate(structType);
        return header + TokensToString(tokens);
    }

    public async Task<string> GenerateEnumReferenceAsync(
        int enumId, ReferenceOptions options, CancellationToken ct = default)
    {
        var enumType = _repository.GetTypeById(enumId);

        if (enumType == null)
            return $"// Enum {enumId} not found";

        var header = await GetCommentHeaderAsync(options, EntityType.Enum, enumId);

        if (!string.IsNullOrEmpty(enumType.Source))
        {
            return header + enumType.Source;
        }

        var tokens = _enumGenerator.Generate(enumType);
        return header + TokensToString(tokens);
    }

    private async Task<string> GetCommentHeaderAsync(ReferenceOptions options, EntityType entityType, int entityId)
    {
        if (options.IncludeComments && options.CommentsFromStage != null)
        {
            var comment = await _resultRepo.GetSuccessfulResultAsync(
                options.CommentsFromStage, entityType, entityId);

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
        int functionBodyId, ReferenceOptions options, CancellationToken ct = default)
    {
        var function = await _typeDb.FunctionBodies
            .Include(f => f.FunctionSignature!)
            .ThenInclude(s => s.Parameters)
            .Include(f => f.ParentType)
            .FirstOrDefaultAsync(f => f.Id == functionBodyId, ct);

        if (function == null)
            return $"// Function {functionBodyId} not found";

        var sb = new StringBuilder();

        // Add comment if available
        sb.Append(await GetCommentHeaderAsync(options, EntityType.StructMethod, functionBodyId));

        sb.AppendLine(function.BodyText); // Full function source

        return sb.ToString();
    }

    private async Task CollectBaseTypesRecursivelyAsync(HashSet<int> typeIds, CancellationToken ct)
    {
        var toProcess = new Queue<int>(typeIds);

        while (toProcess.Count > 0)
        {
            var typeId = toProcess.Dequeue();

            var inheritances = await _typeDb.TypeInheritances
                .Where(i => i.ParentTypeId == typeId && i.RelatedTypeId.HasValue)
                .Select(i => i.RelatedTypeId!.Value)
                .ToListAsync(ct);

            foreach (var baseTypeId in inheritances)
            {
                if (typeIds.Add(baseTypeId))
                {
                    toProcess.Enqueue(baseTypeId);
                }
            }
        }
    }
}
