using Microsoft.Playwright;

namespace SauceDemo.UiTests.Support;

public static class ScreenshotHelper
{
    public static Task<byte[]> CaptureAsync(IPage page) => page.ScreenshotAsync();
}
