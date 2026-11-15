using ClassIsland.BlackboardMonitorPlugin.Models;
using ClassIsland.BlackboardMonitorPlugin.Services;
using ClassIsland.Core.Abstractions.Automation;
using ClassIsland.Core.Attributes;

namespace ClassIsland.BlackboardMonitorPlugin.Automation;

/// <summary>
/// 当黑板开合状态变为指定状态时触发的自动化触发器。
/// </summary>
[TriggerInfo("classisland.blackboard-monitor.state", "黑板开合状态变化时", "\ue1c1")]
public class BlackboardStateTrigger(BlackboardMonitorService monitor) : TriggerBase<BlackboardStateTriggerSettings>
{
    private IDisposable? _consumer;

    public override void Loaded()
    {
        monitor.StateChanged += MonitorOnStateChanged;
        _consumer = monitor.AcquireConsumer("自动化触发器");
    }

    public override void UnLoaded()
    {
        monitor.StateChanged -= MonitorOnStateChanged;
        _consumer?.Dispose();
        _consumer = null;
    }

    private void MonitorOnStateChanged(object? sender, BlackboardStateChangedEventArgs e)
    {
        var settings = Settings;
        var target = settings.TargetState;

        if (e.IsInitial)
        {
            // 初始化时“之前的状态”没有意义，只判断当前状态是否命中。
            if (e.Current == target && settings.TriggerOnInitialState)
            {
                Trigger();
            }

            return;
        }

        if (e.Current == target)
        {
            Trigger();
        }
        else if (e.Previous == target && settings.TriggerOnLeave)
        {
            TriggerRevert();
        }
    }
}
