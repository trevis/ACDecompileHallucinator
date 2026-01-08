using ACTypeBrowser.Components;
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
var dbPath = Environment.GetEnvironmentVariable("ACTYPEBROWSER_DB_PATH");
var connectionString = string.IsNullOrEmpty(dbPath)
    ? builder.Configuration.GetConnectionString("DefaultConnection")
    : $"Data Source={dbPath}";

builder.Services.AddDbContext<TypeContext>(options =>
    options.UseSqlite(connectionString));

// Repositories and Services
builder.Services.AddScoped<ITypeRepository, TypeRepository>();
builder.Services.AddScoped<ITypeHierarchyService, TypeHierarchyService>();
builder.Services.AddScoped<HierarchyTreeBuilder>();

// Hierarchy Rule Engine
builder.Services.AddScoped<IInheritanceGraph>(sp =>
    InheritanceGraphBuilder.Build(sp.GetRequiredService<ITypeRepository>()));
builder.Services.AddScoped<HierarchyRuleEngine>(sp =>
{
    var engine = new HierarchyRuleEngine(sp.GetRequiredService<IInheritanceGraph>());
    engine.RegisterRules(DefaultHierarchyRules.GetDefaultRules());
    return engine;
});

// Performance optimization: TypeLookupCache for efficient type resolution
builder.Services.AddScoped<TypeLookupCache>();

// Theme Service
builder.Services.AddScoped<ACTypeBrowser.Services.ThemeService>();

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