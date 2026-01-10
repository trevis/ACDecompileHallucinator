using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Lib.Output;
using ACDecompileParser.Shared.Lib.Utilities;
using ACDecompileParser.Shared.Lib.Services;

namespace ACDecompileParser.Lib.Parser;

public class SourceParser
{
    public List<List<string>> SourceFileContents { get; }
    public List<string> SourceFilePaths { get; }
    public List<EnumTypeModel> EnumModels { get; } = [];
    public List<StructTypeModel> StructModels { get; } = [];
    public List<UnionTypeModel> UnionModels { get; } = [];
    public List<TypeDefModel> TypeDefModels { get; } = [];
    public List<FunctionBodyModel> FunctionBodyModels { get; } = [];
    public List<TypeModel> TypeModels { get; } = [];
    private readonly Dictionary<string, (string FilePath, int? LineNumber)> _processedFullyQualifiedNames = new();
    private readonly Dictionary<string, int> _structModelIndexByFqn = new Dictionary<string, int>();
    private readonly IProgressReporter? _progressReporter;

    public SourceParser(List<List<string>> sourceFileContents, IProgressReporter? progressReporter = null) : this(
        sourceFileContents, new List<string>(), progressReporter)
    {
    }

    public SourceParser(List<List<string>> sourceFileContents, List<string> filePaths,
        IProgressReporter? progressReporter = null)
    {
        SourceFileContents = sourceFileContents;
        SourceFilePaths = filePaths;
        _progressReporter = progressReporter;
    }

    public SourceParser(List<string> sourceFileContents, IProgressReporter? progressReporter = null) : this(
        sourceFileContents, new List<string>(), progressReporter)
    {
    }

    public SourceParser(List<string> sourceFileContents, List<string> filePaths,
        IProgressReporter? progressReporter = null)
    {
        SourceFileContents = sourceFileContents
            .Select(x => x.Split(["\r\n", "\n"], StringSplitOptions.None).ToList())
            .ToList();
        SourceFilePaths = filePaths;
        _progressReporter = progressReporter;
    }

