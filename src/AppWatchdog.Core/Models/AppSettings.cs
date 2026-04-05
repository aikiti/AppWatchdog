namespace AppWatchdog.Core.Models;

public class AppSettings
{
    public List<WatchTarget> Targets { get; set; } = new();
    public string LogDir { get; set; } = "logs";
    public string StateDir { get; set; } = "state";
    public int LogRetainDays { get; set; } = 30;
    public bool ManualStopHeuristic { get; set; } = false;
    public int ManualStopHeuristicWindowSec { get; set; } = 10;
}
