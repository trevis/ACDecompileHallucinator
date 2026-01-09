using System;
using System.Collections.Generic;
using System.Linq;
using ACDecompileParser.Shared.Lib.Constants;
using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Utilities;

namespace ACDecompileParser.Shared.Lib.Services;

public class OffsetCalculationService
{
    private readonly ITypeRepository _repository;
    private readonly Dictionary<string, int> _primitiveTypeSizes;

    // Caching for performance improvements
    private readonly Dictionary<string, int> _typeSizeCache = new Dictionary<string, int>();
    private readonly HashSet<string> _calculatingTypes = new HashSet<string>(); // To detect circular dependencies
    private Dictionary<int, List<TypeInheritance>> _inheritanceCache = new Dictionary<int, List<TypeInheritance>>();

    private Dictionary<int, List<StructMemberModel>> _structMembersCache =
        new Dictionary<int, List<StructMemberModel>>();

    private readonly IProgressReporter? _progressReporter;

    public OffsetCalculationService(ITypeRepository repository, IProgressReporter? progressReporter = null)
    {
        _repository = repository;
        _progressReporter = progressReporter;
        _primitiveTypeSizes = InitializePrimitiveTypeSizes();
    }

    private Dictionary<string, int> InitializePrimitiveTypeSizes()
    {
        // Initialize sizes for primitive types on x86 architecture
        var sizes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        // Add types to avoid duplicate keys
        sizes["bool"] = 1; // Boolean
        sizes["_BYTE"] = 1; // Alias for byte

        // Character types
        sizes["char"] = 1;
        sizes["wchar_t"] = 2; // Wide character
        sizes["char16_t"] = 2;
        sizes["char32_t"] = 4;

        // Integer types
        sizes["int8_t"] = 1;
        sizes["int16_t"] = 2;
        sizes["int32_t"] = 4;
        sizes["int64_t"] = 8;
        sizes["uint8_t"] = 1;
        sizes["uint16_t"] = 2;
        sizes["uint32_t"] = 4;
        sizes["uint64_t"] = 8;

        // Standard C++ types
        sizes["void"] = 0; // Void has no size
        sizes["int"] = 4; // Only add once - avoid duplicate with INT
        sizes["short"] = 2;
        sizes["long"] = 4; // On x86, long is typically 4 bytes
        sizes["signed"] = 4;
        sizes["unsigned"] = 4;
        sizes["float"] = 4;
        sizes["double"] = 8;

        // Sized types
        sizes["size_t"] = 4; // On x86, size_t is 4 bytes
        sizes["ptrdiff_t"] = 4; // On x86, ptrdiff_t is 4 bytes
        sizes["intptr_t"] = 4; // On x86, intptr_t is 4 bytes
        sizes["uintptr_t"] = 4; // On x86, uintptr_t is 4 bytes

        // Windows types
        sizes["BYTE"] = 1;
        sizes["WORD"] = 2;
        sizes["DWORD"] = 4;
        sizes["DWORD64"] = 8;
        sizes["QWORD"] = 8;
        sizes["DWORD_PTR"] = 4; // On x86, this is 4 bytes
        sizes["INT"] = 4; // This is the same as "int" but we'll keep it as they might be used differently
        sizes["UINT"] = 4;
        sizes["LONG"] = 4;
        sizes["ULONG"] = 4;
        sizes["LONGLONG"] = 8;
        sizes["ULONGLONG"] = 8;
        sizes["HRESULT"] = 4;
        sizes["BOOL"] = 4; // Windows BOOL is 4 bytes
        sizes["WCHAR"] = 2; // Wide character

        // Additional types
        sizes["nullptr_t"] = 4; // Pointer size
        sizes["time_t"] = 4; // On many systems
        sizes["clock_t"] = 4; // On many systems
        sizes["sig_atomic_t"] = 4; // Usually int-sized

        return sizes;
    }

    /// <summary>
    /// Calculates and applies offsets to all struct members in the repository
    /// </summary>
    public void CalculateAndApplyOffsets()
    {
        // Clear caches to ensure fresh calculations
        _typeSizeCache.Clear();
        _inheritanceCache.Clear();
        _structMembersCache.Clear();

        // Get all struct types
        var structTypes = _repository.GetTypesByTypeType(TypeType.Struct);

        // Pre-load all inheritance relationships to avoid repeated queries
        LoadInheritanceCache(structTypes);

        // Pre-load all struct members to avoid repeated queries
        LoadStructMembersCache(structTypes);

        // For each struct type, calculate member offsets
        _progressReporter?.Start("Calculating Offsets", structTypes.Count);
        int count = 0;
        foreach (var structType in structTypes)
        {
            CalculateStructMemberOffsets(structType);
            count++;
            if (count % 50 == 0) // Report every 50 structs to avoid spamming
                _progressReporter?.Report(count, $"Processing {structType.BaseName}");
        }

        _progressReporter?.Finish("Offset calculation completed.");

        // Update all modified struct members in the database
        // The offsets are updated directly on the models, so we need to save them
    }

