using Microsoft.Extensions.Logging;
using OpenCvSharp;
using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Capture = Baballonia.SDK.Capture;

namespace PSVR2Toolkit.Baballonia;

public sealed class Vr2Capture : Capture
{
    private const int IMAGE_WIDTH = 400;
    private const int IMAGE_HEIGHT = 200;
    private const int IMAGE_HEADER_SIZE = 0x100;
    // The image from the buffer is BC4, there is only one channel.
    private const int IMAGE_DATA_SIZE = IMAGE_WIDTH * IMAGE_HEIGHT;

    private readonly IGazeImageApi _gazeImageApi;
    private readonly byte[] _imageBuffer = new byte[0x200100];

    private CancellationTokenSource? _cts;
    private Task? _captureTask;

    public Vr2Capture(string source, ILogger<Vr2Capture> logger)
        : this(source, logger, NativeGazeImageApi.Instance)
    {
    }

    internal Vr2Capture(string source, ILogger<Vr2Capture> logger, IGazeImageApi gazeImageApi)
        : base(source, logger)
    {
        _gazeImageApi = gazeImageApi;
    }

    public override Task<bool> StartCapture()
    {
        if (_captureTask is { IsCompleted: false }) return Task.FromResult(true);

        try
        {
            _gazeImageApi.Initialize();
            Logger.LogInformation("PSVR2 gaze image API initialized. Status={GazeStatus}.", GetGazeStatusForLog());
        }
        catch (Exception e)
        {
            IsReady = false;
            Logger.LogError(e, "Could not initialize PSVR2 gaze image API.");
            return Task.FromResult(false);
        }

        IsReady = false;

        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        _captureTask = Task.Run(() => VideoCapture_UpdateLoop(token), token);

        return Task.FromResult(true);
    }

    private async Task VideoCapture_UpdateLoop(CancellationToken ct)
    {
        var nextInvalidFrameLog = Stopwatch.GetTimestamp();
        var validFrameLogged = false;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                _gazeImageApi.GetGazeImage(_imageBuffer);

                // Check for VI in header.
                if (_imageBuffer[0] == 0x56 && _imageBuffer[1] == 0x49)
                {
                    var mat = new Mat(IMAGE_HEIGHT, IMAGE_WIDTH, MatType.CV_8UC1);
                    // We can skip the header when copying the image data to the matrix data.
                    Marshal.Copy(_imageBuffer, IMAGE_HEADER_SIZE, mat.Data, IMAGE_DATA_SIZE);
                    SetRawMat(mat);
                    IsReady = true;

                    if (!validFrameLogged)
                    {
                        validFrameLogged = true;
                        Logger.LogInformation(
                            "Received first PSVR2 gaze image frame. Status={GazeStatus}, size={Width}x{Height}, header={Header}.",
                            GetGazeStatusForLog(),
                            IMAGE_WIDTH,
                            IMAGE_HEIGHT,
                            FormatHeader(_imageBuffer));
                    }
                }
                else
                {
                    if (Stopwatch.GetTimestamp() >= nextInvalidFrameLog)
                    {
                        Logger.LogWarning(
                            "Waiting for PSVR2 gaze image frame. Status={GazeStatus}, header={Header}.",
                            GetGazeStatusForLog(),
                            FormatHeader(_imageBuffer));
                        nextInvalidFrameLog = Stopwatch.GetTimestamp() + Stopwatch.Frequency;
                    }

                    await Task.Delay(1, ct);
                }
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // Normal shutdown path.
        }
        catch (Exception e)
        {
            IsReady = false;
            Logger.LogError(e, "PSVR2 gaze capture loop failed.");
        }
    }

    public override async Task<bool> StopCapture()
    {
        var captureTask = _captureTask;
        var cts = _cts;

        if (captureTask != null)
        {
            cts?.Cancel();

            try
            {
                await captureTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                // Normal shutdown path.
            }
        }

        cts?.Dispose();
        _cts = null;
        _captureTask = null;
        IsReady = false;
        return true;
    }

    private int GetGazeStatusForLog()
    {
        try
        {
            return _gazeImageApi.GetGazeStatus();
        }
        catch (Exception e)
        {
            Logger.LogWarning(e, "Could not read PSVR2 gaze image API status.");
            return -1;
        }
    }

    private static string FormatHeader(byte[] buffer)
    {
        return string.Join(' ', buffer.AsSpan(0, 8).ToArray().Select(b => b.ToString("X2")));
    }
}
