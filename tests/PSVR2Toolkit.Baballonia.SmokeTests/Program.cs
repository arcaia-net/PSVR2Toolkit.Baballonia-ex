using Microsoft.Extensions.Logging;
using PSVR2Toolkit.Baballonia;

var failures = new List<string>();

CheckProviderMatching(failures);
await CheckInvalidFrameDoesNotBecomeReady(failures);
await CheckStopAndRestart(failures);

if (failures.Count > 0)
{
    foreach (var failure in failures)
    {
        Console.Error.WriteLine(failure);
    }

    return 1;
}

Console.WriteLine("PSVR2Toolkit.Baballonia smoke checks passed.");
return 0;

static void CheckProviderMatching(List<string> failures)
{
    var factory = new Vr2CaptureFactory(new TestLoggerFactory());

    var accepted = new[]
    {
        "psvr2",
        "psvr2://gaze",
        "psvr2://gaze/",
        "playstation-vr2",
        "PlayStation VR2",
    };

    foreach (var source in accepted)
    {
        if (!factory.CanConnect(source))
        {
            failures.Add($"Expected CanConnect({source}) to be true.");
        }
    }

    var rejected = new[]
    {
        "",
        "0",
        "rtsp://example.local/camera",
        "C:\\video\\sample.mp4",
        "http://example.local",
    };

    foreach (var source in rejected)
    {
        if (factory.CanConnect(source))
        {
            failures.Add($"Expected CanConnect({source}) to be false.");
        }
    }
}

static async Task CheckInvalidFrameDoesNotBecomeReady(List<string> failures)
{
    var api = new FakeGazeImageApi(buffer =>
    {
        Array.Clear(buffer);
        buffer[0] = 0x00;
        buffer[1] = 0x00;
    });

    var capture = new Vr2Capture("psvr2://gaze", new TestLogger<Vr2Capture>(), api);
    await capture.StartCapture();
    await Task.Delay(50);

    if (capture.IsReady)
    {
        failures.Add("Invalid frame made capture ready.");
    }

    if (capture.AcquireRawMat() is not null)
    {
        failures.Add("Invalid frame produced a raw Mat.");
    }

    await capture.StopCapture();
}

static async Task CheckStopAndRestart(List<string> failures)
{
    var calls = 0;
    var api = new FakeGazeImageApi(buffer =>
    {
        calls++;
        Array.Clear(buffer);
    });

    var capture = new Vr2Capture("psvr2://gaze", new TestLogger<Vr2Capture>(), api);

    await capture.StartCapture();
    await Task.Delay(25);
    await capture.StopCapture();

    if (capture.IsReady)
    {
        failures.Add("StopCapture left capture ready.");
    }

    var callsAfterStop = calls;
    await Task.Delay(25);

    if (calls != callsAfterStop)
    {
        failures.Add("Capture loop continued after StopCapture.");
    }

    await capture.StartCapture();
    await Task.Delay(25);
    await capture.StopCapture();

    if (calls <= callsAfterStop)
    {
        failures.Add("Capture did not restart after StopCapture.");
    }
}

internal sealed class FakeGazeImageApi(Action<byte[]> fillBuffer) : IGazeImageApi
{
    public void Initialize()
    {
    }

    public void GetGazeImage(byte[] imageBuffer)
    {
        fillBuffer(imageBuffer);
    }
}

internal sealed class TestLoggerFactory : ILoggerFactory
{
    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(categoryName);
    }

    public void AddProvider(ILoggerProvider provider)
    {
    }

    public void Dispose()
    {
    }
}

internal sealed class TestLogger<T> : ILogger<T>
{
    private readonly TestLogger _inner = new(typeof(T).FullName ?? typeof(T).Name);

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return _inner.BeginScope(state);
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return _inner.IsEnabled(logLevel);
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _inner.Log(logLevel, eventId, state, exception, formatter);
    }
}

internal sealed class TestLogger(string categoryName) : ILogger
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (logLevel >= LogLevel.Error)
        {
            Console.Error.WriteLine($"{categoryName}: {formatter(state, exception)}");
            if (exception != null)
            {
                Console.Error.WriteLine(exception);
            }
        }
    }
}