    /// <summary>
    /// Calculates offsets for members of a specific struct
    /// </summary>
    public void CalculateStructMemberOffsets(TypeModel structType)
    {
        // Get all members for this struct from cache
        if (!_structMembersCache.TryGetValue(structType.Id, out var members))
        {
            // If not in cache, fetch from repository (fallback)
            members = _repository.GetStructMembersWithRelatedTypes(structType.Id).ToList();
        }

        // Calculate base class size if this struct inherits from others
        int baseClassSize = CalculateBaseClassSize(structType);

        // Calculate offsets for each member
        int currentOffset = baseClassSize;
        int currentBitOffset = 0; // Track bit position within the current byte
        int currentStorageUnitSize = 0; // Size of the current storage unit for bitfields (in bytes)
        int currentStorageUnitOffset = -1; // Offset where the current storage unit starts

        var membersWithOffsets = new List<(StructMemberModel member, int offset)>();

        foreach (var member in
                 members.OrderBy(m =>
                     m.DeclarationOrder)) // Order by declaration order to maintain source declaration order
        {
            // Bitfield handling
            if (member.BitFieldWidth.HasValue)
            {
                int bitWidth = member.BitFieldWidth.Value;
                int memberSize =
                    CalculateMemberSize(member); // This tells us the "storage unit" size (e.g., 4 bytes for int)

                // Check if we can pack this bitfield into the current storage unit
                // We need to check if:
                // 1. We are currently inside a bitfield sequence (currentStorageUnitOffset != -1)
                // 2. The type size matches the current storage unit (e.g., packing int bitfields together)
                // 3. There is enough space left in the current storage unit

                bool canPack = currentStorageUnitOffset != -1 &&
                               memberSize == currentStorageUnitSize &&
                               (currentBitOffset + bitWidth) <= (memberSize * 8);

                if (canPack)
                {
                    // Pack into existing unit
                    // The member's offset is the start of the storage unit
                    // Note: In some debug formats/pdb, bitfields share the same byte offset.
                    membersWithOffsets.Add((member, currentStorageUnitOffset));

                    currentBitOffset += bitWidth;
                }
                else
                {
                    // Start a new storage unit

                    // First, align properly for this new storage unit
                    int alignment = CalculateAlignment(member);

                    // If we were effectively at a bit offset in previous byte, we are now starting fresh
                    // But currentOffset already tracks byte boundaries. 
                    // However, if we were midway through a previous bitfield set, `currentOffset` might theoretically still point 
                    // to the *start* of that set if we didn't advance it.
                    // Let's adopt a strategy: `currentOffset` always points to the *next available byte address*.

                    if (currentStorageUnitOffset != -1)
                    {
                        // We are finishing a previous bitfield block. 
                        // Advance currentOffset past that block.
                        currentOffset = currentStorageUnitOffset + currentStorageUnitSize;
                    }

                    // Align for the new member
                    currentOffset = AlignOffset(currentOffset, alignment);

                    // This is the start of our new storage unit
                    currentStorageUnitOffset = currentOffset;
                    currentStorageUnitSize = memberSize;
                    currentBitOffset = bitWidth;

                    membersWithOffsets.Add((member, currentStorageUnitOffset));
                }
            }
            else
            {
                // Not a bitfield

                // If we were processing bitfields, finish that block
                if (currentStorageUnitOffset != -1)
                {
                    currentOffset = currentStorageUnitOffset + currentStorageUnitSize;
                    currentStorageUnitOffset = -1;
                    currentBitOffset = 0;
                    currentStorageUnitSize = 0;
                }

                // Calculate the size of this member
                int memberSize = CalculateMemberSize(member);

                // Apply alignment padding if necessary
                int alignment = CalculateAlignment(member);
                currentOffset = AlignOffset(currentOffset, alignment);

                // Store the calculated offset
                membersWithOffsets.Add((member, currentOffset));

                // Move to the next offset position
                currentOffset += memberSize;
            }
        }

        // Handle trailing bitfield block if the struct ends with one
        if (currentStorageUnitOffset != -1)
        {
            // We don't strictly need to update currentOffset here for members, 
            // but if we were calculating total struct size we would.
        }

        // Apply the calculated offsets to the members
        foreach (var (member, offset) in membersWithOffsets)
        {
            member.Offset = offset;
        }

        // Batch update all members in the repository
        foreach (var (member, _) in membersWithOffsets)
        {
            _repository.UpdateStructMember(member);
        }
    }

