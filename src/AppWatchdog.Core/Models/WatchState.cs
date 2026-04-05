namespace AppWatchdog.Core.Models;

public class TargetState
{
    public string TargetId { get; set; } = "";
    public bool Paused { get; set; } = false;
    public DateTime? PausedAt { get; set; }
    public DateTime? LastStopCommandAt { get; set; }
    public DateTime? LastRestartAt { get; set; }
    public int RestartCountInWindow { get; set; } = 0;
    public DateTime? WindowStartTime { get; set; }
    public DateTime? CooldownUntil { get; set; }
    public int ConsecutiveHealthFailures { get; set; } = 0;
}

public class WatchStateFile
{
    public Dictionary<string, TargetState> Targets { get; set; } = new();
    public DateTime LastSaved { get; set; } = DateTime.UtcNow;
}
