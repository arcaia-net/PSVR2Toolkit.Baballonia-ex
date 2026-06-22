# PSVR2Toolkit.Baballonia

Unofficial Baballonia module which provides eye-tracking camera feed support for PlayStation VR2, using PlayStation VR2 Toolkit.

## Prerequisites

- Windows x64.
- .NET 10 SDK.
- The Baballonia repository checked out next to this repository.
- PSVR2Toolkit installed and able to provide gaze image data.

## Build

```powershell
dotnet restore PSVR2Toolkit.Baballonia.sln --ignore-failed-sources
dotnet build PSVR2Toolkit.Baballonia.sln -c Release --no-restore
```

The module output is written to `PSVR2Toolkit.Baballonia/bin/Release/net10.0/`.

## Smoke Check Without Hardware

```powershell
dotnet run --project tests/PSVR2Toolkit.Baballonia.SmokeTests/PSVR2Toolkit.Baballonia.SmokeTests.csproj -c Release
```

This checks provider matching and stop/restart behavior with a fake gaze image API. A real PSVR2 is still required to validate the actual frame format and eye images.

## Baballonia Module Placement

Copy these files into the Baballonia Desktop `Modules` folder:

- `PSVR2Toolkit.Baballonia.dll`
- `PSVR2Toolkit.CAPI.dll`

Baballonia may log a `Bad IL format` warning for `PSVR2Toolkit.CAPI.dll` during startup. That file is a native DLL, not a managed module, so the warning is expected as long as `PlayStation VR2` appears as a backend.

Use one of these source strings when selecting the `PlayStation VR2` provider:

- `psvr2`
- `psvr2://gaze`
- `playstation-vr2`
- `PlayStation VR2`

If no image appears, prefer `psvr2` first. The module logs `PSVR2 gaze image API initialized`, `Waiting for PSVR2 gaze image frame`, and `Received first PSVR2 gaze image frame` messages to help distinguish PSVR2Toolkit status problems from frame format problems.