    /// <summary>
    /// Calculates the total size of all base classes for a struct
    /// </summary>
    public int CalculateBaseClassSize(TypeModel structType)
    {
        // Get all base types (inheritance relationships) from cache
        if (!_inheritanceCache.TryGetValue(structType.Id, out var baseTypes))
        {
            // If not in cache, fetch from repository (fallback)
            baseTypes = _repository.GetBaseTypesWithRelatedTypes(structType.Id).ToList();
        }

        int currentOffset = 0;

        // For each base type, calculate its size recursively
        foreach (var baseType in baseTypes.Where(bt => bt.RelatedType != null))
        {
            var relatedType = baseType.RelatedType!;
            int baseSize = CalculateTypeSize(relatedType);
            int baseAlignment = CalculateAlignment(relatedType);

            // Align the current offset for this base class
            currentOffset = AlignOffset(currentOffset, baseAlignment);

            // Add size
            currentOffset += baseSize;
        }

        return currentOffset;
    }

    /// <summary>
    /// Calculates the size of a type
    /// </summary>
    public int CalculateTypeSize(TypeModel type)
    {
        if (type == null) return 0;

        // Check if we already calculated this type's size
        string cacheKey = type.FullyQualifiedName ?? $"{type.Namespace}::{type.BaseName}";
        if (_typeSizeCache.TryGetValue(cacheKey, out int cachedSize))
        {
            return cachedSize;
        }

        // Check for circular dependencies
        if (_calculatingTypes.Contains(cacheKey))
        {
            // Return a default size to break the cycle
            return 4; // Default size for unknown types
        }

        // Mark this type as being calculated to detect circular dependencies
        _calculatingTypes.Add(cacheKey);

        try
        {
            // If it's a primitive type, return its size
            if (IsPrimitiveType(type.BaseName))
            {
                int primitiveSize = GetPrimitiveTypeSize(type.BaseName);
                _typeSizeCache[cacheKey] = primitiveSize;
                return primitiveSize;
            }

            // If it's a struct, calculate the total size of its members
            if (type.Type == TypeType.Struct)
            {
                // Get all members of this struct from cache
                if (!_structMembersCache.TryGetValue(type.Id, out var members))
                {
                    // If not in cache, fetch from repository (fallback)
                    members = _repository.GetStructMembersWithRelatedTypes(type.Id).ToList();
                }

                int maxAlignment = 1;

                // If the struct has explicit alignment, start with that
                if (type.Alignment.HasValue)
                {
                    maxAlignment = type.Alignment.Value;
                }

                // Calculate base class size and track alignment from bases
                int baseClassSize = 0;

                // Get base types to check their alignments
                if (!_inheritanceCache.TryGetValue(type.Id, out var baseTypes))
                {
                    baseTypes = _repository.GetBaseTypesWithRelatedTypes(type.Id).ToList();
                }

                foreach (var baseType in baseTypes.Where(bt => bt.RelatedType != null))
                {
                    var relatedType = baseType.RelatedType!;
                    int baseAlignment = CalculateAlignment(relatedType);
                    maxAlignment = Math.Max(maxAlignment, baseAlignment);

                    // Align base class offset
                    baseClassSize = AlignOffset(baseClassSize, baseAlignment);
                    baseClassSize += CalculateTypeSize(relatedType);
                }

                // Calculate size of members
                int currentOffset = baseClassSize;
                foreach (var member in members)
                {
                    int alignment = CalculateAlignment(member);
                    maxAlignment = Math.Max(maxAlignment, alignment);

                    currentOffset = AlignOffset(currentOffset, alignment);
                    currentOffset += CalculateMemberSize(member);
                }

                // Pad the final struct size to be a multiple of the largest alignment
                int totalSize = AlignOffset(currentOffset, maxAlignment);

                _typeSizeCache[cacheKey] = totalSize;
                return totalSize;
            }

            // For other types (enums, etc.), default to 4 bytes or 0 if unknown
            int defaultSize = 4;
            _typeSizeCache[cacheKey] = defaultSize;
            return defaultSize;
        }
        finally
        {
            // Remove from calculating set to allow future calculations
            _calculatingTypes.Remove(cacheKey);
        }
    }

