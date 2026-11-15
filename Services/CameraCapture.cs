using ClassIsland.BlackboardMonitorPlugin.Models;
using OpenCvSharp;

namespace ClassIsland.BlackboardMonitorPlugin.Services;

/// <summary>
/// 对 OpenCV <see cref="VideoCapture"/> 的薄封装，负责按指定后端打开摄像头并读取原始帧。
/// </summary>
internal sealed class CameraCapture : IDisposable
{
    private VideoCapture? _capture;

    public bool IsOpen => _capture?.IsOpened() == true;

    public string? LastError { get; private set; }

    public bool Open(int deviceIndex, CameraBackend backend)
    {
        Close();
        LastError = null;

        var capture = new VideoCapture();
        try
        {
            var api = backend switch
            {
                CameraBackend.DirectShow => VideoCaptureAPIs.DSHOW,
                CameraBackend.MediaFoundation => VideoCaptureAPIs.MSMF,
                _ => OperatingSystem.IsWindows() ? VideoCaptureAPIs.DSHOW : VideoCaptureAPIs.ANY
            };

            if (!capture.Open(deviceIndex, api))
            {
                capture.Dispose();
                LastError = $"无法打开摄像头 {deviceIndex}（后端：{backend}）。请检查摄像头是否被其它程序占用，以及系统设置中是否允许应用访问摄像头。";
                return false;
            }

            // 只保留最新一帧，避免总是拿到摄像头驱动缓冲里的旧画面。
            capture.Set(VideoCaptureProperties.BufferSize, 1);
            _capture = capture;
            return true;
        }
        catch (Exception ex)
        {
            capture.Dispose();
            LastError = $"打开摄像头 {deviceIndex} 时出错：{ex.Message}";
            return false;
        }
    }

    /// <summary>
    /// 读取一帧 BGR 图像。读取失败时返回 <see langword="null"/>，调用方应关闭并重开设备。
    /// </summary>
    public Mat? Read()
    {
        if (_capture == null)
        {
            return null;
        }

        var frame = new Mat();
        try
        {
            if (!_capture.Read(frame) || frame.Empty())
            {
                frame.Dispose();
                return null;
            }

            return frame;
        }
        catch
        {
            frame.Dispose();
            return null;
        }
    }

    public void Close()
    {
        if (_capture == null)
        {
            return;
        }

        try
        {
            _capture.Release();
        }
        catch
        {
            // 释放失败无需处理，设备随后会被 GC 回收。
        }

        _capture.Dispose();
        _capture = null;
    }

    public void Dispose() => Close();
}
