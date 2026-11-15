namespace ClassIsland.BlackboardMonitorPlugin.Models;

/// <summary>
/// 黑板开合状态。这里的“左/右”指摄像头画面中的左/右半边。
/// </summary>
/// <remarks>
/// 移动黑板进入摄像头画面即视为该侧被合上，因此“左合上”表示画面左侧被黑板遮挡。
/// </remarks>
public enum BlackboardState
{
    /// <summary>两侧都打开，没有任何半边被遮挡。</summary>
    BothOpen = 0,

    /// <summary>画面左半边被黑板遮挡，右半边打开。</summary>
    LeftClosed = 1,

    /// <summary>画面右半边被黑板遮挡，左半边打开。</summary>
    RightClosed = 2,

    /// <summary>两侧都被遮挡。</summary>
    BothClosed = 3
}

/// <summary>
/// <see cref="BlackboardState"/> 的显示名称。
/// </summary>
public static class BlackboardStateNames
{
    public static string GetName(BlackboardState state) => state switch
    {
        BlackboardState.BothOpen => "全部打开",
        BlackboardState.LeftClosed => "左侧合上",
        BlackboardState.RightClosed => "右侧合上",
        BlackboardState.BothClosed => "全部合上",
        _ => state.ToString()
    };
}
