using Microsoft.Extensions.DependencyInjection;
using ACDecompileParser.Shared.Lib.Storage;

namespace ACDecompileParser.Shared.Lib.Services;

/// <summary>
/// Lightweight entry for type lookup without loading full TypeModel
/// </summary>
public record TypeLookupEntry(int Id, string BaseName, string Namespace, string? StoredFqn);

/// <summary>
/// Caches type name to ID mappings for efficient lookups without database queries.
/// Designed to be used as a scoped service (one instance per request) in web apps,
/// or instantiated directly for batch operations like header file generation.
/// </summary>
public class TypeLookupCache
{
    private readonly IServiceScopeFactory? _scopeFactory;
    private readonly ITypeRepository? _directRepository;
    private Dictionary<int, TypeLookupEntry>? _byId;
    private Dictionary<string, int>? _byFqn;
    private Dictionary<string, List<int>>? _byBaseName;
    private bool _isLoaded;
    private readonly object _lock = new();

    [ActivatorUtilitiesConstructor]
    public TypeLookupCache(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public TypeLookupCache(ITypeRepository repository)
    {
        _directRepository = repository;
    }

    /// <summary>
    /// Lazily loads all type lookup data from the repository.
    /// Safe to call multiple times - only loads once.
    /// </summary>
    public void EnsureLoaded()
    {
        if (_isLoaded) return;

        lock (_lock)
        {
            if (_isLoaded) return;

            List<(int Id, string BaseName, string Namespace, string? StoredFqn)> data;

            if (_directRepository != null)
            {
                data = _directRepository.GetTypeLookupData();
            }
            else
            {
                using var scope = _scopeFactory!.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<ITypeRepository>();
                data = repository.GetTypeLookupData();
            }

            _byId = new Dictionary<int, TypeLookupEntry>();
            _byFqn = new Dictionary<string, int>(StringComparer.Ordinal);
            _byBaseName = new Dictionary<string, List<int>>(StringComparer.Ordinal);

            foreach (var (id, baseName, ns, fqn) in data)
            {
                var entry = new TypeLookupEntry(id, baseName, ns, fqn);
                _byId[id] = entry;

                if (!string.IsNullOrEmpty(fqn))
                    _byFqn[fqn] = id;

                if (!_byBaseName.TryGetValue(baseName, out var list))
                {
                    list = new List<int>();
                    _byBaseName[baseName] = list;
                }

                list.Add(id);
            }

            _isLoaded = true;
        }
    }

    /// <summary>
    /// Tries to get a type ID by its fully qualified name.
    /// </summary>
    public bool TryGetIdByFqn(string fqn, out int id)
    {
        EnsureLoaded();
        return _byFqn!.TryGetValue(fqn, out id);
    }

    /// <summary>
    /// Tries to get all type IDs with a given base name.
    /// </summary>
    public bool TryGetIdsByBaseName(string baseName, out List<int>? ids)
    {
        EnsureLoaded();
        return _byBaseName!.TryGetValue(baseName, out ids);
    }

    /// <summary>
    /// Tries to get a type lookup entry by ID.
    /// </summary>
    public bool TryGetEntry(int id, out TypeLookupEntry? entry)
    {
        EnsureLoaded();
        return _byId!.TryGetValue(id, out entry);
    }
}
