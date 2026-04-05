using AppWatchdog.Core.Models;
using AppWatchdog.Core.Services.HealthCheck;
using Serilog;

namespace AppWatchdog.Core.Services;

public class WatchdogEngine : IDisposable
{
    private readonly ConfigManager _configManager;
    private readonly StateManager _stateManager;
    private AppSettings _settings;
    private CancellationTokenSource? _cts;
    private readonly Dictionary<string, Task> _watchTasks = new();
    private readonly Dictionary<string, IHealthChecker?> _healthCheckers = new();
    private bool _running;

    public event Action<string, string>? OnStatusChanged;

    public AppSettings Settings => _settings;
    public StateManager StateManager => _stateManager;
    public ConfigManager ConfigManager => _configManager;
    public bool IsRunning => _running;

    public WatchdogEngine(ConfigManager configManager, StateManager stateManager)
    {
        _configManager = configManager;
        _stateManager = stateManager;
        _settings = configManager.Load();
    }

    public void ReloadConfig()
    {
        _settings = _configManager.Load();
        Log.Information("Config reloaded. {Count} targets.", _settings.Targets.Count);
    }

    public void Start()
    {
        if (_running) return;
        _running = true;
        _cts = new CancellationTokenSource();

        foreach (var target in _settings.Targets)
        {
            if (!target.Enabled)
            {
                Log.Information("Target {Id} is disabled, skipping.", target.Id);
                continue;
            }

            _healthCheckers[target.Id] = HealthCheckerFactory.Create(target.HealthCheck);
            var task = Task.Run(() => WatchTargetLoop(target, _cts.Token));
            _watchTasks[target.Id] = task;
        }

        Log.Information("Watchdog engine started with {Count} active targets.",
            _watchTasks.Count);
    }

    public void Stop()
    {
        if (!_running) return;
        _running = false;
        _cts?.Cancel();
        try
        {
            Task.WhenAll(_watchTasks.Values).Wait(TimeSpan.FromSeconds(10));
        }
        catch { }
        _watchTasks.Clear();
        _healthCheckers.Clear();
        Log.Information("Watchdog engine stopped.");
    }

    public void Restart()
    {
        Stop();
        ReloadConfig();
        Start();
    }

    public void PauseTarget(string targetId)
    {
        _stateManager.SetPaused(targetId, true);
        OnStatusChanged?.Invoke(targetId, "Paused");
    }

    public void ResumeTarget(string targetId)
    {
        _stateManager.SetPaused(targetId, false);
        OnStatusChanged?.Invoke(targetId, "Resumed");
    }

    public void StopTarget(string targetId)
    {
        var target = _settings.Targets.FirstOrDefault(t => t.Id == targetId);
        if (target == null) return;

        _stateManager.RecordStopCommand(targetId);
        _stateManager.SetPaused(targetId, true);
        ProcessMonitor.StopProcess(target);
        OnStatusChanged?.Invoke(targetId, "Stopped & Paused");
    }

    public void StartTarget(string targetId)
    {
        var target = _settings.Targets.FirstOrDefault(t => t.Id == targetId);
        if (target == null) return;

        _stateManager.SetPaused(targetId, false);
        if (!ProcessMonitor.IsRunning(target))
        {
            ProcessMonitor.StartProcess(target);
        }
        OnStatusChanged?.Invoke(targetId, "Started");
    }

    public List<TargetStatusInfo> GetTargetStatuses()
    {
        var result = new List<TargetStatusInfo>();
        foreach (var target in _settings.Targets)
        {
            var state = _stateManager.GetTargetState(target.Id);
            var running = ProcessMonitor.IsRunning(target);
            var paused = _stateManager.IsPaused(target.Id);
            var cooldown = _stateManager.IsInCooldown(target.Id);

            result.Add(new TargetStatusInfo
            {
                Id = target.Id,
                DisplayName = target.DisplayName,
                Enabled = target.Enabled,
                IsRunning = running,
                IsPaused = paused,
                IsInCooldown = cooldown,
                RestartCount = state.RestartCountInWindow,
                LastRestartAt = state.LastRestartAt,
                CooldownUntil = state.CooldownUntil
            });
        }
        return result;
    }

