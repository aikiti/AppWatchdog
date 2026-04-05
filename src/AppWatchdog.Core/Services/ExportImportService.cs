using System.IO.Compression;
using Serilog;

namespace AppWatchdog.Core.Services;

public static class ExportImportService
{
    public static bool ExportZip(string zipPath, string configPath, string stateDir)
    {
        try
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            if (File.Exists(configPath))
                zip.CreateEntryFromFile(configPath, "appsettings.json");

            var statePath = Path.Combine(stateDir, "state.json");
            if (File.Exists(statePath))
                zip.CreateEntryFromFile(statePath, "state.json");

            Log.Information("Exported config + state to {Path}.", zipPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to export zip.");
            return false;
        }
    }

    public static bool ImportZip(string zipPath, string configPath, string stateDir)
    {
        try
        {
            using var zip = ZipFile.OpenRead(zipPath);

            var configEntry = zip.GetEntry("appsettings.json");
            if (configEntry != null)
            {
                var dir = Path.GetDirectoryName(configPath);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
                configEntry.ExtractToFile(configPath, overwrite: true);
            }

            var stateEntry = zip.GetEntry("state.json");
            if (stateEntry != null)
            {
                Directory.CreateDirectory(stateDir);
                stateEntry.ExtractToFile(Path.Combine(stateDir, "state.json"), overwrite: true);
            }

            Log.Information("Imported config + state from {Path}.", zipPath);
            return true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to import zip.");
            return false;
        }
    }
}
