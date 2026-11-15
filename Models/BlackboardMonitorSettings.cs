using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClassIsland.BlackboardMonitorPlugin.Services;

namespace ClassIsland.BlackboardMonitorPlugin.Models;

/// <summary>
/// 黑板开合检测插件的设置。
/// </summary>
/// <remarks>
/// 默认值来自对样例视频的实测：把检测区域裁剪到画面上方 65% 时，全部打开的帧中单列最暗占比不超过 0.41，
/// 有遮挡的帧中单列最暗占比不低于 0.78，因此 0.5 的列判定阈值有足够余量。
/// </remarks>
public class BlackboardMonitorSettings : INotifyPropertyChanged
{
    private bool _isEnabled = true;
    private int _cameraIndex;
    private CameraBackend _captureBackend = CameraBackend.Auto;
    private int _captureIntervalMs = 200;
    private int _debounceFrames = 3;
    private double _darkThreshold = 35;
    private double _columnDarkRatio = 0.5;
    private double _minRunWidth = 0.05;
    private double _bothClosedRunWidth = 0.45;
    private double _bothClosedCenterTolerance = 0.12;
    private double _bothClosedCoverage = 0.9;
    private double _roiX;
    private double _roiY;
    private double _roiWidth = 1;
    private double _roiHeight = 0.65;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>可选的摄像头后端，供界面下拉框使用。</summary>
    public static CameraBackend[] CaptureBackends { get; } = Enum.GetValues<CameraBackend>();

    /// <summary>总开关。关闭后不再打开摄像头，触发器也不会被触发。</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    /// <summary>摄像头序号。多摄像头时依次尝试 0、1、2……</summary>
    public int CameraIndex
    {
        get => _cameraIndex;
        set => Set(ref _cameraIndex, Math.Max(0, value));
    }

    /// <summary>取流后端。</summary>
    public CameraBackend CaptureBackend
    {
        get => _captureBackend;
        set => Set(ref _captureBackend, value);
    }

    /// <summary>两次检测之间的间隔（毫秒）。越小响应越快，占用也越高。</summary>
    public int CaptureIntervalMs
    {
        get => _captureIntervalMs;
        set => Set(ref _captureIntervalMs, Math.Clamp(value, 50, 5000));
    }

    /// <summary>连续多少帧结果一致才认为状态真正改变，用于抑制抖动。</summary>
    public int DebounceFrames
    {
        get => _debounceFrames;
        set => Set(ref _debounceFrames, Math.Clamp(value, 1, 100));
    }

    /// <summary>灰度低于此值的像素视为“黑”。</summary>
    public double DarkThreshold
    {
        get => _darkThreshold;
        set => Set(ref _darkThreshold, Math.Clamp(value, 0, 255));
    }

    /// <summary>一列中黑像素占比超过此值时，认为该列被黑板遮挡。</summary>
    public double ColumnDarkRatio
    {
        get => _columnDarkRatio;
        set => Set(ref _columnDarkRatio, Math.Clamp(value, 0.05, 0.95));
    }

    /// <summary>连续遮挡列宽度小于此比例时，视为两侧都打开。</summary>
    public double MinRunWidth
    {
        get => _minRunWidth;
        set => Set(ref _minRunWidth, Math.Clamp(value, 0.001, 1));
    }

    /// <summary>遮挡区域宽度达到此比例，且中心接近画面中央时，视为两侧都合上。</summary>
    public double BothClosedRunWidth
    {
        get => _bothClosedRunWidth;
        set => Set(ref _bothClosedRunWidth, Math.Clamp(value, 0.05, 1));
    }

    /// <summary>“中心接近画面中央”的容差，按检测区域宽度归一化。</summary>
    public double BothClosedCenterTolerance
    {
        get => _bothClosedCenterTolerance;
        set => Set(ref _bothClosedCenterTolerance, Math.Clamp(value, 0, 0.5));
    }

    /// <summary>遮挡列总占比超过此值时，也视为两侧都合上（用于摄像头画面偏移的情况）。</summary>
    public double BothClosedCoverage
    {
        get => _bothClosedCoverage;
        set => Set(ref _bothClosedCoverage, Math.Clamp(value, 0.1, 1));
    }

    /// <summary>检测区域左边界（0~1）。</summary>
    public double RoiX
    {
        get => _roiX;
        set => Set(ref _roiX, Math.Clamp(value, 0, 1));
    }

    /// <summary>检测区域上边界（0~1）。</summary>
    public double RoiY
    {
        get => _roiY;
        set => Set(ref _roiY, Math.Clamp(value, 0, 1));
    }

    /// <summary>检测区域宽度（0~1）。</summary>
    public double RoiWidth
    {
        get => _roiWidth;
        set => Set(ref _roiWidth, Math.Clamp(value, 0.05, 1));
    }

    /// <summary>检测区域高度（0~1）。</summary>
    public double RoiHeight
    {
        get => _roiHeight;
        set => Set(ref _roiHeight, Math.Clamp(value, 0.05, 1));
    }

    public BlackboardDetectionParameters ToDetectionParameters() => new()
    {
        DarkThreshold = (int)Math.Round(DarkThreshold),
        ColumnDarkRatio = ColumnDarkRatio,
        MinRunWidth = MinRunWidth,
        BothClosedRunWidth = BothClosedRunWidth,
        BothClosedCenterTolerance = BothClosedCenterTolerance,
        BothClosedCoverage = BothClosedCoverage,
        RoiX = RoiX,
        RoiY = RoiY,
        RoiWidth = RoiWidth,
        RoiHeight = RoiHeight
    };

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