    /// <summary>
    /// Calculates the size of a specific member
    /// </summary>
    public int CalculateMemberSize(StructMemberModel member)
    {
        // If this is a function pointer, it's essentially a pointer
        if (member.IsFunctionPointer)
        {
            return 4; // Function pointers are 4 bytes on x86
        }

        // Check if the member has a TypeReference
        if (member.TypeReference != null)
        {
            // Handle arrays
            if (member.TypeReference.IsArray)
            {
                int elementSize = 0;

                // If the element type is a pointer, the size is 4 bytes (on x86)
                if (member.TypeReference.IsPointer)
                {
                    elementSize = 4;
                }
                // If we have a resolved type reference, use its size
                else if (member.TypeReference.ReferencedTypeId.HasValue)
                {
                    var referencedType = _repository.GetTypeById(member.TypeReference.ReferencedTypeId.Value);
                    elementSize = CalculateTypeSize(referencedType!);
                }
                else
                {
                    // If not resolved, try to get size from type string
                    elementSize = GetSizeFromTypeString(member.TypeReference.TypeString);
                }

                // If element size is 0 (unknown), default to 1 byte
                if (elementSize == 0) elementSize = 1;

                // Calculate total array size
                int arraySize = member.TypeReference.ArraySize ?? 1; // If size is null, treat as 1 element
                return elementSize * arraySize;
            }

            // Handle pointers - each pointer is 4 bytes on x86
            if (member.TypeReference.IsPointer)
            {
                return 4; // All pointers are 4 bytes on x86, regardless of depth
            }
        }

        // If we have a referenced type through TypeReferenceId
        if (member.TypeReferenceId.HasValue)
        {
            var typeReference = _repository.GetTypeReferenceById(member.TypeReferenceId.Value);
            if (typeReference != null && typeReference.ReferencedTypeId.HasValue)
            {
                var referencedType = _repository.GetTypeById(typeReference.ReferencedTypeId.Value);
                return CalculateTypeSize(referencedType!);
            }
        }

        // Try to determine size from the TypeString
        return GetSizeFromTypeString(member.TypeString);
    }

    /// <summary>
    /// Gets the size of a type based on its string representation
    /// </summary>
    public int GetSizeFromTypeString(string typeString)
    {
        if (string.IsNullOrEmpty(typeString))
            return 4; // Default size if type string is empty

        // Check if it's a pointer type (ends with *)
        // Note: We need to handle const/volatile modifiers, but ExtractBaseTypeName removes those.
        // We can check the raw string for * before cleaning, but we must be careful about templates like MyType<int*>.
        // However, standard pointer syntax puts * at the end (or before name).
        // Since we are looking at the type definition, if it ends with *, it's a pointer.
        // We trim whitespace first.
        if (typeString.TrimEnd().EndsWith("*"))
        {
            return 4;
        }

        // Extract base type name (remove modifiers like *, &, const)
        string baseTypeName = ExtractBaseTypeName(typeString);

        // Check if it's a primitive type
        if (IsPrimitiveType(baseTypeName))
        {
            return GetPrimitiveTypeSize(baseTypeName);
        }

        // If it's not a primitive type, we need to look it up in the repository
        var typeModel = _repository.GetTypeByFullyQualifiedName(typeString);
        if (typeModel != null)
        {
            return CalculateTypeSize(typeModel);
        }

        // If we can't find the type, default to 4 bytes
        return 4;
    }

    /// <summary>
    /// Checks if a type name corresponds to a primitive type
    /// </summary>
    public bool IsPrimitiveType(string typeName)
    {
        return _primitiveTypeSizes.ContainsKey(typeName) ||
               PrimitiveTypes.TypeNames.Contains(typeName);
    }

    /// <summary>
    /// Gets the size of a primitive type
    /// </summary>
    public int GetPrimitiveTypeSize(string typeName)
    {
        if (_primitiveTypeSizes.TryGetValue(typeName, out int size))
        {
            return size;
        }

        // Check in the PrimitiveTypes constants as well
        if (PrimitiveTypes.TypeNames.Contains(typeName))
        {
            // For types not in our size map, default to 4 bytes
            return 4;
        }

        // Default size if unknown
        return 4;
    }

