using System.Text.Json;
using System.Text.Json.Serialization;
using AppWatchdog.Core.Models;
using Serilog;

namespace AppWatchdog.Core.Services;

public class StateManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly string _statePath;
    private WatchStateFile _state = new();
    private readonly object _lock = new();

    public StateManager(string stateDir)
    {
        Directory.CreateDirectory(stateDir);
        _statePath = Path.Combine(stateDir, "state.json");
        Load();
    }

    private void Load()
    {
        if (!File.Exists(_statePath))
        {
            _state = new WatchStateFile();
            return;
        }
        try
        {
            var json = File.ReadAllText(_statePath);
            _state = JsonSerializer.Deserialize<WatchStateFile>(json, JsonOptions) ?? new WatchStateFile();
            Log.Information("State loaded from {Path}.", _statePath);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load state. Starting fresh.");
            _state = new WatchStateFile();
        }
    }

    public void Save()
    {
        lock (_lock)
        {
            try
            {
                _state.LastSaved = DateTime.UtcNow;
                var json = JsonSerializer.Serialize(_state, JsonOptions);
                File.WriteAllText(_statePath, json);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to save state.");
            }
        }
    }

    public TargetState GetTargetState(string targetId)
    {
        lock (_lock)
        {
            if (!_state.Targets.ContainsKey(targetId))
            {
                _state.Targets[targetId] = new TargetState { TargetId = targetId };
            }
            return _state.Targets[targetId];
        }
    }

    public void SetPaused(string targetId, bool paused)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            state.Paused = paused;
            state.PausedAt = paused ? DateTime.UtcNow : null;
            if (paused)
                state.LastStopCommandAt = DateTime.UtcNow;
            Save();
            Log.Information("Target {Id} paused={Paused}.", targetId, paused);
        }
    }

    public bool IsPaused(string targetId)
    {
        lock (_lock)
        {
            return GetTargetState(targetId).Paused;
        }
    }

    public void RecordRestart(string targetId, int windowSec, int maxRestarts, int cooldownSec)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            var now = DateTime.UtcNow;

            if (state.WindowStartTime == null || (now - state.WindowStartTime.Value).TotalSeconds > windowSec)
            {
                state.WindowStartTime = now;
                state.RestartCountInWindow = 0;
            }

            state.RestartCountInWindow++;
            state.LastRestartAt = now;

            if (state.RestartCountInWindow >= maxRestarts)
            {
                state.CooldownUntil = now.AddSeconds(cooldownSec);
                Log.Warning("Target {Id} entered cooldown until {Until}.", targetId, state.CooldownUntil);
            }

            Save();
        }
    }

    public bool IsInCooldown(string targetId)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            if (state.CooldownUntil == null) return false;
            if (DateTime.UtcNow >= state.CooldownUntil.Value)
            {
                state.CooldownUntil = null;
                state.RestartCountInWindow = 0;
                state.WindowStartTime = null;
                Save();
                return false;
            }
            return true;
        }
    }

    public void RecordHealthFailure(string targetId)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            state.ConsecutiveHealthFailures++;
            Save();
        }
    }

    public void ResetHealthFailures(string targetId)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            state.ConsecutiveHealthFailures = 0;
            Save();
        }
    }

    public int GetHealthFailures(string targetId)
    {
        lock (_lock)
        {
            return GetTargetState(targetId).ConsecutiveHealthFailures;
        }
    }

    public void RecordStopCommand(string targetId)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            state.LastStopCommandAt = DateTime.UtcNow;
            Save();
        }
    }

    public bool WasRecentlyManualStopped(string targetId, int windowSec)
    {
        lock (_lock)
        {
            var state = GetTargetState(targetId);
            if (state.LastStopCommandAt == null) return false;
            return (DateTime.UtcNow - state.LastStopCommandAt.Value).TotalSeconds <= windowSec;
        }
    }

    public Dictionary<string, TargetState> GetAllStates()
    {
        lock (_lock)
        {
            return new Dictionary<string, TargetState>(_state.Targets);
        }
    }

    public string ExportJson()
    {
        lock (_lock)
        {
            return JsonSerializer.Serialize(_state, JsonOptions);
        }
    }
}