    private async Task WatchTargetLoop(WatchTarget target, CancellationToken ct)
    {
        Log.Information("Starting watch loop for {Id} (detectMode={Mode}, processName={Name}).",
            target.Id, target.DetectMode, target.GetEffectiveProcessName());

        if (target.EnsureRunningOnStart && !_stateManager.IsPaused(target.Id))
        {
            if (!ProcessMonitor.IsRunning(target))
            {
                Log.Information("Target {Id} not running on start. Launching.", target.Id);
                ProcessMonitor.StartProcess(target);
                await SafeDelay(target.StartGraceSec * 1000, ct);
            }
        }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SafeDelay(target.CheckIntervalSec * 1000, ct);
                if (ct.IsCancellationRequested) break;

                if (_stateManager.IsPaused(target.Id))
                {
                    continue;
                }

                if (_stateManager.IsInCooldown(target.Id))
                {
                    Log.Debug("Target {Id} is in cooldown.", target.Id);
                    continue;
                }

                var isRunning = ProcessMonitor.IsRunning(target);

                if (!isRunning)
                {
                    if (_settings.ManualStopHeuristic &&
                        _stateManager.WasRecentlyManualStopped(target.Id, _settings.ManualStopHeuristicWindowSec))
                    {
                        Log.Information("Target {Id} disappeared but was recently manually stopped. Pausing.", target.Id);
                        _stateManager.SetPaused(target.Id, true);
                        OnStatusChanged?.Invoke(target.Id, "Manual stop detected → Paused");
                        continue;
                    }

                    Log.Warning("Target {Id} is NOT running. Waiting {Delay}s before restart.",
                        target.Id, target.RestartDelaySec);
                    OnStatusChanged?.Invoke(target.Id, "Down - waiting to restart");

                    await SafeDelay(target.RestartDelaySec * 1000, ct);
                    if (ct.IsCancellationRequested) break;

                    if (_stateManager.IsPaused(target.Id)) continue;

                    // Double-check after delay
                    if (ProcessMonitor.IsRunning(target))
                    {
                        Log.Information("Target {Id} came back during delay. No restart needed.", target.Id);
                        OnStatusChanged?.Invoke(target.Id, "Running");
                        continue;
                    }

                    if (!target.AllowMultipleInstances && ProcessMonitor.CountRunningInstances(target) > 0)
                    {
                        Log.Information("Target {Id} already has instances running. Skipping restart.", target.Id);
                        continue;
                    }

                    Log.Information("Restarting target {Id}.", target.Id);
                    ProcessMonitor.StartProcess(target);
                    _stateManager.RecordRestart(target.Id, target.RestartWindowSec, target.MaxRestartsInWindow, target.CooldownSec);
                    OnStatusChanged?.Invoke(target.Id, "Restarted");

                    await SafeDelay(target.StartGraceSec * 1000, ct);
                }
                else
                {
                    // Process is running — do health check if configured
                    await PerformHealthCheck(target, ct);
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Error in watch loop for {Id}.", target.Id);
                await SafeDelay(5000, ct);
            }
        }

        Log.Information("Watch loop ended for {Id}.", target.Id);
    }

    private async Task PerformHealthCheck(WatchTarget target, CancellationToken ct)
    {
        if (!_healthCheckers.TryGetValue(target.Id, out var checker) || checker == null)
            return;

        var hc = target.HealthCheck!;
        var healthy = await checker.CheckAsync(ct);

        if (healthy)
        {
            _stateManager.ResetHealthFailures(target.Id);
        }
        else
        {
            _stateManager.RecordHealthFailure(target.Id);
            var failures = _stateManager.GetHealthFailures(target.Id);
            Log.Warning("Health check failed for {Id} ({Desc}). Consecutive failures: {Count}.",
                target.Id, checker.Description, failures);

            if (failures >= hc.FailureCountForHang && hc.RestartOnHang)
            {
                Log.Warning("Target {Id} considered hung. Restarting.", target.Id);
                ProcessMonitor.StopProcess(target);
                await SafeDelay(2000, ct);
                ProcessMonitor.StartProcess(target);
                _stateManager.ResetHealthFailures(target.Id);
                _stateManager.RecordRestart(target.Id, target.RestartWindowSec, target.MaxRestartsInWindow, target.CooldownSec);
                OnStatusChanged?.Invoke(target.Id, "Restarted (hung)");
            }
        }
    }

    private static async Task SafeDelay(int ms, CancellationToken ct)
    {
        try { await Task.Delay(ms, ct); }
        catch (OperationCanceledException) { }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
    }
}

public class TargetStatusInfo
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public bool Enabled { get; set; }
    public bool IsRunning { get; set; }
    public bool IsPaused { get; set; }
    public bool IsInCooldown { get; set; }
    public int RestartCount { get; set; }
    public DateTime? LastRestartAt { get; set; }
    public DateTime? CooldownUntil { get; set; }

    public string StatusText
    {
        get
        {
            if (!Enabled) return "Disabled";
            if (IsPaused) return "Paused";
            if (IsInCooldown) return $"Cooldown until {CooldownUntil:HH:mm:ss}";
            return IsRunning ? "Running" : "Down";
        }
    }
}
