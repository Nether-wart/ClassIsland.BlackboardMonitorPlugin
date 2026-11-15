namespace ClassIsland.BlackboardMonitorPlugin.Models;

/// <summary>
/// 一次黑板状态检测的参数。
/// </summary>
public sealed class BlackboardDetectionParameters
{
    /// <summary>灰度低于该值的像素视为“黑”。</summary>
    public int DarkThreshold { get; init; } = 35;

    /// <summary>一列中黑像素占比超过该值时，认为该列被黑板遮挡。</summary>
    public double ColumnDarkRatio { get; init; } = 0.5;

    /// <summary>连续遮挡列宽度小于该比例时视为两侧都打开。</summary>
    public double MinRunWidth { get; init; } = 0.05;

    /// <summary>遮挡区域宽度达到该比例且中心居中时视为两侧都合上。</summary>
    public double BothClosedRunWidth { get; init; } = 0.45;

    /// <summary>允许的遮挡区域中心偏移。</summary>
    public double BothClosedCenterTolerance { get; init; } = 0.12;

    /// <summary>遮挡列总占比超过该值时视为两侧都合上。</summary>
    public double BothClosedCoverage { get; init; } = 0.9;

    /// <summary>检测区域左边界（0~1）。</summary>
    public double RoiX { get; init; }

    /// <summary>检测区域上边界（0~1）。</summary>
    public double RoiY { get; init; }

    /// <summary>检测区域宽度（0~1）。</summary>
    public double RoiWidth { get; init; } = 1;

    /// <summary>检测区域高度（0~1）。</summary>
    public double RoiHeight { get; init; } = 1;
}

/// <summary>
/// 一次黑板状态检测的结果。
/// </summary>
/// <param name="State">判定出的状态。</param>
/// <param name="Coverage">检测区域中被遮挡的列占全部列的比例。</param>
/// <param name="RunWidth">最长连续遮挡列宽度（按检测区域宽度归一化）。</param>
/// <param name="RunCenter">最长连续遮挡区域的中心（按检测区域宽度归一化），无遮挡时为 <see cref="double.NaN"/>。</param>
/// <param name="MaxColumnDarkRatio">单列中最高的黑像素占比，用于调试阈值。</param>
/// <param name="RunStart">最长连续遮挡区域的起点（按检测区域宽度归一化），无遮挡时为 <see cref="double.NaN"/>。</param>
/// <param name="RunEnd">最长连续遮挡区域的终点（按检测区域宽度归一化），无遮挡时为 <see cref="double.NaN"/>。</param>
public readonly record struct BlackboardDetectionResult(
    BlackboardState State,
    double Coverage,
    double RunWidth,
    double RunCenter,
    double MaxColumnDarkRatio,
    double RunStart,
    double RunEnd);
