using AppWatchdog.Core.Services;
using Serilog;

namespace AppWatchdog.Cli;

class Program
{
    static int Main(string[] args)
    {
        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "config", "appsettings.json");
        string? customConfig = null;

        // Parse --config
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                customConfig = args[i + 1];
                break;
            }
        }

        if (customConfig != null)
            configPath = Path.GetFullPath(customConfig);

        var configManager = new ConfigManager(configPath);
        var settings = configManager.Load();

        var logDir = Path.IsPathRooted(settings.LogDir)
            ? settings.LogDir
            : Path.Combine(baseDir, settings.LogDir);
        var stateDir = Path.IsPathRooted(settings.StateDir)
            ? settings.StateDir
            : Path.Combine(baseDir, settings.StateDir);

        LoggingSetup.Initialize(logDir);

        var stateManager = new StateManager(stateDir);

        if (args.Length == 0)
        {
            PrintUsage();
            return 0;
        }

        var command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "";

        try
        {
            return command.ToLowerInvariant() switch
            {
                "run" or "--headless" => RunHeadless(configManager, stateManager),
                "pause" => PauseTarget(stateManager, args),
                "resume" => ResumeTarget(stateManager, args),
                "stop" => StopTarget(configManager, stateManager, args),
                "start" => StartTarget(configManager, stateManager, args),
                "status" => PrintStatus(configManager, stateManager),
                "print-config" => PrintConfig(configManager),
                "install-task" => InstallTask(),
                "uninstall-task" => UninstallTask(),
                "status-task" => StatusTask(),
                "export" => Export(configManager, stateDir, args),
                "import" => Import(configPath, stateDir, args),
                "help" or "--help" or "-h" => PrintUsageReturn(),
                _ => UnknownCommand(command)
            };
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CLI error.");
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    static int RunHeadless(ConfigManager configManager, StateManager stateManager)
    {
        Console.WriteLine("AppWatchdog running in headless mode. Press Ctrl+C to stop.");
        Log.Information("Headless mode started.");

        using var engine = new WatchdogEngine(configManager, stateManager);
        engine.OnStatusChanged += (id, status) =>
            Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] {id}: {status}");

        engine.Start();

        var exitEvent = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            exitEvent.Set();
        };

        exitEvent.Wait();
        engine.Stop();
        Log.Information("Headless mode stopped.");
        return 0;
    }

    static int PauseTarget(StateManager stateManager, string[] args)
    {
        var id = GetTargetIdArg(args);
        if (id == null) return 1;
        stateManager.SetPaused(id, true);
        Console.WriteLine($"Target '{id}' paused.");
        return 0;
    }

    static int ResumeTarget(StateManager stateManager, string[] args)
    {
        var id = GetTargetIdArg(args);
        if (id == null) return 1;
        stateManager.SetPaused(id, false);
        Console.WriteLine($"Target '{id}' resumed.");
        return 0;
    }

    static int StopTarget(ConfigManager configManager, StateManager stateManager, string[] args)
    {
        var id = GetTargetIdArg(args);
        if (id == null) return 1;
        var settings = configManager.Load();
        var target = settings.Targets.FirstOrDefault(t => t.Id == id);
        if (target == null)
        {
            Console.Error.WriteLine($"Target '{id}' not found.");
            return 1;
        }
        stateManager.RecordStopCommand(id);
        stateManager.SetPaused(id, true);
        ProcessMonitor.StopProcess(target);
        Console.WriteLine($"Target '{id}' stopped and paused.");
        return 0;
    }

    static int StartTarget(ConfigManager configManager, StateManager stateManager, string[] args)
    {
        var id = GetTargetIdArg(args);
        if (id == null) return 1;
        var settings = configManager.Load();
        var target = settings.Targets.FirstOrDefault(t => t.Id == id);
        if (target == null)
        {
            Console.Error.WriteLine($"Target '{id}' not found.");
            return 1;
        }
        stateManager.SetPaused(id, false);
        if (!ProcessMonitor.IsRunning(target))
            ProcessMonitor.StartProcess(target);
        Console.WriteLine($"Target '{id}' started and resumed.");
        return 0;
    }

    static int PrintStatus(ConfigManager configManager, StateManager stateManager)
    {
        using var engine = new WatchdogEngine(configManager, stateManager);
        var statuses = engine.GetTargetStatuses();

        Console.WriteLine($"{"ID",-20} {"Name",-25} {"Status",-15} {"Running",-8} {"Restarts",-10}");
        Console.WriteLine(new string('-', 80));

        foreach (var s in statuses)
        {
            Console.WriteLine($"{s.Id,-20} {s.DisplayName,-25} {s.StatusText,-15} {s.IsRunning,-8} {s.RestartCount,-10}");
        }
        return 0;
    }

    static int PrintConfig(ConfigManager configManager)
    {
        var settings = configManager.Load();
        Console.WriteLine(configManager.ExportToJson(settings));
        return 0;
    }

    static int InstallTask()
    {
        var exePath = Environment.ProcessPath ?? System.Reflection.Assembly.GetExecutingAssembly().Location;
        var ok = TaskSchedulerManager.InstallTask(exePath, "--headless run");
        Console.WriteLine(ok ? "Task installed." : "Failed to install task.");
        return ok ? 0 : 1;
    }

    static int UninstallTask()
    {
        var ok = TaskSchedulerManager.UninstallTask();
        Console.WriteLine(ok ? "Task uninstalled." : "Failed to uninstall task.");
        return ok ? 0 : 1;
    }

    static int StatusTask()
    {
        Console.WriteLine(TaskSchedulerManager.GetTaskStatus());
        return 0;
    }

    static int Export(ConfigManager configManager, string stateDir, string[] args)
    {
        var outPath = GetFileArg(args) ?? "appwatchdog-export.zip";
        var ok = ExportImportService.ExportZip(outPath, configManager.ConfigPath, stateDir);
        Console.WriteLine(ok ? $"Exported to {outPath}" : "Export failed.");
        return ok ? 0 : 1;
    }

    static int Import(string configPath, string stateDir, string[] args)
    {
        var inPath = GetFileArg(args);
        if (inPath == null)
        {
            Console.Error.WriteLine("Usage: import <zip-path>");
            return 1;
        }
        var ok = ExportImportService.ImportZip(inPath, configPath, stateDir);
        Console.WriteLine(ok ? "Imported." : "Import failed.");
        return ok ? 0 : 1;
    }

    static string? GetTargetIdArg(string[] args)
    {
        var filtered = args.Where(a => !a.StartsWith("--")).Skip(1).FirstOrDefault();
        if (filtered == null)
        {
            Console.Error.WriteLine("Usage: <command> <target-id>");
            return null;
        }
        return filtered;
    }

    static string? GetFileArg(string[] args)
    {
        return args.Where(a => !a.StartsWith("--")).Skip(1).FirstOrDefault();
    }

    static void PrintUsage()
    {
        Console.WriteLine(@"AppWatchdog CLI

Usage: AppWatchdog.Cli <command> [options]

Commands:
  run, --headless     Run watchdog in headless (no GUI) mode
  status              Show status of all targets
  pause <id>          Pause monitoring for a target (no auto-restart)
  resume <id>         Resume monitoring for a target
  stop <id>           Stop target process and pause monitoring
  start <id>          Start target process and resume monitoring
  print-config        Print current configuration as JSON
  install-task        Install Windows Task Scheduler entry for auto-start
  uninstall-task      Remove the scheduled task
  status-task         Show scheduled task status
  export [path]       Export config + state as zip
  import <path>       Import config + state from zip

Options:
  --config <path>     Use custom config file path
  --help, -h          Show this help");
    }

    static int PrintUsageReturn() { PrintUsage(); return 0; }
    static int UnknownCommand(string cmd) { Console.Error.WriteLine($"Unknown command: {cmd}"); PrintUsage(); return 1; }
}