    /// <summary>
    /// Calculates the required alignment for a member
    /// </summary>
    public int CalculateAlignment(StructMemberModel member)
    {
        // If the member has an explicit alignment specified, use that
        if (member.Alignment.HasValue)
        {
            return member.Alignment.Value;
        }

        // Otherwise, determine alignment based on type
        int size = 0;

        // Check if the member has a TypeReference
        if (member.TypeReference != null)
        {
            // Handle arrays
            if (member.TypeReference.IsArray)
            {
                // Alignment for arrays is the alignment of the element type
                if (member.TypeReference.ReferencedTypeId.HasValue)
                {
                    var referencedType = _repository.GetTypeById(member.TypeReference.ReferencedTypeId.Value);
                    size = CalculateTypeSize(referencedType!);
                }
                else
                {
                    size = GetSizeFromTypeString(member.TypeReference.TypeString);
                }
            }
            // Handle pointers
            else if (member.TypeReference.IsPointer)
            {
                return 4; // Pointers align to 4-byte boundaries on x86
            }
        }

        // If we have a referenced type through TypeReferenceId
        if (member.TypeReferenceId.HasValue)
        {
            var typeReference = _repository.GetTypeReferenceById(member.TypeReferenceId.Value);
            if (typeReference != null && typeReference.ReferencedTypeId.HasValue)
            {
                var referencedType = _repository.GetTypeById(typeReference.ReferencedTypeId.Value);
                size = CalculateTypeSize(referencedType!);
            }
        }

        // If we couldn't determine size from references, try TypeString
        if (size == 0)
        {
            size = GetSizeFromTypeString(member.TypeString);
        }

        // Default alignment rules:
        // - 1-byte types align to 1-byte boundary
        // - 2-byte types align to 2-byte boundary
        // - 4-byte types align to 4-byte boundary
        // - 8-byte types align to 4-byte boundary on x86 (not 8-byte due to architecture)
        if (size <= 1) return 1;
        if (size <= 2) return 2;
        if (size <= 4) return 4;
        return 4; // On x86, even 8-byte types typically align to 4-byte boundary
    }

    /// <summary>
    /// Calculates the required alignment for a type
    /// </summary>
    public int CalculateAlignment(TypeModel type)
    {
        // If the type has an explicit alignment, use that
        if (type.Alignment.HasValue)
        {
            return type.Alignment.Value;
        }

        // If it's a primitive type, use its size as alignment (up to 4/8 bytes)
        if (IsPrimitiveType(type.BaseName))
        {
            int size = GetPrimitiveTypeSize(type.BaseName);
            if (size <= 1) return 1;
            if (size <= 2) return 2;
            if (size <= 4) return 4;
            return 8; // Double or long long
        }

        // If it's a struct, its alignment is the maximum alignment of its members
        if (type.Type == TypeType.Struct)
        {
            // To avoid infinite recursion/calculating everything, we can try to guess
            // But ideally we should look at members. 
            // For now, default to 4 for non-primitives to avoid deep recursion during simple checks
            return 4;
        }

        return 4;
    }

    /// <summary>
    /// Aligns an offset to the specified alignment boundary
    /// </summary>
    public int AlignOffset(int offset, int alignment)
    {
        if (alignment <= 1) return offset;

        // Round up to the next alignment boundary
        int remainder = offset % alignment;
        if (remainder == 0) return offset;

        return offset + (alignment - remainder);
    }

    /// <summary>
    /// Loads all inheritance relationships into cache to avoid repeated database queries
    /// </summary>
    private void LoadInheritanceCache(IEnumerable<TypeModel> structTypes)
    {
        _inheritanceCache.Clear();

        // Extract all type IDs
        var typeIds = structTypes.Select(t => t.Id).ToList();

        // Bulk fetch all inheritance relationships in a single query
        _inheritanceCache = _repository.GetBaseTypesForMultipleTypes(typeIds);
    }

    /// <summary>
    /// Loads all struct members into cache to avoid repeated database queries
    /// </summary>
    private void LoadStructMembersCache(IEnumerable<TypeModel> structTypes)
    {
        _structMembersCache.Clear();

        // Extract all type IDs
        var typeIds = structTypes.Select(t => t.Id).ToList();

        // Bulk fetch all struct members in a single query
        _structMembersCache = _repository.GetStructMembersForMultipleTypes(typeIds);
    }

    /// <summary>
    /// Extracts the base type name from a type string, removing modifiers like *, &, const
    /// </summary>
    private string ExtractBaseTypeName(string typeString)
    {
        return ParsingUtilities.ExtractBaseTypeName(typeString);
    }
}
