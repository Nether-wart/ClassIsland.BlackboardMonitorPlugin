using System.Runtime.InteropServices;
using Avalonia.Threading;
using ClassIsland.BlackboardMonitorPlugin.Models;
using Microsoft.Extensions.Logging;
using OpenCvSharp;

namespace ClassIsland.BlackboardMonitorPlugin.Services;

/// <summary>
/// 黑板开合监视服务：按需打开摄像头、周期性地把画面缩成灰度图交给 <see cref="BlackboardDetector"/> 判定，
/// 并对判定结果做防抖后通过 <see cref="StateChanged"/> 通知订阅者。
/// </summary>
/// <remarks>
/// 摄像头只在有订阅者（触发器或设置页预览）存在、且总开关打开时才会被占用，因此没有配置任何触发器时
/// 不会出现摄像头被长期占用的情况。<see cref="AcquireConsumer"/> 返回的对象释放后即解除订阅。
/// </remarks>
public sealed class BlackboardMonitorService : IDisposable
{
    private const int AnalysisWidth = 160;
    private const int AnalysisHeight = 90;
    private const int PreviewWidth = 640;
    private const int OpenRetryDelaySeconds = 3;

    private readonly ILogger<BlackboardMonitorService> _logger;
    private readonly BlackboardMonitorSettingsStore _store;
    private readonly object _sync = new();
    private readonly List<Consumer> _consumers = [];
    private readonly ManualResetEventSlim _runSignal = new(false);
    private readonly CancellationTokenSource _shutdown = new();
    private readonly byte[] _grayBuffer = new byte[AnalysisWidth * AnalysisHeight];

    private Thread? _worker;
    private string? _status;
    private bool _disposed;
    private volatile bool _reconnectRequested;
    private DateTime _nextOpenAttempt = DateTime.MinValue;
    private BlackboardState? _candidateState;
    private int _candidateCount;

    public BlackboardMonitorService(ILogger<BlackboardMonitorService> logger, BlackboardMonitorSettingsStore store)
    {
        _logger = logger;
        _store = store;
        Settings.PropertyChanged += OnSettingsPropertyChanged;
    }

    /// <summary>当前最新的稳定状态。</summary>
    public BlackboardState CurrentState { get; private set; } = BlackboardState.BothOpen;

    /// <summary>是否已经得到过有效状态。</summary>
    public bool HasState { get; private set; }

    /// <summary>当前的状态描述（错误信息），正常时为 <see langword="null"/>。</summary>
    public string? Status
    {
        get
        {
            lock (_sync)
            {
                return _status;
            }
        }
    }

    public BlackboardMonitorSettings Settings => _store.Settings;

    /// <summary>稳定状态发生变化时触发，始终在 UI 线程上引发。</summary>
    public event EventHandler<BlackboardStateChangedEventArgs>? StateChanged;

    /// <summary>有预览订阅者时，每处理完一帧都会触发，始终在 UI 线程上引发。</summary>
    public event EventHandler<BlackboardPreviewEventArgs>? PreviewUpdated;

    /// <summary><see cref="Status"/> 变化时触发。</summary>
    public event EventHandler? StatusChanged;

    /// <summary>
    /// 订阅监视服务。<paramref name="withPreview"/> 为 <see langword="true"/> 时会同时产生预览画面。
    /// </summary>
    /// <returns>释放后即解除订阅的对象。</returns>
    public IDisposable AcquireConsumer(string reason, bool withPreview = false)
    {
        var consumer = new Consumer(this, reason, withPreview);
        lock (_sync)
        {
            if (_disposed)
            {
                return consumer;
            }

            _consumers.Add(consumer);
            EnsureWorkerStarted();
            UpdateRunSignal();
        }

        _logger.LogInformation("黑板监视已启用订阅：{Reason}（预览：{Preview}）", reason, withPreview);
        return consumer;
    }

    /// <summary>
    /// 请求重新打开摄像头。用于摄像头曾被其它程序占用、之后恢复可用的场景。
    /// </summary>
    public void RequestReconnect()
    {
        _nextOpenAttempt = DateTime.MinValue;
        _reconnectRequested = true;
        lock (_sync)
        {
            UpdateRunSignal();
        }
    }

    private bool HasPreviewConsumer
    {
        get
        {
            lock (_sync)
            {
                return _consumers.Exists(x => x.WithPreview);
            }
        }
    }

    private bool IsActive
    {
        get
        {
            lock (_sync)
            {
                return ShouldRunLocked();
            }
        }
    }

