using System.ComponentModel;
using ClassIsland.BlackboardMonitorPlugin.Models;
using ClassIsland.Shared.Helpers;

namespace ClassIsland.BlackboardMonitorPlugin.Services;

/// <summary>
/// 负责读写插件设置文件。设置文件位于插件的配置目录中，与宿主配置分离。
/// </summary>
public sealed class BlackboardMonitorSettingsStore : IDisposable
{
    private readonly string _path;
    private readonly System.Timers.Timer _saveTimer;
    private readonly object _sync = new();

    public BlackboardMonitorSettingsStore(string pluginConfigFolder)
    {
        _path = Path.Combine(pluginConfigFolder, "Settings.json");
        Settings = ConfigureFileHelper.LoadConfig<BlackboardMonitorSettings>(_path);
        Settings.PropertyChanged += OnSettingsPropertyChanged;
        _saveTimer = new System.Timers.Timer(1000) { AutoReset = false };
        _saveTimer.Elapsed += (_, _) => Save();
    }

    public BlackboardMonitorSettings Settings { get; }

    public void ResetToDefaults()
    {
        var defaults = new BlackboardMonitorSettings();
        Settings.IsEnabled = defaults.IsEnabled;
        Settings.CameraIndex = defaults.CameraIndex;
        Settings.CaptureBackend = defaults.CaptureBackend;
        Settings.CaptureIntervalMs = defaults.CaptureIntervalMs;
        Settings.DebounceFrames = defaults.DebounceFrames;
        Settings.DarkThreshold = defaults.DarkThreshold;
        Settings.ColumnDarkRatio = defaults.ColumnDarkRatio;
        Settings.MinRunWidth = defaults.MinRunWidth;
        Settings.BothClosedRunWidth = defaults.BothClosedRunWidth;
        Settings.BothClosedCenterTolerance = defaults.BothClosedCenterTolerance;
        Settings.BothClosedCoverage = defaults.BothClosedCoverage;
        Settings.RoiX = defaults.RoiX;
        Settings.RoiY = defaults.RoiY;
        Settings.RoiWidth = defaults.RoiWidth;
        Settings.RoiHeight = defaults.RoiHeight;
    }

    public void Save()
    {
        lock (_sync)
        {
            ConfigureFileHelper.SaveConfig(_path, Settings);
        }
    }

    private void OnSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // 拖动滑块会产生密集的变更，合并成一次写入。
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    public void Dispose()
    {
        _saveTimer.Stop();
        _saveTimer.Dispose();
        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        Save();
    }
}
