using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using ClassIsland.BlackboardMonitorPlugin.Models;
using ClassIsland.BlackboardMonitorPlugin.Services;
using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;

namespace ClassIsland.BlackboardMonitorPlugin.Views.SettingsPages;

[SettingsPageInfo("classisland.blackboard-monitor.settings", "黑板开合监视", "\ue1c1", "\ue1c0")]
public partial class BlackboardMonitorSettingsPage : SettingsPageBase
{
    private readonly BlackboardMonitorService _monitor;
    private readonly BlackboardMonitorSettingsStore _store;
    private IDisposable? _consumer;

    public BlackboardMonitorSettingsPage(BlackboardMonitorService monitor, BlackboardMonitorSettingsStore store)
    {
        _monitor = monitor;
        _store = store;
        InitializeComponent();

        DataContext = store.Settings;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        RestoreDefaultsButton.Click += RestoreDefaultsButtonOnClick;
        ReconnectButton.Click += ReconnectButtonOnClick;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_consumer != null)
        {
            return;
        }

        _monitor.PreviewUpdated += MonitorOnPreviewUpdated;
        _monitor.StatusChanged += MonitorOnStatusChanged;
        _consumer = _monitor.AcquireConsumer("设置页实时预览", withPreview: true);
        UpdateStatusText();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_consumer == null)
        {
            return;
        }

        _monitor.PreviewUpdated -= MonitorOnPreviewUpdated;
        _monitor.StatusChanged -= MonitorOnStatusChanged;
        _consumer.Dispose();
        _consumer = null;
        _store.Save();
    }

    private void MonitorOnPreviewUpdated(object? sender, BlackboardPreviewEventArgs e)
    {
        try
        {
            using var stream = new MemoryStream(e.Jpeg);
            PreviewImage.Source = new Bitmap(stream);
        }
        catch
        {
            // 解码失败时保留上一帧即可。
            return;
        }

        StateTextBlock.Text =
            $"当前状态：{BlackboardStateNames.GetName(_monitor.CurrentState)}（本帧判定：{BlackboardStateNames.GetName(e.Result.State)}）";
        MetricsTextBlock.Text =
            $"遮挡列占比 {e.Result.Coverage:P0}｜最长遮挡宽度 {e.Result.RunWidth:P0}｜单列最高黑占比 {e.Result.MaxColumnDarkRatio:P0}";
        UpdateStatusText();
    }

    private void MonitorOnStatusChanged(object? sender, EventArgs e) => UpdateStatusText();

    private void UpdateStatusText()
    {
        var status = _monitor.Status;
        ErrorTextBlock.Text = status ?? "";
        ErrorTextBlock.IsVisible = !string.IsNullOrEmpty(status);
    }

    private void RestoreDefaultsButtonOnClick(object? sender, RoutedEventArgs e) => _store.ResetToDefaults();

    private void ReconnectButtonOnClick(object? sender, RoutedEventArgs e) => _monitor.RequestReconnect();
}
