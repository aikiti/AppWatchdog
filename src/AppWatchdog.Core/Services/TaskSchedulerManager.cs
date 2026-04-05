using System.Diagnostics;
using Serilog;

namespace AppWatchdog.Core.Services;

public static class TaskSchedulerManager
{
    private const string TaskName = "AppWatchdog";

    public static bool InstallTask(string exePath, string? args = null)
    {
        try
        {
            var arguments = args ?? "";
            var fullArgs = string.IsNullOrWhiteSpace(arguments) ? "" : $" {arguments}";
            var xml = $@"<?xml version=""1.0"" encoding=""UTF-16""?>
<Task version=""1.2"" xmlns=""http://schemas.microsoft.com/windows/2004/02/mit/task"">
  <RegistrationInfo>
    <Description>AppWatchdog - Process Monitor and Auto-Restarter</Description>
  </RegistrationInfo>
  <Triggers>
    <LogonTrigger>
      <Enabled>true</Enabled>
    </LogonTrigger>
  </Triggers>
  <Principals>
    <Principal id=""Author"">
      <LogonType>InteractiveToken</LogonType>
      <RunLevel>HighestAvailable</RunLevel>
    </Principal>
  </Principals>
  <Settings>
    <MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy>
    <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
    <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
    <AllowHardTerminate>true</AllowHardTerminate>
    <StartWhenAvailable>true</StartWhenAvailable>
    <RunOnlyIfNetworkAvailable>false</RunOnlyIfNetworkAvailable>
    <AllowStartOnDemand>true</AllowStartOnDemand>
    <Enabled>true</Enabled>
    <Hidden>false</Hidden>
    <RunOnlyIfIdle>false</RunOnlyIfIdle>
    <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
    <Priority>7</Priority>
  </Settings>
  <Actions Context=""Author"">
    <Exec>
      <Command>{EscapeXml(exePath)}</Command>
      <Arguments>{EscapeXml(fullArgs.Trim())}</Arguments>
      <WorkingDirectory>{EscapeXml(Path.GetDirectoryName(exePath) ?? ".")}</WorkingDirectory>
    </Exec>
  </Actions>
</Task>";

            var tempFile = Path.GetTempFileName();
            File.WriteAllText(tempFile, xml, System.Text.Encoding.Unicode);

            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Create /TN \"{TaskName}\" /XML \"{tempFile}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(15000);
            var output = proc.StandardOutput.ReadToEnd();
            var error = proc.StandardError.ReadToEnd();

            try { File.Delete(tempFile); } catch { }

            if (proc.ExitCode == 0)
            {
                Log.Information("Task '{Name}' installed successfully.", TaskName);
                return true;
            }

            Log.Error("Failed to install task: {Error} {Output}", error, output);
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install scheduled task.");
            return false;
        }
    }

    public static bool UninstallTask()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Delete /TN \"{TaskName}\" /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return false;
            proc.WaitForExit(15000);

            if (proc.ExitCode == 0)
            {
                Log.Information("Task '{Name}' uninstalled.", TaskName);
                return true;
            }

            Log.Warning("Failed to uninstall task (may not exist).");
            return false;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to uninstall scheduled task.");
            return false;
        }
    }

    public static string GetTaskStatus()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "schtasks.exe",
                Arguments = $"/Query /TN \"{TaskName}\" /FO LIST",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var proc = Process.Start(psi);
            if (proc == null) return "Error: could not query task scheduler.";
            proc.WaitForExit(15000);

            if (proc.ExitCode == 0)
                return proc.StandardOutput.ReadToEnd();

            return "Task not found (not installed).";
        }
        catch (Exception ex)
        {
            return $"Error: {ex.Message}";
        }
    }

    private static string EscapeXml(string s)
    {
        return s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");
    }
}
