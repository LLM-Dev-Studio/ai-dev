# WinAppDriver sample (exploratory)

This folder contains a minimal sample showing how to drive the WinUI desktop app with WinAppDriver/Appium.

## What this sample is

- A starting point for exploratory UI automation.
- Not wired into the main solution or CI by default.
- Intended for local experimentation.

## Prerequisites

1. Install **WinAppDriver** (Windows Application Driver).
2. Start WinAppDriver server before running tests:

```powershell
WinAppDriver.exe 127.0.0.1 4723/wd/hub
```

3. Build your WinUI app so the executable exists:

```powershell
dotnet build ..\..\ai-dev.ui.winui\ai-dev.ui.winui.csproj
```

## Configure app path

Set environment variable `AIDEV_WINUI_EXE` to the absolute path of the built exe, for example:

```powershell
$env:AIDEV_WINUI_EXE = "M:\ai-dev-net\ai-dev.ui.winui\bin\Debug\net10.0-windows10.0.19041.0\win-x64\ai-dev.ui.winui.exe"
```

## Run sample tests

```powershell
dotnet test .\WinAppDriver.Sample.Tests.csproj
```

## Notes

- Inspect your app's AutomationIds with **Inspect.exe** (Windows SDK) or Accessibility Insights.
- The sample currently uses a very basic assertion (window exists).
- Expand with stable selectors (`AccessibilityId`) for reliable tests.

## See Also

- [WinAppDriver on Github](https://github.com/microsoft/WinAppDriver) 