using ACDecompileParser.Shared.Lib.Models;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace ACTypeBrowser.Services;

/// <summary>
/// Caches the sidebar hierarchy tree to avoid rebuilding on every navigation.
/// Pre-built at startup and only rebuilt when search term changes.
/// </summary>
public class SidebarTreeCache
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ITypeHierarchyService _hierarchyService;
    private readonly HierarchyRuleEngine _ruleEngine;
    private readonly HierarchyTreeBuilder _treeBuilder;

    private HierarchyNode? _cachedFullTree;
    private readonly object _lock = new();
    private bool _isLoaded;

    // Search cache - stores recent search results
    private readonly Dictionary<string, HierarchyNode> _searchCache = new(StringComparer.OrdinalIgnoreCase);
    private const int MaxSearchCacheSize = 10;

    public SidebarTreeCache(
        IServiceScopeFactory scopeFactory,
        ITypeHierarchyService hierarchyService,
        HierarchyRuleEngine ruleEngine)
    {
        _scopeFactory = scopeFactory;
        _hierarchyService = hierarchyService;
        _ruleEngine = ruleEngine;
        _treeBuilder = new HierarchyTreeBuilder();
    }

    /// <summary>
    /// Ensures the full tree is loaded. Safe to call multiple times.
    /// </summary>
    public void EnsureLoaded()
    {
        if (_isLoaded) return;

        lock (_lock)
        {
            if (_isLoaded) return;
            BuildFullTree();
            _isLoaded = true;
        }
    }

    private void BuildFullTree()
    {
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITypeRepository>();

        var allTypes = repository.GetAllTypes(includeIgnored: false)
            .Where(t => !t.BaseName.StartsWith("$"))
            .ToList();

        var grouped = _hierarchyService.GroupTypesByBaseNameAndNamespace(allTypes, _ruleEngine);
        _cachedFullTree = _treeBuilder.BuildTree(grouped);
    }

    /// <summary>
    /// Gets the full cached tree. Call EnsureLoaded() first or this returns null.
    /// </summary>
    public HierarchyNode? GetFullTree()
    {
        EnsureLoaded();
        return _cachedFullTree;
    }

    /// <summary>
    /// Gets a filtered tree for the given search term.
    /// Results are cached for performance.
    /// </summary>
    public HierarchyNode? SearchTree(string searchTerm)
    {
        if (string.IsNullOrWhiteSpace(searchTerm))
            return GetFullTree();

        var normalizedTerm = searchTerm.Trim();

        // Check cache first
        if (_searchCache.TryGetValue(normalizedTerm, out var cached))
            return cached;

        // Build search result
        using var scope = _scopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ITypeRepository>();

        var searchResults = repository.SearchTypes(normalizedTerm, includeIgnored: false)
            .Where(t => !t.BaseName.StartsWith("$"))
            .ToList();

        var grouped = _hierarchyService.GroupTypesByBaseNameAndNamespace(searchResults, _ruleEngine);
        var searchTree = _treeBuilder.BuildTree(grouped);

        // Add to cache, evicting oldest if needed
        lock (_lock)
        {
            if (_searchCache.Count >= MaxSearchCacheSize)
            {
                // Simple eviction - remove first entry
                var firstKey = _searchCache.Keys.First();
                _searchCache.Remove(firstKey);
            }

            _searchCache[normalizedTerm] = searchTree;
        }

        return searchTree;
    }

    /// <summary>
    /// Clears the search cache. Useful if underlying data changes.
    /// </summary>
    public void ClearSearchCache()
    {
        lock (_lock)
        {
            _searchCache.Clear();
        }
    }
}
