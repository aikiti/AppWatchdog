using System.Text.Json;
using AppWatchdog.Core.Models;
using AppWatchdog.Core.Services;
using Serilog;

namespace AppWatchdog.App;

public class SettingsForm : Form
{
    private readonly WatchdogEngine _engine;
    private readonly string _logDir;
    private readonly string _stateDir;
    private readonly ListBox _targetList;
    private readonly PropertyGrid _propertyGrid;
    private readonly TextBox _statusBox;
    private readonly System.Windows.Forms.Timer _statusTimer;

    public SettingsForm(WatchdogEngine engine, string logDir, string stateDir)
    {
        _engine = engine;
        _logDir = logDir;
        _stateDir = stateDir;

        Text = "AppWatchdog - Settings";
        Size = new Size(900, 650);
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(700, 500);

        var splitContainer = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 250
        };

        // Left panel: target list
        var leftPanel = new Panel { Dock = DockStyle.Fill };

        var listLabel = new Label
        {
            Text = "Targets (processName mode recommended for launcher apps)",
            Dock = DockStyle.Top,
            Height = 40,
            Padding = new Padding(4)
        };

        _targetList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false
        };
        _targetList.SelectedIndexChanged += TargetList_SelectedIndexChanged;

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(2)
        };

        var addBtn = new Button { Text = "Add", Width = 60 };
        addBtn.Click += AddTarget_Click;
        var removeBtn = new Button { Text = "Remove", Width = 70 };
        removeBtn.Click += RemoveTarget_Click;
        var duplicateBtn = new Button { Text = "Duplicate", Width = 75 };
        duplicateBtn.Click += DuplicateTarget_Click;

        buttonPanel.Controls.AddRange(new Control[] { addBtn, removeBtn, duplicateBtn });

        leftPanel.Controls.Add(_targetList);
        leftPanel.Controls.Add(buttonPanel);
        leftPanel.Controls.Add(listLabel);

        // Right panel: property grid + status
        var rightPanel = new Panel { Dock = DockStyle.Fill };

        _propertyGrid = new PropertyGrid
        {
            Dock = DockStyle.Fill,
            PropertySort = PropertySort.Categorized
        };

        var statusLabel = new Label { Text = "Status:", Dock = DockStyle.Bottom, Height = 20 };
        _statusBox = new TextBox
        {
            Dock = DockStyle.Bottom,
            Multiline = true,
            ReadOnly = true,
            Height = 120,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9)
        };

        rightPanel.Controls.Add(_propertyGrid);
        rightPanel.Controls.Add(statusLabel);
        rightPanel.Controls.Add(_statusBox);

        splitContainer.Panel1.Controls.Add(leftPanel);
        splitContainer.Panel2.Controls.Add(rightPanel);

        // Bottom bar
        var bottomBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 45,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(4)
        };

        var saveBtn = new Button { Text = "Save && Apply", Width = 110, Height = 30 };
        saveBtn.Click += Save_Click;
        var reloadBtn = new Button { Text = "Reload", Width = 80, Height = 30 };
        reloadBtn.Click += Reload_Click;
        var exportBtn = new Button { Text = "Export Zip", Width = 90, Height = 30 };
        exportBtn.Click += Export_Click;
        var importBtn = new Button { Text = "Import Zip", Width = 90, Height = 30 };
        importBtn.Click += Import_Click;
        var logBtn = new Button { Text = "Open Logs", Width = 80, Height = 30 };
        logBtn.Click += (_, _) => OpenLogFolder();

        bottomBar.Controls.AddRange(new Control[] { saveBtn, reloadBtn, exportBtn, importBtn, logBtn });

        Controls.Add(splitContainer);
        Controls.Add(bottomBar);

        LoadTargets();

        _statusTimer = new System.Windows.Forms.Timer { Interval = 3000 };
        _statusTimer.Tick += (_, _) => RefreshStatus();
        _statusTimer.Start();
        RefreshStatus();
    }

    private void LoadTargets()
    {
        _targetList.Items.Clear();
        foreach (var t in _engine.Settings.Targets)
        {
            _targetList.Items.Add(new TargetListItem(t));
        }
        if (_targetList.Items.Count > 0)
            _targetList.SelectedIndex = 0;
    }

    private void TargetList_SelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_targetList.SelectedItem is TargetListItem item)
        {
            _propertyGrid.SelectedObject = item.Target;
        }
    }

    private void AddTarget_Click(object? sender, EventArgs e)
    {
        var target = new WatchTarget
        {
            Id = $"target-{_engine.Settings.Targets.Count + 1}",
            DisplayName = "New Target",
            ExePath = @"C:\path\to\app.exe",
            DetectMode = "processName"
        };
        _engine.Settings.Targets.Add(target);
        _targetList.Items.Add(new TargetListItem(target));
        _targetList.SelectedIndex = _targetList.Items.Count - 1;
    }

    private void RemoveTarget_Click(object? sender, EventArgs e)
    {
        if (_targetList.SelectedItem is TargetListItem item)
        {
            _engine.Settings.Targets.Remove(item.Target);
            _targetList.Items.Remove(item);
        }
    }

    private void DuplicateTarget_Click(object? sender, EventArgs e)
    {
        if (_targetList.SelectedItem is TargetListItem item)
        {
            var clone = item.Target.Clone();
            clone.Id = item.Target.Id + "-copy";
            clone.DisplayName = item.Target.DisplayName + " (Copy)";
            _engine.Settings.Targets.Add(clone);
            _targetList.Items.Add(new TargetListItem(clone));
            _targetList.SelectedIndex = _targetList.Items.Count - 1;
        }
    }

    private void Save_Click(object? sender, EventArgs e)
    {
        try
        {
            _engine.ConfigManager.Save(_engine.Settings);
            _engine.Restart();
            RefreshStatus();
            MessageBox.Show("Settings saved and applied.", "AppWatchdog",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save: {ex.Message}", "Error",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void Reload_Click(object? sender, EventArgs e)
    {
        _engine.Restart();
        LoadTargets();
        RefreshStatus();
    }

    private void Export_Click(object? sender, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "ZIP files|*.zip",
            FileName = "appwatchdog-export.zip"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var ok = ExportImportService.ExportZip(dlg.FileName, _engine.ConfigManager.ConfigPath, _stateDir);
            MessageBox.Show(ok ? "Exported." : "Export failed.", "AppWatchdog");
        }
    }

    private void Import_Click(object? sender, EventArgs e)
    {
        using var dlg = new OpenFileDialog { Filter = "ZIP files|*.zip" };
        if (dlg.ShowDialog() == DialogResult.OK)
        {
            var ok = ExportImportService.ImportZip(dlg.FileName, _engine.ConfigManager.ConfigPath, _stateDir);
            if (ok)
            {
                _engine.Restart();
                LoadTargets();
                RefreshStatus();
            }
            MessageBox.Show(ok ? "Imported and applied." : "Import failed.", "AppWatchdog");
        }
    }

    private void RefreshStatus()
    {
        try
        {
            var statuses = _engine.GetTargetStatuses();
            var lines = new List<string>
            {
                $"Engine: {(_engine.IsRunning ? "Running" : "Stopped")}",
                $"Targets: {statuses.Count}",
                ""
            };
            foreach (var s in statuses)
            {
                lines.Add($"  {s.DisplayName,-20} {s.StatusText,-15} Restarts={s.RestartCount}");
            }
            _statusBox.Text = string.Join(Environment.NewLine, lines);
        }
        catch { }
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
        catch { }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        _statusTimer.Stop();
        _statusTimer.Dispose();
        base.OnFormClosing(e);
    }

    private class TargetListItem
    {
        public WatchTarget Target { get; }
        public TargetListItem(WatchTarget target) => Target = target;
        public override string ToString() => $"{Target.DisplayName} ({Target.Id}) [{(Target.Enabled ? "ON" : "OFF")}]";
    }
}
