using AppWatchdog.Core.Services;
using Serilog;

namespace AppWatchdog.App;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _trayIcon;
    private readonly WatchdogEngine _engine;
    private readonly string _logDir;
    private readonly string _stateDir;
    private readonly System.Windows.Forms.Timer _refreshTimer;
    private SettingsForm? _settingsForm;

    public TrayApplicationContext(WatchdogEngine engine, string logDir, string stateDir)
    {
        _engine = engine;
        _logDir = logDir;
        _stateDir = stateDir;

        _trayIcon = new NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "AppWatchdog",
            Visible = true,
            ContextMenuStrip = BuildContextMenu()
        };

        _trayIcon.DoubleClick += (_, _) => ShowSettings();

        _engine.OnStatusChanged += (id, status) =>
        {
            try
            {
                _trayIcon.ShowBalloonTip(3000, "AppWatchdog", $"{id}: {status}", ToolTipIcon.Info);
            }
            catch { }
        };

        _engine.Start();

        _refreshTimer = new System.Windows.Forms.Timer { Interval = 5000 };
        _refreshTimer.Tick += (_, _) => UpdateTrayMenu();
        _refreshTimer.Start();
    }

    private ContextMenuStrip BuildContextMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings());
        menu.Items.Add(new ToolStripSeparator());

        // Target items will be added dynamically
        var targetsItem = new ToolStripMenuItem("Targets") { Name = "targets" };
        menu.Items.Add(targetsItem);

        menu.Items.Add(new ToolStripSeparator());

        var monitoringItem = new ToolStripMenuItem("Stop Monitoring", null, (_, _) => ToggleMonitoring());
        monitoringItem.Name = "monitoring";
        menu.Items.Add(monitoringItem);

        menu.Items.Add("Open Log Folder", null, (_, _) => OpenLogFolder());
        menu.Items.Add("Install Task Scheduler", null, (_, _) => InstallTask());
        menu.Items.Add("Uninstall Task Scheduler", null, (_, _) => UninstallTask());

        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        return menu;
    }

    private void UpdateTrayMenu()
    {
        try
        {
            var menu = _trayIcon.ContextMenuStrip;
            if (menu == null) return;

            var targetsItem = menu.Items.Find("targets", false).FirstOrDefault() as ToolStripMenuItem;
            if (targetsItem == null) return;

            targetsItem.DropDownItems.Clear();
            var statuses = _engine.GetTargetStatuses();

            foreach (var s in statuses)
            {
                var targetMenu = new ToolStripMenuItem($"{s.DisplayName} [{s.StatusText}]");

                targetMenu.DropDownItems.Add("Pause", null, (_, _) => _engine.PauseTarget(s.Id));
                targetMenu.DropDownItems.Add("Resume", null, (_, _) => _engine.ResumeTarget(s.Id));
                targetMenu.DropDownItems.Add("Stop Process", null, (_, _) => _engine.StopTarget(s.Id));
                targetMenu.DropDownItems.Add("Start Process", null, (_, _) => _engine.StartTarget(s.Id));

                targetsItem.DropDownItems.Add(targetMenu);
            }

            // Update monitoring toggle
            var monItem = menu.Items.Find("monitoring", false).FirstOrDefault() as ToolStripMenuItem;
            if (monItem != null)
            {
                monItem.Text = _engine.IsRunning ? "Stop Monitoring" : "Start Monitoring";
            }
        }
        catch (Exception ex)
        {
            Log.Debug(ex, "Error updating tray menu.");
        }
    }

    private void ToggleMonitoring()
    {
        if (_engine.IsRunning)
            _engine.Stop();
        else
            _engine.Start();
    }

    private void ShowSettings()
    {
        if (_settingsForm != null && !_settingsForm.IsDisposed)
        {
            _settingsForm.BringToFront();
            return;
        }

        _settingsForm = new SettingsForm(_engine, _logDir, _stateDir);
        _settingsForm.Show();
    }

    private void OpenLogFolder()
    {
        try
        {
            Directory.CreateDirectory(_logDir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = _logDir,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open log folder.");
        }
    }

    private void InstallTask()
    {
        var exePath = Environment.ProcessPath ?? "";
        var ok = TaskSchedulerManager.InstallTask(exePath);
        MessageBox.Show(ok ? "Task installed successfully." : "Failed to install task.\nTry running as Administrator.",
            "AppWatchdog", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void UninstallTask()
    {
        var ok = TaskSchedulerManager.UninstallTask();
        MessageBox.Show(ok ? "Task uninstalled." : "Failed to uninstall task.",
            "AppWatchdog", MessageBoxButtons.OK, ok ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
    }

    private void ExitApp()
    {
        _engine.Stop();
        _refreshTimer.Stop();
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        Application.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _engine.Dispose();
            _refreshTimer.Dispose();
            _trayIcon.Dispose();
        }
        base.Dispose(disposing);
    }
}
