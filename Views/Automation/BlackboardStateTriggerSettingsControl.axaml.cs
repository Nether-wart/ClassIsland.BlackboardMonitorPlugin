using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ClassIsland.BlackboardMonitorPlugin.Automation;
using ClassIsland.BlackboardMonitorPlugin.Models;
using ClassIsland.BlackboardMonitorPlugin.Services;
using ClassIsland.Core.Abstractions.Controls;

namespace ClassIsland.BlackboardMonitorPlugin.Views.Automation;

/// <summary>
/// “黑板开合状态变化时”触发器的设置控件。
/// </summary>
/// <remarks>
/// 设置对象由宿主在控件构造之后注入（<see cref="TriggerSettingsControlBase.SettingsInternal"/>），
/// 因此下拉框的初值只能在控件挂到可视化树之后再同步。
/// </remarks>
public partial class BlackboardStateTriggerSettingsControl : TriggerSettingsControlBase<BlackboardStateTriggerSettings>
{
    private readonly BlackboardMonitorService _monitor;
    private bool _isSyncing;

    public BlackboardStateTriggerSettingsControl(BlackboardMonitorService monitor)
    {
        _monitor = monitor;
        InitializeComponent();

        TargetStateComboBox.SelectionChanged += TargetStateComboBoxOnSelectionChanged;
        TriggerOnLeaveCheckBox.IsCheckedChanged += TriggerOnLeaveCheckBoxOnIsCheckedChanged;
        TriggerOnInitialCheckBox.IsCheckedChanged += TriggerOnInitialCheckBoxOnIsCheckedChanged;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        _isSyncing = true;
        TargetStateComboBox.SelectedIndex = (int)Settings.TargetState;
        TriggerOnLeaveCheckBox.IsChecked = Settings.TriggerOnLeave;
        TriggerOnInitialCheckBox.IsChecked = Settings.TriggerOnInitialState;
        _isSyncing = false;

        _monitor.StateChanged += MonitorOnStateChanged;
        _monitor.StatusChanged += MonitorOnStatusChanged;
        UpdateStatusText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _monitor.StateChanged -= MonitorOnStateChanged;
        _monitor.StatusChanged -= MonitorOnStatusChanged;
        base.OnDetachedFromVisualTree(e);
    }

    private void TargetStateComboBoxOnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        var index = TargetStateComboBox.SelectedIndex;
        if (index < 0 || index > (int)BlackboardState.BothClosed)
        {
            return;
        }

        Settings.TargetState = (BlackboardState)index;
    }

    private void TriggerOnLeaveCheckBoxOnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        Settings.TriggerOnLeave = TriggerOnLeaveCheckBox.IsChecked == true;
    }

    private void TriggerOnInitialCheckBoxOnIsCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (_isSyncing)
        {
            return;
        }

        Settings.TriggerOnInitialState = TriggerOnInitialCheckBox.IsChecked == true;
    }

    private void MonitorOnStateChanged(object? sender, BlackboardStateChangedEventArgs e) => UpdateStatusText();

    private void MonitorOnStatusChanged(object? sender, EventArgs e) => UpdateStatusText();

    private void UpdateStatusText()
    {
        var current = _monitor.HasState
            ? BlackboardStateNames.GetName(_monitor.CurrentState)
            : "未知（尚未得到有效画面）";
        var status = _monitor.Status;
        StatusTextBlock.Text = string.IsNullOrEmpty(status)
            ? $"当前状态：{current}"
            : $"当前状态：{current}\n{status}";
    }
}
