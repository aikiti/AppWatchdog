namespace AppWatchdog.Core.Models;

public class HealthCheckConfig
{
    public string Type { get; set; } = "none"; // "none", "tcp", "http", "fileHeartbeat"
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? Url { get; set; }
    public string? FilePath { get; set; }
    public int ThresholdSec { get; set; } = 60;
    public int IntervalSec { get; set; } = 30;
    public int FailureCountForHang { get; set; } = 3;
    public bool RestartOnHang { get; set; } = true;
}
