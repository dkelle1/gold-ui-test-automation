using OpenQA.Selenium;

namespace SauceDemo.UiTests.Support;

public static class ScreenshotHelper
{
    public static byte[] Capture(IWebDriver driver) => ((ITakesScreenshot)driver).GetScreenshot().AsByteArray;
}
