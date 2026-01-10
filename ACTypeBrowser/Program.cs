using ACTypeBrowser.Components;
using ACTypeBrowser.Services;
using ACDecompileParser.Shared.Lib.Storage;
using ACDecompileParser.Shared.Lib.Services;
using ACDecompileParser.Shared.Lib.Output;
using ACDecompileParser.Shared.Lib.Output.CSharp;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls("http://*:5112");

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// Database
var dbPath = Path.GetFullPath(Environment.GetEnvironmentVariable("ACTYPEBROWSER_DB_PATH") ??
                              "/projects/ACDecompileHallucinator/out/types.db");
var connectionString = string.IsNullOrEmpty(dbPath)
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : $"Data Source={dbPath}";

if (!File.Exists(dbPath))
{
    throw new Exception($"Error: Type database not found at {dbPath}");
}

builder.Services.AddDbContext<TypeContext>(options =>
    options.UseSqlite(connectionString));

// Hallucinator DB
var hallucinatorDbPath = Path.GetFullPath(Environment.GetEnvironmentVariable("HALLUCINATOR_DB_PATH") ??
                                          "/projects/ACDecompileHallucinator/out/hallucinator.db");
string hallucinatorConnectionString = $"Data Source={hallucinatorDbPath}";

if (!File.Exists(hallucinatorDbPath))
{
    throw new Exception($"Error: Hallucinator database not found at {hallucinatorDbPath}");
}

builder.Services.AddDbContext<ACSourceHallucinator.Data.HallucinatorDbContext>(options =>
    options.UseSqlite(hallucinatorConnectionString));

// Repositories and Services
builder.Services
    .AddScoped<ACSourceHallucinator.Data.Repositories.IStageResultRepository,
        ACSourceHallucinator.Data.Repositories.StageResultRepository>();
builder.Services.AddScoped<SqlTypeRepository>();
builder.Services.AddSingleton<ITypeRepository, InMemoryTypeRepository>();
builder.Services.AddSingleton<ITypeHierarchyService, TypeHierarchyService>();
builder.Services.AddScoped<HierarchyTreeBuilder>();

// Hierarchy Rule Engine
builder.Services.AddSingleton<IInheritanceGraph>(sp =>
{
    using var scope = sp.CreateScope();
    var repo = scope.ServiceProvider.GetRequiredService<ITypeRepository>();
    return InheritanceGraphBuilder.Build(repo);
});
builder.Services.AddSingleton<HierarchyRuleEngine>(sp =>
{
    var engine = new HierarchyRuleEngine(sp.GetRequiredService<IInheritanceGraph>());
    engine.RegisterRules(DefaultHierarchyRules.GetDefaultRules());
    return engine;
});

// Performance optimization: TypeLookupCache for efficient type resolution
builder.Services.AddSingleton<TypeLookupCache>(sp =>
    new TypeLookupCache(sp.GetRequiredService<IServiceScopeFactory>()));

// Performance optimization: SidebarTreeCache for pre-built sidebar tree
builder.Services.AddSingleton<SidebarTreeCache>();

// Theme Service
builder.Services.AddScoped<ThemeService>();
builder.Services.AddScoped<PageTitleService>();

// Code Generators
builder.Services.AddScoped<ICodeGenerator, StructOutputGenerator>();
builder.Services.AddScoped<ICodeGenerator, EnumOutputGenerator>();
builder.Services.AddScoped<TypeGroupProcessor>(sp =>
    new TypeGroupProcessor(
        sp.GetRequiredService<ITypeRepository>(),
        sp.GetRequiredService<TypeLookupCache>(),
        sp.GetRequiredService<ITypeHierarchyService>()));

builder.Services.AddScoped<CSharpGroupProcessor>(sp =>
    new CSharpGroupProcessor(
        sp.GetRequiredService<ITypeRepository>(),
        sp.GetRequiredService<TypeLookupCache>(),
        sp.GetRequiredService<ITypeHierarchyService>()));


var app = builder.Build();

// Pre-warm caches at startup for faster first page load
Console.WriteLine("Pre-warming caches...");
var typeRepo = app.Services.GetRequiredService<ITypeRepository>();
if (typeRepo is InMemoryTypeRepository inMemoryRepo)
{
    inMemoryRepo.EnsureLoaded();
    Console.WriteLine("  InMemoryTypeRepository loaded.");
}

var lookupCache = app.Services.GetRequiredService<TypeLookupCache>();
lookupCache.EnsureLoaded();
Console.WriteLine("  TypeLookupCache loaded.");

var sidebarCache = app.Services.GetRequiredService<SidebarTreeCache>();
sidebarCache.EnsureLoaded();
Console.WriteLine("  SidebarTreeCache loaded.");
Console.WriteLine("Cache pre-warming complete.");

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();