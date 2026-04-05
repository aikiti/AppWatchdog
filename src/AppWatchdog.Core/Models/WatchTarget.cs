using System.Text.Json.Serialization;

namespace AppWatchdog.Core.Models;

public class WatchTarget
{
    public string Id { get; set; } = "";
    public bool Enabled { get; set; } = true;
    public string DisplayName { get; set; } = "";
    public string ExePath { get; set; } = "";
    public string? WorkDir { get; set; }
    public string? Args { get; set; }
    public string? ProcessName { get; set; }
    public string DetectMode { get; set; } = "processName"; // "processName" | "exePath"
    public int CheckIntervalSec { get; set; } = 5;
    public int RestartDelaySec { get; set; } = 30;
    public int StartGraceSec { get; set; } = 10;
    public bool EnsureRunningOnStart { get; set; } = true;
    public bool AllowMultipleInstances { get; set; } = false;
    public int MaxRestartsInWindow { get; set; } = 6;
    public int RestartWindowSec { get; set; } = 600;
    public int CooldownSec { get; set; } = 300;
    public string StopMethod { get; set; } = "none"; // "none" | "closeWindow" | "kill"
    public string ManualStopBehavior { get; set; } = "noRestartUntilManualResume";
    public HealthCheckConfig? HealthCheck { get; set; }

    public string GetEffectiveProcessName()
    {
        if (!string.IsNullOrWhiteSpace(ProcessName))
            return ProcessName;
        return Path.GetFileNameWithoutExtension(ExePath);
    }

    public string GetEffectiveWorkDir()
    {
        if (!string.IsNullOrWhiteSpace(WorkDir))
            return WorkDir;
        return Path.GetDirectoryName(ExePath) ?? ".";
    }

    public WatchTarget Clone()
    {
        return new WatchTarget
        {
            Id = Id,
            Enabled = Enabled,
            DisplayName = DisplayName,
            ExePath = ExePath,
            WorkDir = WorkDir,
            Args = Args,
            ProcessName = ProcessName,
            DetectMode = DetectMode,
            CheckIntervalSec = CheckIntervalSec,
            RestartDelaySec = RestartDelaySec,
            StartGraceSec = StartGraceSec,
            EnsureRunningOnStart = EnsureRunningOnStart,
            AllowMultipleInstances = AllowMultipleInstances,
            MaxRestartsInWindow = MaxRestartsInWindow,
            RestartWindowSec = RestartWindowSec,
            CooldownSec = CooldownSec,
            StopMethod = StopMethod,
            ManualStopBehavior = ManualStopBehavior,
            HealthCheck = HealthCheck
        };
    }
}
