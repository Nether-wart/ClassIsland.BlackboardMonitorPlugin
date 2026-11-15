using ClassIsland.BlackboardMonitorPlugin.Models;

namespace ClassIsland.BlackboardMonitorPlugin.Services;

/// <summary>
/// 基于“按列统计黑像素”的黑板遮挡检测。
/// </summary>
/// <remarks>
/// <para>
/// 摄像头固定在黑板附近朝教室拍摄，合上的黑板块是一大块近乎全黑的遮挡物，所以逐列统计黑像素占比即可把
/// 遮挡区域与教室背景分开。相比直接比较左右两半的黑像素比例，先求出“最长连续遮挡列区间”再判断其位置与
/// 宽度要稳健得多：合上的黑板往往会越过画面上半部分的中线，直接按半区统计会污染另一半的数值。
/// </para>
/// <para>
/// 判定顺序：遮挡区间过窄 → 两侧打开；区间足够宽且居中（或总占比极高）→ 两侧合上；
/// 否则根据区间中心落在哪一侧判断是哪半边合上。
/// </para>
/// </remarks>
public static class BlackboardDetector
{
    public static BlackboardDetectionResult Detect(
        ReadOnlySpan<byte> gray,
        int width,
        int height,
        BlackboardDetectionParameters parameters)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "帧尺寸无效。");
        }

        if (gray.Length < width * height)
        {
            throw new ArgumentException("灰度缓冲区长度小于帧尺寸。", nameof(gray));
        }

        var x0 = Clamp((int)Math.Round(parameters.RoiX * width), 0, width - 1);
        var y0 = Clamp((int)Math.Round(parameters.RoiY * height), 0, height - 1);
        var x1 = Clamp((int)Math.Round((parameters.RoiX + parameters.RoiWidth) * width), x0 + 1, width);
        var y1 = Clamp((int)Math.Round((parameters.RoiY + parameters.RoiHeight) * height), y0 + 1, height);

        var columns = x1 - x0;
        var rows = y1 - y0;
        var minDarkPixels = parameters.ColumnDarkRatio * rows;
        var darkThreshold = parameters.DarkThreshold;

        var coveredColumns = 0;
        var maxColumnRatio = 0d;
        var bestLength = 0;
        var bestStart = 0;
        var bestEnd = 0;
        var currentLength = 0;
        var currentStart = 0;

        for (var x = 0; x < columns; x++)
        {
            var columnOffset = (y0 * width) + x0 + x;
            var darkPixels = 0;
            for (var y = 0; y < rows; y++)
            {
                if (gray[columnOffset + (y * width)] < darkThreshold)
                {
                    darkPixels++;
                }
            }

            var columnRatio = (double)darkPixels / rows;
            if (columnRatio > maxColumnRatio)
            {
                maxColumnRatio = columnRatio;
            }

            if (columnRatio > parameters.ColumnDarkRatio)
            {
                coveredColumns++;
                if (currentLength == 0)
                {
                    currentStart = x;
                }

                currentLength++;
                if (currentLength > bestLength)
                {
                    bestLength = currentLength;
                    bestStart = currentStart;
                    bestEnd = x + 1;
                }
            }
            else
            {
                currentLength = 0;
            }
        }

        var coverage = (double)coveredColumns / columns;
        var runWidth = (double)bestLength / columns;
        var runStart = bestLength == 0 ? double.NaN : (double)bestStart / columns;
        var runEnd = bestLength == 0 ? double.NaN : (double)bestEnd / columns;
        var runCenter = bestLength == 0 ? double.NaN : (runStart + runEnd) / 2;

        BlackboardState state;
        if (runWidth < parameters.MinRunWidth)
        {
            state = BlackboardState.BothOpen;
        }
        else if (runWidth >= parameters.BothClosedRunWidth &&
                 Math.Abs(runCenter - 0.5) <= parameters.BothClosedCenterTolerance)
        {
            state = BlackboardState.BothClosed;
        }
        else if (coverage >= parameters.BothClosedCoverage)
        {
            state = BlackboardState.BothClosed;
        }
        else
        {
            state = runCenter < 0.5 ? BlackboardState.LeftClosed : BlackboardState.RightClosed;
        }

        return new BlackboardDetectionResult(state, coverage, runWidth, runCenter, maxColumnRatio, runStart, runEnd);
    }

    private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;
}
