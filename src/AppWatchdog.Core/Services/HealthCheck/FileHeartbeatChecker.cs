using Serilog;

namespace AppWatchdog.Core.Services.HealthCheck;

public class FileHeartbeatChecker : IHealthChecker
{
    private readonly string _filePath;
    private readonly int _thresholdSec;

    public string Description => $"FileHeartbeat {_filePath} (threshold={_thresholdSec}s)";

    public FileHeartbeatChecker(string filePath, int thresholdSec)
    {
        _filePath = filePath;
        _thresholdSec = thresholdSec;
    }

    public Task<bool> CheckAsync(CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(_filePath))
            {
                Log.Debug("Heartbeat file not found: {Path}", _filePath);
                return Task.FromResult(false);
            }

            var lastWrite = File.GetLastWriteTimeUtc(_filePath);
            var age = (DateTime.UtcNow - lastWrite).TotalSeconds;
            var healthy = age <= _thresholdSec;

            if (!healthy)
                Log.Debug("Heartbeat file {Path} is stale: {Age:F0}s > {Threshold}s", _filePath, age, _thresholdSec);

            return Task.FromResult(healthy);
        }
        catch (Exception ex)
        {
            Log.Debug("FileHeartbeat check failed for {Path}: {Msg}", _filePath, ex.Message);
            return Task.FromResult(false);
        }
    }
}