    public void Parse()
    {
        _progressReporter?.Start("Parsing Files", SourceFileContents.Count);

        for (int fileIndex = 0; fileIndex < SourceFileContents.Count; fileIndex++)
        {
            var sourceLines = SourceFileContents[fileIndex];
            var currentFilePath =
                fileIndex < SourceFilePaths.Count ? SourceFilePaths[fileIndex] : $"file_{fileIndex + 1}";

            // Parse structs from the current source file
            var newStructs = TypeParser.ParseStructs(sourceLines, currentFilePath);
            foreach (var newStruct in newStructs)
            {
                var typeModel = newStruct.MakeTypeModel();
                typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;
                var fqn = typeModel.FullyQualifiedName;

                bool shouldBeIgnored = IgnoreFilter.ShouldIgnoreType(fqn);
                typeModel.IsIgnored = shouldBeIgnored;

                if (!_processedFullyQualifiedNames.ContainsKey(fqn))
                {
                    _processedFullyQualifiedNames[fqn] = (currentFilePath, newStruct.LineNumber);
                    _structModelIndexByFqn[fqn] = StructModels.Count;
                    StructModels.Add(newStruct);
                    TypeModels.Add(typeModel);
                }
                else
                {
                    var (firstFilePath, firstLineNumber) = _processedFullyQualifiedNames[fqn];
                    var existingIndex = _structModelIndexByFqn[fqn];
                    var existingStruct = StructModels[existingIndex];

                    bool existingIsForwardDecl = !existingStruct.Members.Any();
                    bool newIsForwardDecl = !newStruct.Members.Any();

                    string firstLoc = $"{firstFilePath}{(firstLineNumber.HasValue ? ":" + firstLineNumber : "")}";
                    string currentLoc =
                        $"{currentFilePath}{(newStruct.LineNumber.HasValue ? ":" + newStruct.LineNumber : "")}";

                    if (existingIsForwardDecl && !newIsForwardDecl)
                    {
                        Console.WriteLine(
                            $"Info: Replacing forward declaration of struct '{fqn}' from '{firstLoc}' with full declaration from '{currentLoc}'.");
                        StructModels[existingIndex] = newStruct;
                        var typeModelIndex = TypeModels.FindIndex(t => t.FullyQualifiedName == fqn);
                        if (typeModelIndex >= 0)
                        {
                            TypeModels[typeModelIndex] = typeModel;
                        }

                        _processedFullyQualifiedNames[fqn] = (currentFilePath, newStruct.LineNumber);
                    }
                    else if (!existingIsForwardDecl && newIsForwardDecl)
                    {
                        Console.WriteLine(
                            $"Info: Ignoring forward declaration of struct '{fqn}' from '{currentLoc}', already have full declaration from '{firstLoc}'.");
                    }
                    else if (existingIsForwardDecl && newIsForwardDecl)
                    {
                        Console.WriteLine(
                            $"Info: Duplicate forward declaration of struct '{fqn}' found in '{currentLoc}', first declared in '{firstLoc}'. Keeping first declaration.");
                    }
                    else
                    {
                        Console.WriteLine(
                            $"Warning: Duplicate struct type '{fqn}' found in '{currentLoc}', first defined in '{firstLoc}'. Skipping duplicate.");
                    }
                }
            }

            // Parse enums from the current source file
            var newEnums = TypeParser.ParseEnums(sourceLines, currentFilePath);
            foreach (var newEnum in newEnums)
            {
                var typeModel = newEnum.MakeTypeModel();
                typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;
                var fqn = typeModel.FullyQualifiedName;

                bool shouldBeIgnored = IgnoreFilter.ShouldIgnoreType(fqn);
                typeModel.IsIgnored = shouldBeIgnored;

                if (!_processedFullyQualifiedNames.ContainsKey(fqn))
                {
                    _processedFullyQualifiedNames[fqn] = (currentFilePath, newEnum.LineNumber);
                    EnumModels.Add(newEnum);
                    TypeModels.Add(typeModel);
                }
                else
                {
                    var (firstFilePath, firstLineNumber) = _processedFullyQualifiedNames[fqn];
                    string firstLoc = $"{firstFilePath}{(firstLineNumber.HasValue ? ":" + firstLineNumber : "")}";
                    string currentLoc =
                        $"{currentFilePath}{(newEnum.LineNumber.HasValue ? ":" + newEnum.LineNumber : "")}";
                    Console.WriteLine(
                        $"Warning: Duplicate enum type '{fqn}' found in '{currentLoc}', first defined in '{firstLoc}'. Skipping duplicate.");
                }
            }

            // Parse unions from the current source file
            var newUnions = TypeParser.ParseUnions(sourceLines, currentFilePath);
            foreach (var newUnion in newUnions)
            {
                var typeModel = newUnion.MakeTypeModel();
                typeModel.StoredFullyQualifiedName = typeModel.FullyQualifiedName;
                var unionFqn = typeModel.FullyQualifiedName;

                bool shouldBeIgnored = IgnoreFilter.ShouldIgnoreType(unionFqn);
                typeModel.IsIgnored = shouldBeIgnored;

                if (!_processedFullyQualifiedNames.ContainsKey(unionFqn))
                {
                    _processedFullyQualifiedNames[unionFqn] = (currentFilePath, newUnion.LineNumber);
                    UnionModels.Add(newUnion);
                    TypeModels.Add(typeModel);
                }
                else
                {
                    var (firstFilePath, firstLineNumber) = _processedFullyQualifiedNames[unionFqn];
                    string firstLoc = $"{firstFilePath}{(firstLineNumber.HasValue ? ":" + firstLineNumber : "")}";
                    string currentLoc =
                        $"{currentFilePath}{(newUnion.LineNumber.HasValue ? ":" + newUnion.LineNumber : "")}";
                    Console.WriteLine(
                        $"Warning: Duplicate union type '{unionFqn}' found in '{currentLoc}', first defined in '{firstLoc}'. Skipping duplicate.");
                }
            }

            // Parse typedefs from the current source file
            var newTypedefs = TypedefParser.ParseTypedefs(sourceLines);
            foreach (var typedef in newTypedefs)
            {
                typedef.File = currentFilePath;
                var typedefFqn = typedef.FullyQualifiedName;

                if (!_processedFullyQualifiedNames.ContainsKey(typedefFqn))
                {
                    _processedFullyQualifiedNames[typedefFqn] = (currentFilePath, typedef.LineNumber);
                    TypeDefModels.Add(typedef);
                }
                else
                {
                    var (firstFilePath, firstLineNumber) = _processedFullyQualifiedNames[typedefFqn];
                    string firstLoc = $"{firstFilePath}{(firstLineNumber.HasValue ? ":" + firstLineNumber : "")}";
                    string currentLoc =
                        $"{currentFilePath}{(typedef.LineNumber.HasValue ? ":" + typedef.LineNumber : "")}";
                    Console.WriteLine(
                        $"Warning: Duplicate typedef '{typedefFqn}' found in '{currentLoc}', first defined in '{firstLoc}'. Skipping duplicate.");
                }
            }

            // Parse function bodies
            var newFunctions = FunctionBodyParser.Parse(sourceLines, currentFilePath);
            foreach (var func in newFunctions)
            {
                FunctionBodyModels.Add(func);
            }

            _progressReporter?.Report(fileIndex + 1, Path.GetFileName(currentFilePath));
        }

        _progressReporter?.Finish("Parsing completed.");
    }

