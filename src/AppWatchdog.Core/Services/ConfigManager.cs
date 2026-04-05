using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatchdog.Core.Models;
using Serilog;

namespace AppWatchdog.Core.Services;

public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    public string ConfigPath { get; }

    public ConfigManager(string configPath)
    {
        ConfigPath = Path.GetFullPath(configPath);
    }

    public AppSettings Load()
    {
        if (!File.Exists(ConfigPath))
        {
            Log.Warning("Config file not found at {Path}. Creating default.", ConfigPath);
            var defaultSettings = CreateDefault();
            Save(defaultSettings);
            return defaultSettings;
        }

        try
        {
            var json = File.ReadAllText(ConfigPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
            if (settings == null)
            {
                Log.Warning("Config deserialized to null. Using defaults.");
                return CreateDefault();
            }
            Log.Information("Config loaded from {Path} with {Count} targets.", ConfigPath, settings.Targets.Count);
            return settings;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load config from {Path}. Using defaults.", ConfigPath);
            return CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        try
        {
            var dir = Path.GetDirectoryName(ConfigPath);
            if (!string.IsNullOrEmpty(dir))
                Directory.CreateDirectory(dir);

            var json = JsonSerializer.Serialize(settings, JsonOptions);
            File.WriteAllText(ConfigPath, json);
            Log.Information("Config saved to {Path}.", ConfigPath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to save config to {Path}.", ConfigPath);
            throw;
        }
    }

    public static AppSettings CreateDefault()
    {
        return new AppSettings
        {
            Targets = new List<WatchTarget>
            {
                new()
                {
                    Id = "example-app",
                    Enabled = false,
                    DisplayName = "Example Application",
                    ExePath = @"C:\Program Files\Example\example.exe",
                    DetectMode = "processName",
                    ProcessName = "example",
                    CheckIntervalSec = 5,
                    RestartDelaySec = 30,
                    StartGraceSec = 10,
                    EnsureRunningOnStart = true,
                    MaxRestartsInWindow = 6,
                    RestartWindowSec = 600,
                    CooldownSec = 300,
                    StopMethod = "none",
                    ManualStopBehavior = "noRestartUntilManualResume"
                }
            }
        };
    }

    public string ExportToJson(AppSettings settings)
    {
        return JsonSerializer.Serialize(settings, JsonOptions);
    }

    public AppSettings? ImportFromJson(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import settings from JSON.");
            return null;
        }
    }
}