    /// <summary>必须在持有 <see cref="_sync"/> 时调用。</summary>
    /// <remarks>预览订阅者不检查总开关，便于在关闭检测时仍能通过设置页校准画面。</remarks>
    private bool ShouldRunLocked() => !_disposed && _consumers.Exists(x => x.WithPreview || Settings.IsEnabled);

    private void Release(Consumer consumer)
    {
        lock (_sync)
        {
            if (!_consumers.Remove(consumer))
            {
                return;
            }

            UpdateRunSignal();
        }

        _logger.LogInformation("黑板监视已解除订阅：{Reason}（剩余 {Count} 个）", consumer.Reason, CountConsumers());
    }

    private int CountConsumers()
    {
        lock (_sync)
        {
            return _consumers.Count;
        }
    }

    private void UpdateRunSignal()
    {
        if (ShouldRunLocked())
        {
            _runSignal.Set();
        }
        else
        {
            _runSignal.Reset();
        }
    }

    private void EnsureWorkerStarted()
    {
        if (_worker != null)
        {
            return;
        }

        _worker = new Thread(WorkerLoop)
        {
            IsBackground = true,
            Name = "BlackboardMonitor"
        };
        _worker.Start();
    }

    private void WorkerLoop()
    {
        try
        {
            using var capture = new CameraCapture();
            // 注意：这里不能释放 WaitHandle，它由 ManualResetEventSlim / CancellationTokenSource 自己持有。
            var handles = new[] { _runSignal.WaitHandle, _shutdown.Token.WaitHandle };

            while (!_shutdown.IsCancellationRequested)
            {
                if (!IsActive)
                {
                    capture.Close();
                    if (WaitHandle.WaitAny(handles) == 1)
                    {
                        break;
                    }

                    continue;
                }

                try
                {
                    Tick(capture);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "处理黑板监视画面时出错");
                    capture.Close();
                    SetStatus($"检测画面时出错：{ex.Message}");
                }

                var interval = Math.Clamp(Settings.CaptureIntervalMs, 50, 5000);
                if (_shutdown.Token.WaitHandle.WaitOne(interval))
                {
                    break;
                }
            }

            capture.Close();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "黑板监视线程异常退出");
            SetStatus($"监视线程异常退出：{ex.Message}");
        }
    }

    private void Tick(CameraCapture capture)
    {
        var settings = Settings;

        if (_reconnectRequested)
        {
            _reconnectRequested = false;
            capture.Close();
        }

        if (!capture.IsOpen)
        {
            if (DateTime.UtcNow < _nextOpenAttempt)
            {
                return;
            }

            if (!capture.Open(settings.CameraIndex, settings.CaptureBackend))
            {
                _nextOpenAttempt = DateTime.UtcNow.AddSeconds(OpenRetryDelaySeconds);
                SetStatus(capture.LastError);
                return;
            }

            _nextOpenAttempt = DateTime.MinValue;
            _candidateState = null;
            _candidateCount = 0;
            SetStatus(null);
        }

        using var frame = capture.Read();
        if (frame == null || frame.Empty())
        {
            capture.Close();
            SetStatus("读取摄像头画面失败，正在重试。");
            return;
        }

        var result = Analyze(frame, settings);
        UpdateState(result.State, settings.DebounceFrames);

        if (HasPreviewConsumer)
        {
            EmitPreview(frame, result, settings);
        }
    }

    private BlackboardDetectionResult Analyze(Mat frame, BlackboardMonitorSettings settings)
    {
        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);

        using var small = new Mat();
        Cv2.Resize(gray, small, new Size(AnalysisWidth, AnalysisHeight), 0, 0, InterpolationFlags.Area);

        var length = (int)(small.Total() * small.ElemSize());
        if (length != _grayBuffer.Length)
        {
            throw new InvalidOperationException($"分析图尺寸异常：期望 {_grayBuffer.Length} 字节，实际 {length} 字节。");
        }

        Marshal.Copy(small.Data, _grayBuffer, 0, _grayBuffer.Length);
        return BlackboardDetector.Detect(_grayBuffer, AnalysisWidth, AnalysisHeight, settings.ToDetectionParameters());
    }

    private void UpdateState(BlackboardState next, int debounceFrames)
    {
        if (_candidateState == next)
        {
            _candidateCount++;
        }
        else
        {
            _candidateState = next;
            _candidateCount = 1;
        }

        if (_candidateCount < debounceFrames)
        {
            return;
        }

        if (HasState && CurrentState == next)
        {
            return;
        }

        var previous = CurrentState;
        var isInitial = !HasState;
        CurrentState = next;
        HasState = true;
        _candidateCount = 0;

        _logger.LogDebug("黑板状态：{Previous} → {Current}（初始化：{IsInitial}）", previous, next, isInitial);

        var args = new BlackboardStateChangedEventArgs(previous, next, isInitial);
        Post(() => StateChanged?.Invoke(this, args));
    }

    private void EmitPreview(Mat frame, BlackboardDetectionResult result, BlackboardMonitorSettings settings)
    {
        try
        {
            var previewHeight = Math.Max(1, (int)Math.Round((double)PreviewWidth * frame.Height / frame.Width));
            using var preview = new Mat();
            Cv2.Resize(frame, preview, new Size(PreviewWidth, previewHeight), 0, 0, InterpolationFlags.Area);

            var roiX = (int)Math.Round(settings.RoiX * preview.Width);
            var roiY = (int)Math.Round(settings.RoiY * preview.Height);
            var roiW = Math.Max(1, (int)Math.Round(settings.RoiWidth * preview.Width));
            var roiH = Math.Max(1, (int)Math.Round(settings.RoiHeight * preview.Height));

            Cv2.Rectangle(preview, new Rect(roiX, roiY, roiW, roiH), new Scalar(0, 200, 255), 2);
            Cv2.Line(preview, new Point(roiX + (roiW / 2), roiY), new Point(roiX + (roiW / 2), roiY + roiH),
                new Scalar(140, 140, 140), 1);

            if (!double.IsNaN(result.RunStart))
            {
                var runX0 = roiX + (int)Math.Round(result.RunStart * roiW);
                var runX1 = roiX + (int)Math.Round(result.RunEnd * roiW);
                Cv2.Rectangle(preview, new Rect(runX0, roiY, Math.Max(1, runX1 - runX0), roiH),
                    new Scalar(0, 0, 255), 1);
            }

            Cv2.PutText(preview, ToAsciiLabel(result.State), new Point(8, 26), HersheyFonts.HersheySimplex, 0.75,
                new Scalar(255, 255, 255), 2);

            Cv2.ImEncode(".jpg", preview, out var jpeg, new ImageEncodingParam(ImwriteFlags.JpegQuality, 70));
            var args = new BlackboardPreviewEventArgs(jpeg, result);
            Post(() => PreviewUpdated?.Invoke(this, args));
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "生成预览画面失败");
        }
    }

    private static string ToAsciiLabel(BlackboardState state) => state switch
    {
        BlackboardState.BothOpen => "Both open",
        BlackboardState.LeftClosed => "Left closed",
        BlackboardState.RightClosed => "Right closed",
        BlackboardState.BothClosed => "Both closed",
        _ => state.ToString()
    };

    private void SetStatus(string? status)
    {
        bool changed;
        lock (_sync)
        {
            changed = _status != status;
            _status = status;
        }

        if (!changed)
        {
            return;
        }

        if (status != null)
        {
            _logger.LogWarning("黑板监视：{Status}", status);
        }

        Post(() => StatusChanged?.Invoke(this, EventArgs.Empty));
    }

    private void OnSettingsPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(BlackboardMonitorSettings.IsEnabled)
            or nameof(BlackboardMonitorSettings.CameraIndex)
            or nameof(BlackboardMonitorSettings.CaptureBackend)))
        {
            return;
        }

        lock (_sync)
        {
            UpdateRunSignal();
        }

        // 换摄像头后立刻重试，不必等待重试退避。
        _nextOpenAttempt = DateTime.MinValue;
    }

    private static void Post(Action action)
    {
        try
        {
            Dispatcher.UIThread.Post(action);
        }
        catch (Exception)
        {
            // 应用正在退出时调度器可能已经关闭，忽略即可。
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _consumers.Clear();
        }

        Settings.PropertyChanged -= OnSettingsPropertyChanged;
        _shutdown.Cancel();
        _runSignal.Set();
        _worker?.Join(TimeSpan.FromSeconds(2));
        // 不再释放 _runSignal / _shutdown：线程可能仍阻塞在摄像头读取上，提前释放会导致它抛异常。
    }

    private sealed class Consumer(BlackboardMonitorService owner, string reason, bool withPreview) : IDisposable
    {
        private bool _released;

        public string Reason { get; } = reason;

        public bool WithPreview { get; } = withPreview;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            owner.Release(this);
        }
    }
}
