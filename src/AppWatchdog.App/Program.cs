using System.Diagnostics;
using AppWatchdog.Core.Services;
using Serilog;

namespace AppWatchdog.App;

static class Program
{
    private static Mutex? _mutex;
    private const string MutexName = "Global\\AppWatchdog_SingleInstance";

    [STAThread]
    static void Main(string[] args)
    {
        _mutex = new Mutex(true, MutexName, out bool createdNew);
        if (!createdNew)
        {
            MessageBox.Show("AppWatchdog is already running.", "AppWatchdog",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        var baseDir = AppContext.BaseDirectory;
        var configPath = Path.Combine(baseDir, "config", "appsettings.json");

        // Parse --config
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--config" && i + 1 < args.Length)
            {
                configPath = Path.GetFullPath(args[i + 1]);
                break;
            }
        }

        // Check --headless
        if (args.Any(a => a.Equals("--headless", StringComparison.OrdinalIgnoreCase)))
        {
            RunHeadless(configPath);
            return;
        }

        var configManager = new ConfigManager(configPath);
        var settings = configManager.Load();

        var logDir = Path.IsPathRooted(settings.LogDir)
            ? settings.LogDir
            : Path.Combine(baseDir, settings.LogDir);
        var stateDir = Path.IsPathRooted(settings.StateDir)
            ? settings.StateDir
            : Path.Combine(baseDir, settings.StateDir);

        LoggingSetup.Initialize(logDir);
        Log.Information("AppWatchdog starting (GUI mode).");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            Log.Fatal(e.ExceptionObject as Exception, "Unhandled exception.");
            Log.CloseAndFlush();
        };

        Application.ThreadException += (_, e) =>
        {
            Log.Error(e.Exception, "UI thread exception.");
        };

        try
        {
            var stateManager = new StateManager(stateDir);
            var engine = new WatchdogEngine(configManager, stateManager);

            Application.Run(new TrayApplicationContext(engine, logDir, stateDir));
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error in application.");
            MessageBox.Show($"Fatal error: {ex.Message}", "AppWatchdog", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            Log.CloseAndFlush();
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
        }
    }

    static void RunHeadless(string configPath)
    {
        var configManager = new ConfigManager(configPath);
        var settings = configManager.Load();
        var baseDir = AppContext.BaseDirectory;

        var logDir = Path.IsPathRooted(settings.LogDir) ? settings.LogDir : Path.Combine(baseDir, settings.LogDir);
        var stateDir = Path.IsPathRooted(settings.StateDir) ? settings.StateDir : Path.Combine(baseDir, settings.StateDir);

        LoggingSetup.Initialize(logDir);
        Log.Information("AppWatchdog starting (headless mode).");

        var stateManager = new StateManager(stateDir);
        using var engine = new WatchdogEngine(configManager, stateManager);
        engine.Start();

        var exitEvent = new ManualResetEventSlim(false);
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; exitEvent.Set(); };
        exitEvent.Wait();
        engine.Stop();
        Log.Information("Headless mode stopped.");
        Log.CloseAndFlush();
    }
}