    public void SaveToDatabase(ITypeRepository repo)
    {
        if (!(repo is SqlTypeRepository typeRepoInstance))
        {
            throw new ArgumentException("Repository must be a SqlTypeRepository instance");
        }

        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction;
        try
        {
            transaction = typeRepoInstance.BeginTransaction();
        }
        catch (InvalidOperationException)
        {
            transaction = null;
        }

        try
        {
            SaveToDatabaseInternal(repo, typeRepoInstance);
            transaction?.Commit();
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            transaction?.Dispose();
        }
    }

    private void SaveToDatabaseInternal(ITypeRepository repo, SqlTypeRepository typeRepoInstance)
    {
        _progressReporter?.Start("Saving to Database", 10);

        // ============================================================
        // PHASE 1: Insert base Types (no FK dependencies)
        // ============================================================
        _progressReporter?.Report(0, "Phase 1/10: Base Types");
        var templateArgToParent = new Dictionary<TypeTemplateArgument, TypeModel>();
        var inheritanceToParent = new Dictionary<TypeInheritance, TypeModel>();

        foreach (var type in TypeModels)
        {
            foreach (var ta in type.TemplateArguments.ToList())
            {
                var newTa = new TypeTemplateArgument
                {
                    Position = ta.Position,
                    TypeString = ParsingUtilities.NormalizeTypeString(ta.TypeString),
                    TypeReferenceId = null
                };
                templateArgToParent[newTa] = type;
            }

            type.TemplateArguments.Clear();

            foreach (var bt in type.BaseTypes.ToList())
            {
                var newBt = new TypeInheritance
                {
                    Order = bt.Order,
                    RelatedTypeString = ParsingUtilities.NormalizeTypeString(bt.RelatedTypeString),
                    RelatedTypeId = null
                };
                inheritanceToParent[newBt] = type;
            }

            type.BaseTypes.Clear();
        }

        repo.InsertTypes(TypeModels);
        typeRepoInstance.SaveChanges(); // SAVE #1

        // Build lookup for FK assignment
        var typeModelsByFqn = new Dictionary<string, TypeModel>();
        foreach (var type in TypeModels)
        {
            var key = type.StoredFullyQualifiedName ?? type.FullyQualifiedName;
            if (!string.IsNullOrEmpty(key) && !typeModelsByFqn.ContainsKey(key))
                typeModelsByFqn[key] = type;
        }

        // ============================================================
        // PHASE 2: Insert template args + inheritance (depends on Types)
        // ============================================================
        _progressReporter?.Report(1, "Phase 2/10: Template Args & Inheritance");
        foreach (var kvp in templateArgToParent)
        {
            var ta = kvp.Key;
            var parentType = kvp.Value;
            if (parentType.Id != 0)
            {
                ta.ParentTypeId = parentType.Id;
                typeRepoInstance.InsertTypeTemplateArgument(ta);
            }
        }

        foreach (var kvp in inheritanceToParent)
        {
            var inh = kvp.Key;
            var parentType = kvp.Value;
            if (parentType.Id != 0)
            {
                inh.ParentTypeId = parentType.Id;
                typeRepoInstance.InsertTypeInheritance(inh);
            }
        }

        typeRepoInstance.SaveChanges(); // SAVE #2

        // ============================================================
        // PHASE 3: Insert enum members (depends on Types)
        // ============================================================
        _progressReporter?.Report(2, "Phase 3/10: Enum Members");
        foreach (var enumModel in EnumModels)
        {
            var fqn = enumModel.FullyQualifiedName;
            if (typeModelsByFqn.TryGetValue(fqn, out var typeModel))
            {
                foreach (var member in enumModel.Members)
                    member.EnumTypeId = typeModel.Id;
                if (enumModel.Members.Any())
                    repo.InsertEnumMembers(enumModel.Members);
            }
        }

        // ============================================================
        // PHASE 4: Collect and batch insert ALL TypeReferences
        // ============================================================
        _progressReporter?.Report(3, "Phase 4/10: Type References");
        var existingTypeRefsLookup = repo.GetAllTypeReferences()
            .GroupBy(tr => tr.TypeString)
            .ToDictionary(g => g.Key, g => g.First());

        var newTypeRefs = new List<TypeReference>();

        // Collect from typedefs
        foreach (var typedef in TypeDefModels)
        {
            if (typedef.TypeReference != null && !existingTypeRefsLookup.ContainsKey(typedef.TypeReference.TypeString))
            {
                typedef.TypeReference.File = typedef.File;
                typedef.TypeReference.LineNumber = typedef.LineNumber;
                newTypeRefs.Add(typedef.TypeReference);
                existingTypeRefsLookup[typedef.TypeReference.TypeString] = typedef.TypeReference;
            }

            if (typedef.FunctionSignature != null)
            {
                var sig = typedef.FunctionSignature;
                if (sig.ReturnTypeReference != null &&
                    !existingTypeRefsLookup.ContainsKey(sig.ReturnTypeReference.TypeString))
                {
                    newTypeRefs.Add(sig.ReturnTypeReference);
                    existingTypeRefsLookup[sig.ReturnTypeReference.TypeString] = sig.ReturnTypeReference;
                }

                foreach (var param in sig.Parameters)
                {
                    if (param.TypeReference != null &&
                        !existingTypeRefsLookup.ContainsKey(param.TypeReference.TypeString))
                    {
                        newTypeRefs.Add(param.TypeReference);
                        existingTypeRefsLookup[param.TypeReference.TypeString] = param.TypeReference;
                    }
                }
            }
        }

        // Collect from struct members
        foreach (var structModel in StructModels)
        {
            foreach (var member in structModel.Members)
            {
                if (!string.IsNullOrEmpty(member.TypeString) && !existingTypeRefsLookup.ContainsKey(member.TypeString))
                {
                    var typeReference = TypeReferenceUtilities.CreateTypeReference(member.TypeString);
                    typeReference.File = structModel.File;
                    typeReference.LineNumber = member.LineNumber ?? structModel.LineNumber;
                    newTypeRefs.Add(typeReference);
                    existingTypeRefsLookup[member.TypeString] = typeReference;
                }
            }
        }

        // Collect from union members
        foreach (var unionModel in UnionModels)
        {
            foreach (var member in unionModel.Members)
            {
                if (!string.IsNullOrEmpty(member.TypeString) && !existingTypeRefsLookup.ContainsKey(member.TypeString))
                {
                    var typeReference = TypeReferenceUtilities.CreateTypeReference(member.TypeString);
                    typeReference.File = unionModel.File;
                    typeReference.LineNumber = member.LineNumber ?? unionModel.LineNumber;
                    newTypeRefs.Add(typeReference);
                    existingTypeRefsLookup[member.TypeString] = typeReference;
                }
            }
        }

        // Collect from function body signatures
        foreach (var func in FunctionBodyModels)
        {
            if (func.FunctionSignature != null)
            {
                var sig = func.FunctionSignature;
                if (sig.ReturnTypeReference != null &&
                    !existingTypeRefsLookup.ContainsKey(sig.ReturnTypeReference.TypeString))
                {
                    newTypeRefs.Add(sig.ReturnTypeReference);
                    existingTypeRefsLookup[sig.ReturnTypeReference.TypeString] = sig.ReturnTypeReference;
                }

                foreach (var param in sig.Parameters)
                {
                    if (param.TypeReference != null &&
                        !existingTypeRefsLookup.ContainsKey(param.TypeReference.TypeString))
                    {
                        newTypeRefs.Add(param.TypeReference);
                        existingTypeRefsLookup[param.TypeReference.TypeString] = param.TypeReference;
                    }
                }
            }
        }

        // Collect from struct member function signatures (including return function sigs)
        foreach (var structModel in StructModels)
        {
            foreach (var member in structModel.Members)
            {
                if (member.IsFunctionPointer && member.FunctionSignature != null)
                {
                    if (member.FunctionSignature.ReturnFunctionSignature != null)
                    {
                        var rs = member.FunctionSignature.ReturnFunctionSignature;
                        if (rs.ReturnTypeReference != null &&
                            !existingTypeRefsLookup.ContainsKey(rs.ReturnTypeReference.TypeString))
                        {
                            newTypeRefs.Add(rs.ReturnTypeReference);
                            existingTypeRefsLookup[rs.ReturnTypeReference.TypeString] = rs.ReturnTypeReference;
                        }
                    }

                    if (member.FunctionSignature.ReturnTypeReference != null &&
                        !existingTypeRefsLookup.ContainsKey(member.FunctionSignature.ReturnTypeReference.TypeString))
                    {
                        newTypeRefs.Add(member.FunctionSignature.ReturnTypeReference);
                        existingTypeRefsLookup[member.FunctionSignature.ReturnTypeReference.TypeString] =
                            member.FunctionSignature.ReturnTypeReference;
                    }
                }
            }
        }

        // BULK INSERT all new TypeReferences
        if (newTypeRefs.Any())
            repo.InsertTypeReferences(newTypeRefs);
        typeRepoInstance.SaveChanges(); // SAVE #3

        // ============================================================
        // PHASE 5: Insert TypeDefs and their FunctionSignatures
        // ============================================================
        _progressReporter?.Report(4, "Phase 5/10: TypeDefs");
        foreach (var typedef in TypeDefModels)
        {
            if (typedef.TypeReference != null)
            {
                if (existingTypeRefsLookup.TryGetValue(typedef.TypeReference.TypeString, out var tr))
                    typedef.TypeReference = tr;
                typedef.TypeReferenceId = typedef.TypeReference.Id;
            }

            if (typedef.FunctionSignature != null)
            {
                var sig = typedef.FunctionSignature;
                if (sig.ReturnTypeReference != null &&
                    existingTypeRefsLookup.TryGetValue(sig.ReturnTypeReference.TypeString, out var retTr))
                {
                    sig.ReturnTypeReference = retTr;
                    sig.ReturnTypeReferenceId = retTr.Id;
                }

                foreach (var param in sig.Parameters)
                {
                    if (param.TypeReference != null &&
                        existingTypeRefsLookup.TryGetValue(param.TypeReference.TypeString, out var paramTr))
                    {
                        param.TypeReference = paramTr;
                        param.TypeReferenceId = paramTr.Id;
                    }
                }

                repo.InsertFunctionSignature(sig);
                typedef.FunctionSignatureId = sig.Id;
            }

            repo.InsertTypeDef(typedef);
        }

        // Build typeStringToRef for struct/union members
        var typeStringToRef = new Dictionary<string, TypeReference>();
        foreach (var kvp in existingTypeRefsLookup)
            typeStringToRef[kvp.Key] = kvp.Value;

        // ============================================================
        // PHASE 6: Insert FunctionSignatures for struct members
        // ============================================================
        _progressReporter?.Report(5, "Phase 6/10: Struct Func Sigs");
        var allFunctionSignatures = new List<FunctionSignatureModel>();
        foreach (var structModel in StructModels)
        {
            foreach (var member in structModel.Members)
            {
                if (member.IsFunctionPointer && member.FunctionSignature != null)
                {
                    if (member.FunctionSignature.ReturnFunctionSignature != null)
                        allFunctionSignatures.Add(member.FunctionSignature.ReturnFunctionSignature);
                    allFunctionSignatures.Add(member.FunctionSignature);
                }
            }
        }

        if (allFunctionSignatures.Any())
        {
            // Insert return signatures first (they have no dependencies)
            var rsigs = allFunctionSignatures.Where(s => s.Name.StartsWith("__return_sig_")).ToList();
            foreach (var sig in rsigs)
                repo.InsertFunctionSignature(sig);

            // Insert member signatures (may depend on return sigs)
            var msigs = allFunctionSignatures.Where(s => !s.Name.StartsWith("__return_sig_")).ToList();
            foreach (var sig in msigs)
            {
                if (sig.ReturnFunctionSignature != null)
                    sig.ReturnFunctionSignatureId = sig.ReturnFunctionSignature.Id;
                repo.InsertFunctionSignature(sig);
            }
        }

        typeRepoInstance.SaveChanges(); // SAVE #4

        // ============================================================
        // PHASE 7: Insert StructMembers (depends on Types, TypeRefs, FunctionSigs)
        // ============================================================
        _progressReporter?.Report(6, "Phase 7/10: Struct Members");
        var allStructMembers = new List<StructMemberModel>();
        foreach (var structModel in StructModels)
        {
            var fqn = structModel.FullyQualifiedNameWithTemplates;
            if (typeModelsByFqn.TryGetValue(fqn, out var typeModel))
            {
                foreach (var member in structModel.Members)
                {
                    member.StructTypeId = typeModel.Id;

                    if (member.TypeReference != null && member.TypeReference.IsArray)
                    {
                        // Array types already inserted, just set FK
                        if (member.TypeReference.Id != 0)
                            member.TypeReferenceId = member.TypeReference.Id;
                    }
                    else if (!string.IsNullOrEmpty(member.TypeString) &&
                             typeStringToRef.TryGetValue(member.TypeString, out var tr))
                    {
                        member.TypeReferenceId = tr.Id;
                        member.TypeReference = null;
                    }
                    else
                    {
                        member.TypeReference = null;
                    }

                    if (member.IsFunctionPointer && member.FunctionSignature != null)
                        member.FunctionSignatureId = member.FunctionSignature.Id;
                    allStructMembers.Add(member);
                }
            }
        }

        if (allStructMembers.Any())
            repo.InsertStructMembers(allStructMembers);

        // ============================================================
        // PHASE 8: Insert UnionMembers
        // ============================================================
        _progressReporter?.Report(7, "Phase 8/10: Union Members");
        var allUnionMembers = new List<StructMemberModel>();
        foreach (var unionModel in UnionModels)
        {
            var fqn = unionModel.FullyQualifiedNameWithTemplates;
            if (typeModelsByFqn.TryGetValue(fqn, out var typeModel))
            {
                foreach (var member in unionModel.Members)
                {
                    member.StructTypeId = typeModel.Id;
                    if (!string.IsNullOrEmpty(member.TypeString) &&
                        typeStringToRef.TryGetValue(member.TypeString, out var tr))
                        member.TypeReferenceId = tr.Id;
                    member.TypeReference = null;
                    if (member.IsFunctionPointer && member.FunctionSignature != null)
                        member.FunctionSignatureId = member.FunctionSignature.Id;
                    allUnionMembers.Add(member);
                }
            }
        }

        if (allUnionMembers.Any())
            repo.InsertStructMembers(allUnionMembers);
        typeRepoInstance.SaveChanges(); // SAVE #5

        // ============================================================
        // PHASE 9: Insert FunctionBodies
        // ============================================================
        _progressReporter?.Report(8, "Phase 9/10: Function Bodies");
        foreach (var func in FunctionBodyModels)
        {
            var fqn = func.FullyQualifiedName;
            if (fqn.Contains("::"))
            {
                var lastScope = fqn.LastIndexOf("::");
                var parentName = fqn.Substring(0, lastScope);
                if (typeModelsByFqn.TryGetValue(parentName, out var parentType))
                    func.ParentId = parentType.Id;
            }

            if (func.FunctionSignature != null)
            {
                var sig = func.FunctionSignature;
                if (sig.ReturnTypeReference != null &&
                    existingTypeRefsLookup.TryGetValue(sig.ReturnTypeReference.TypeString, out var retRef))
                {
                    sig.ReturnTypeReference = retRef;
                    sig.ReturnTypeReferenceId = retRef.Id;
                }

                foreach (var param in sig.Parameters)
                {
                    if (param.TypeReference != null &&
                        existingTypeRefsLookup.TryGetValue(param.TypeReference.TypeString, out var paramRef))
                    {
                        param.TypeReference = paramRef;
                        param.TypeReferenceId = paramRef.Id;
                    }
                }

                repo.InsertFunctionSignature(sig);
                func.FunctionSignatureId = sig.Id;
            }

            repo.InsertFunctionBody(func);
        }

        typeRepoInstance.SaveChanges(); // SAVE #6

        // ============================================================
        // PHASE 10: Resolution & Offsets - REMOVED (Handled by Post-Process in Program.cs via InMemoryTypeRepository)
        // ============================================================
        _progressReporter?.Report(9, "Phase 10/10: Resolution & Offsets");
        var resolutionService = new TypeResolutionService(repo, _progressReporter);
        resolutionService.ResolveTypeReferences();
        repo.PopulateBaseTypePaths(TypeModels);
        typeRepoInstance.SaveChanges(); // SAVE #7 (final)
        _progressReporter?.Finish("Database save completed.");
    }


    public void GenerateHeaderFiles(string outputDir = "./include/", ITypeRepository? repository = null)
    {
        var exporter = new FileOutputGenerator();
        exporter.GenerateHeaderFiles(TypeModels, outputDir, repository);
    }
}
