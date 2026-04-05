using System.Diagnostics;
using AppWatchdog.Core.Models;
using Serilog;

namespace AppWatchdog.Core.Services;

public static class ProcessMonitor
{
    public static bool IsRunning(WatchTarget target)
    {
        try
        {
            if (target.DetectMode == "exePath")
                return IsRunningByExePath(target);
            else
                return IsRunningByProcessName(target);
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error checking if {Id} is running.", target.Id);
            return false;
        }
    }

    private static bool IsRunningByProcessName(WatchTarget target)
    {
        var name = target.GetEffectiveProcessName();
        var processes = Process.GetProcessesByName(name);
        return processes.Length > 0;
    }

    private static bool IsRunningByExePath(WatchTarget target)
    {
        var normalizedExe = Path.GetFullPath(target.ExePath).ToLowerInvariant();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var mainModule = proc.MainModule;
                if (mainModule != null)
                {
                    var procPath = Path.GetFullPath(mainModule.FileName).ToLowerInvariant();
                    if (procPath == normalizedExe)
                        return true;
                }
            }
            catch
            {
                // Access denied or 32/64-bit mismatch — skip
            }
            finally
            {
                proc.Dispose();
            }
        }
        return false;
    }

    public static int CountRunningInstances(WatchTarget target)
    {
        try
        {
            if (target.DetectMode == "exePath")
                return CountByExePath(target);
            var name = target.GetEffectiveProcessName();
            return Process.GetProcessesByName(name).Length;
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Error counting instances of {Id}.", target.Id);
            return 0;
        }
    }

    private static int CountByExePath(WatchTarget target)
    {
        int count = 0;
        var normalizedExe = Path.GetFullPath(target.ExePath).ToLowerInvariant();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var mainModule = proc.MainModule;
                if (mainModule != null)
                {
                    var procPath = Path.GetFullPath(mainModule.FileName).ToLowerInvariant();
                    if (procPath == normalizedExe)
                        count++;
                }
            }
            catch { }
            finally { proc.Dispose(); }
        }
        return count;
    }

    public static Process? StartProcess(WatchTarget target)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = target.ExePath,
                WorkingDirectory = target.GetEffectiveWorkDir(),
                UseShellExecute = true
            };

            if (!string.IsNullOrWhiteSpace(target.Args))
            {
                psi.Arguments = target.Args;
            }

            Log.Information("Starting process for {Id}: {Exe} {Args}",
                target.Id, target.ExePath, target.Args ?? "(no args)");

            return Process.Start(psi);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to start process for {Id}.", target.Id);
            return null;
        }
    }

    public static void StopProcess(WatchTarget target)
    {
        if (target.StopMethod == "none") return;

        try
        {
            Process[] processes;
            if (target.DetectMode == "exePath")
            {
                processes = GetProcessesByExePath(target);
            }
            else
            {
                var name = target.GetEffectiveProcessName();
                processes = Process.GetProcessesByName(name);
            }

            foreach (var proc in processes)
            {
                try
                {
                    if (target.StopMethod == "closeWindow")
                    {
                        Log.Information("Closing main window for {Id} (PID {Pid}).", target.Id, proc.Id);
                        proc.CloseMainWindow();
                    }
                    else if (target.StopMethod == "kill")
                    {
                        Log.Information("Killing process for {Id} (PID {Pid}).", target.Id, proc.Id);
                        proc.Kill();
                    }
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "Failed to stop process PID {Pid} for {Id}.", proc.Id, target.Id);
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error stopping process for {Id}.", target.Id);
        }
    }

    private static Process[] GetProcessesByExePath(WatchTarget target)
    {
        var result = new List<Process>();
        var normalizedExe = Path.GetFullPath(target.ExePath).ToLowerInvariant();
        foreach (var proc in Process.GetProcesses())
        {
            try
            {
                var mainModule = proc.MainModule;
                if (mainModule != null)
                {
                    var procPath = Path.GetFullPath(mainModule.FileName).ToLowerInvariant();
                    if (procPath == normalizedExe)
                    {
                        result.Add(proc);
                        continue;
                    }
                }
            }
            catch { }
            proc.Dispose();
        }
        return result.ToArray();
    }
}
