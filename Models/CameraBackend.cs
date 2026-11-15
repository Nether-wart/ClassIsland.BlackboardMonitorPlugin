namespace ClassIsland.BlackboardMonitorPlugin.Models;

/// <summary>
/// 摄像头取流后端。不同后端对设备的兼容性与启动耗时不同。
/// </summary>
public enum CameraBackend
{
    /// <summary>由 OpenCV 自动选择（Windows 上等价于 DirectShow）。</summary>
    Auto = 0,

    /// <summary>DirectShow。启动快，Windows 上最常用。</summary>
    DirectShow = 1,

    /// <summary>Media Foundation。启动较慢，但对部分设备更稳定。</summary>
    MediaFoundation = 2
}
