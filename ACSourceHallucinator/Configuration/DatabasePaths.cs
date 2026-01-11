namespace ACSourceHallucinator.Configuration;

public static class DatabasePaths
{
    private const string DefaultDataDir = "/projects/ACDecompileHallucinator/out/";
    private const string TypesDbName = "types.db";
    private const string HallucinatorDbName = "hallucinator.db";
    private const string LlmCacheDbName = "llmcache.db";

    public static string GetDataDirectory()
    {
        return Environment.GetEnvironmentVariable("AC_DATA_DIRECTORY") ?? DefaultDataDir;
    }

    public static string GetTypesDbPath(string? overridePath = null)
    {
        if (!string.IsNullOrEmpty(overridePath)) return Path.GetFullPath(overridePath);
        return Path.GetFullPath(Path.Combine(GetDataDirectory(), TypesDbName));
    }

    public static string GetHallucinatorDbPath(string? overridePath = null)
    {
        if (!string.IsNullOrEmpty(overridePath)) return Path.GetFullPath(overridePath);
        return Path.GetFullPath(Path.Combine(GetDataDirectory(), HallucinatorDbName));
    }

    public static string GetLlmCacheDbPath()
    {
        return Path.GetFullPath(Path.Combine(GetDataDirectory(), LlmCacheDbName));
    }
}
