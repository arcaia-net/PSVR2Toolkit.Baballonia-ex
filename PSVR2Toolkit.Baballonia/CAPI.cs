using System.Runtime.InteropServices;
using System.Threading;

namespace PSVR2Toolkit.Baballonia;

internal interface IGazeImageApi
{
    void Initialize();
    void GetGazeImage(byte[] imageBuffer);
}

internal sealed class NativeGazeImageApi : IGazeImageApi
{
    public static NativeGazeImageApi Instance { get; } = new();

    private int _initialized;

    private NativeGazeImageApi()
    {
    }

    public void Initialize()
    {
        if (Volatile.Read(ref _initialized) == 1) return;

        CAPI.CAPI_Initialize();
        Volatile.Write(ref _initialized, 1);
    }

    public void GetGazeImage(byte[] imageBuffer)
    {
        CAPI.CAPI_GetGazeImage(imageBuffer);
    }
}

internal static class CAPI
{
    private const string DllName = "PSVR2Toolkit.CAPI.dll";

    /// <summary>
    /// Initializes CAPI for usage, required to call other functions.
    /// </summary>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CAPI_Initialize();

    /// <summary>
    /// Gets the current gaze image and loads it into the image buffer.
    /// </summary>
    /// <param name="imageBuffer">An array to load the current gaze image into, must be 0x200100 bytes in length.</param>
    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void CAPI_GetGazeImage(byte[] imageBuffer);
}
