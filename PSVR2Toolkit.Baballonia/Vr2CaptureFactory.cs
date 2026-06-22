using Baballonia.SDK;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace PSVR2Toolkit.Baballonia;

public class Vr2CaptureFactory(ILoggerFactory loggerFactory) : ICaptureFactory
{
    private static readonly HashSet<string> SupportedSources = new(StringComparer.OrdinalIgnoreCase)
    {
        "psvr2",
        "psvr2://gaze",
        "playstation-vr2",
        "playstation vr2",
    };

    public Capture Create(string address)
    {
        return new Vr2Capture(address, loggerFactory.CreateLogger<Vr2Capture>());
    }

    public bool CanConnect(string address)
    {
        return SupportedSources.Contains(NormalizeSource(address));
    }

    public string GetProviderName()
    {
        return "PlayStation VR2";
    }

    private static string NormalizeSource(string? address)
    {
        return (address ?? string.Empty).Trim().TrimEnd('/');
    }
}
