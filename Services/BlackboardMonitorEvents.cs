using ClassIsland.BlackboardMonitorPlugin.Models;

namespace ClassIsland.BlackboardMonitorPlugin.Services;

/// <summary>
/// 黑板状态发生变化时的事件参数。
/// </summary>
public sealed class BlackboardStateChangedEventArgs(BlackboardState previous, BlackboardState current, bool isInitial)
    : EventArgs
{
    /// <summary>变化前的状态。</summary>
    public BlackboardState Previous { get; } = previous;

    /// <summary>变化后的状态。</summary>
    public BlackboardState Current { get; } = current;

    /// <summary>是否是插件启动后第一次得到有效状态。</summary>
    public bool IsInitial { get; } = isInitial;
}

/// <summary>
/// 实时预览画面可用时的事件参数。
/// </summary>
public sealed class BlackboardPreviewEventArgs(byte[] jpeg, BlackboardDetectionResult result) : EventArgs
{
    /// <summary>带检测标注的 JPEG 预览图。</summary>
    public byte[] Jpeg { get; } = jpeg;

    /// <summary>这一帧的检测结果。</summary>
    public BlackboardDetectionResult Result { get; } = result;
}
