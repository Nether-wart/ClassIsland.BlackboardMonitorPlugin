using ClassIsland.BlackboardMonitorPlugin.Models;

namespace ClassIsland.BlackboardMonitorPlugin.Automation;

/// <summary>
/// “黑板开合状态变化时”触发器的设置。
/// </summary>
public class BlackboardStateTriggerSettings
{
    /// <summary>目标状态。状态变为该值时触发工作流。</summary>
    public BlackboardState TargetState { get; set; } = BlackboardState.BothClosed;

    /// <summary>状态离开 <see cref="TargetState"/> 时是否触发工作流的恢复。</summary>
    public bool TriggerOnLeave { get; set; }

    /// <summary>插件刚启动、且当前状态已经等于 <see cref="TargetState"/> 时，是否也触发一次。</summary>
    public bool TriggerOnInitialState { get; set; } = true;
}
