using System.CommandLine;
using ACSourceHallucinator.Configuration;
using ACSourceHallucinator.Data;
using ACSourceHallucinator.Data.Repositories;
using ACSourceHallucinator.Interfaces;
using ACSourceHallucinator.Llm;
using ACSourceHallucinator.Models;
using ACSourceHallucinator.Pipeline;
using ACSourceHallucinator.ReferenceGeneration;
using ACSourceHallucinator.Stages;
using ACSourceHallucinator.Tui;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Spectre.Console;

// Assumes TypeContext is from the shared library
using ACDecompileParser.Shared.Lib.Storage;

namespace ACSourceHallucinator;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("ACSourceHallucinator - LLM-powered decompiled code enhancement");

        var sourceDbOption = new Option<string>(
                "--source-db",
                "Path to the source type database")
            { IsRequired = true };

        var hallucinatorDbOption = new Option<string>(
            "--hallucinator-db",
            () => "hallucinator.db",
            "Path to the hallucinator results database");

        var llmUrlOption = new Option<string>(
            "--llm-url",
            () => "http://localhost:1234/v1",
            "Base URL for the LLM API");

        var modelOption = new Option<string>(
                "--model",
                "LLM model name")
            { IsRequired = true };

        var maxTokensOption = new Option<int>(
            "--max-tokens",
            () => 2048,
            "Maximum tokens for LLM responses");

        var temperatureOption = new Option<double>(
            "--temperature",
            () => 0.7,
            "LLM temperature");

        var timeoutOption = new Option<int>(
            "--timeout",
            () => 15,
            "LLM request timeout in minutes");

        var maxRetriesOption = new Option<int>(
            "--max-retries",
            () => 5,
            "Maximum retry attempts per item");

        var skipCacheOption = new Option<bool>(
            "--skip-cache",
            "Skip the LLM response cache");

        var debugStructOption = new Option<string?>(
            "--debug-struct",
            "Process only a single struct by FullyQualifiedName");

        rootCommand.AddOption(sourceDbOption);
        rootCommand.AddOption(hallucinatorDbOption);
        rootCommand.AddOption(llmUrlOption);
        rootCommand.AddOption(modelOption);
        rootCommand.AddOption(maxTokensOption);
        rootCommand.AddOption(temperatureOption);
        rootCommand.AddOption(timeoutOption);
        rootCommand.AddOption(maxRetriesOption);
        rootCommand.AddOption(skipCacheOption);
        rootCommand.AddOption(debugStructOption);

        rootCommand.SetHandler(async (context) =>
        {
            var options = new CliOptions
            {
                SourceDbPath = context.ParseResult.GetValueForOption(sourceDbOption)!,
                HallucinatorDbPath = context.ParseResult.GetValueForOption(hallucinatorDbOption)!,
                LlmBaseUrl = context.ParseResult.GetValueForOption(llmUrlOption)!,
                LlmModel = context.ParseResult.GetValueForOption(modelOption)!,
                MaxTokens = context.ParseResult.GetValueForOption(maxTokensOption),
                Temperature = context.ParseResult.GetValueForOption(temperatureOption),
                TimeoutMinutes = context.ParseResult.GetValueForOption(timeoutOption),
                MaxRetries = context.ParseResult.GetValueForOption(maxRetriesOption),
                SkipCache = context.ParseResult.GetValueForOption(skipCacheOption),
                DebugStructFqn = context.ParseResult.GetValueForOption(debugStructOption)
            };

            await RunPipelineAsync(options, context.GetCancellationToken());
        });

        return await rootCommand.InvokeAsync(args);
    }

    private static async Task RunPipelineAsync(CliOptions options, CancellationToken ct)
    {
        var services = ConfigureServices(options);

        // Ensure hallucinator database is created
        using (var scope = services.CreateScope())
        {
            var hallucinatorDb = scope.ServiceProvider.GetRequiredService<HallucinatorDbContext>();
            await hallucinatorDb.Database.EnsureCreatedAsync(ct);
        }

        using var scope2 = services.CreateScope();
        var provider = scope2.ServiceProvider;

        var pipeline = new PipelineBuilder(provider)
            .AddStage<CommentFunctionsStage>()
            .AddStage<CommentStructsStage>()
            .AddStage<CommentEnumsStage>()
            // Add more stages as needed
            .Build();

        var pipelineOptions = provider.GetRequiredService<PipelineOptions>();

        var runner = provider.GetRequiredService<PipelineRunner>();
        await runner.RunAsync(pipeline, pipelineOptions, ct);
    }

    private static ServiceProvider ConfigureServices(CliOptions options)
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.AddFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning);
            builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
        });

        // Database contexts
        services.AddDbContext<TypeContext>(opt =>
            opt.UseSqlite($"Data Source={options.SourceDbPath}"));

        services.AddDbContext<HallucinatorDbContext>(opt =>
            opt.UseSqlite($"Data Source={options.HallucinatorDbPath}"));

        // LLM client options
        services.Configure<LlmClientOptions>(opt =>
        {
            opt.BaseUrl = options.LlmBaseUrl;
            opt.MaxTokens = options.MaxTokens;
            opt.Temperature = options.Temperature;
            opt.TimeoutMinutes = options.TimeoutMinutes;
            opt.SkipCache = options.SkipCache;
        });

        services.AddHttpClient<ILlmClient, OpenAiCompatibleClient>();
        services.AddScoped<ILlmCache, LlmCache>();

        // Repositories
        services.AddScoped<IStageResultRepository, StageResultRepository>();

        // Reference generation
        services.AddScoped<IReferenceTextGenerator, ReferenceTextGenerator>();

        // Stages
        services.AddScoped<CommentFunctionsStage>();
        services.AddScoped<CommentStructsStage>();
        services.AddScoped<CommentEnumsStage>();

        // Pipeline
        services.AddScoped<PipelineRunner>();

        // Pipeline options
        var pipelineOptions = new PipelineOptions
        {
            DebugFilterFqn = options.DebugStructFqn,
            SkipCache = options.SkipCache,
            MaxRetries = options.MaxRetries,
            Model = options.LlmModel
        };
        services.AddSingleton(pipelineOptions);

        // TUI
        services.AddSingleton<IAnsiConsole>(AnsiConsole.Console);
        services.AddScoped<PipelineTui>();

        return services.BuildServiceProvider();
    }
}