using AppWatchdog.Core.Models;

namespace AppWatchdog.Core.Services.HealthCheck;

public static class HealthCheckerFactory
{
    public static IHealthChecker? Create(HealthCheckConfig? config)
    {
        if (config == null || config.Type == "none")
            return null;

        return config.Type switch
        {
            "tcp" => new TcpHealthChecker(config.Host ?? "localhost", config.Port),
            "http" => new HttpHealthChecker(config.Url ?? "http://localhost"),
            "fileHeartbeat" => new FileHeartbeatChecker(config.FilePath ?? "", config.ThresholdSec),
            _ => null
        };
    }
}
