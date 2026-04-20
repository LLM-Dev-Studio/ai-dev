using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;

using Xunit;

namespace WinAppDriver.Sample.Tests;

public sealed class WinUiSmokeTests : IDisposable
{
    private WindowsDriver? _driver;

    [Fact]
    public void LaunchesAiDevWinUiMainWindow()
    {
        var appPath = Environment.GetEnvironmentVariable("AIDEV_WINUI_EXE");
        if (string.IsNullOrWhiteSpace(appPath))
            Assert.Skip("WinUI smoke test is opt-in. Set AIDEV_WINUI_EXE to the WinUI executable path to enable.");

        if (!File.Exists(appPath))
            Assert.Skip($"WinUI executable not found at AIDEV_WINUI_EXE: {appPath}");

        var options = new AppiumOptions();
        options.AddAdditionalAppiumOption("app", appPath);
        options.AddAdditionalAppiumOption("deviceName", "WindowsPC");

        _driver = new WindowsDriver(new Uri("http://127.0.0.1:4723/wd/hub"), options);
        _driver.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(5);

        var windowTitle = _driver.Title;
        Assert.False(string.IsNullOrWhiteSpace(windowTitle));
    }

    public void Dispose()
    {
        _driver?.Quit();
        _driver?.Dispose();
    }
}
