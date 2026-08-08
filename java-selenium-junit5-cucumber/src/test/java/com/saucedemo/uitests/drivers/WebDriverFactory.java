package com.saucedemo.uitests.drivers;

import com.saucedemo.uitests.config.TestSettings;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.net.MalformedURLException;
import java.net.URI;
import java.net.URISyntaxException;
import java.net.URL;
import java.nio.file.Files;
import java.nio.file.Path;
import org.openqa.selenium.MutableCapabilities;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.chrome.ChromeDriver;
import org.openqa.selenium.chrome.ChromeOptions;
import org.openqa.selenium.edge.EdgeDriver;
import org.openqa.selenium.edge.EdgeOptions;
import org.openqa.selenium.firefox.FirefoxDriver;
import org.openqa.selenium.firefox.FirefoxOptions;
import org.openqa.selenium.remote.RemoteWebDriver;

/**
 * Builds browser sessions from {@link TestSettings}.
 *
 * <p>Relies on Selenium Manager (bundled with Selenium 4.6+) to resolve driver binaries automatically -
 * no WebDriverManager dependency or manual PATH setup, matching the Selenium siblings in this gallery.
 * When {@link TestSettings#remoteUrl()} is set, sessions are created against a remote endpoint (e.g. a
 * {@code selenium/standalone-chrome} container or a Grid) instead of a local browser, with no other code
 * change required.
 */
public final class WebDriverFactory {

    private static final String USER_DATA_DIR_PREFIX = "saucedemo-uitests-";

    private WebDriverFactory() {
    }

    public static BrowserSession create(TestSettings settings) {
        Path userDataDir = null;
        MutableCapabilities options;

        switch (settings.browser()) {
            case CHROME -> {
                userDataDir = createUserDataDir();
                options = chromeOptions(settings, userDataDir);
            }
            case EDGE -> {
                userDataDir = createUserDataDir();
                options = edgeOptions(settings, userDataDir);
            }
            case FIREFOX -> options = firefoxOptions(settings);
            default -> throw new IllegalStateException("Unsupported browser: " + settings.browser());
        }

        WebDriver driver = createLocalOrRemoteDriver(settings, options);

        try {
            driver.manage().timeouts().pageLoadTimeout(settings.pageLoadTimeout());

            if (!settings.headless()) {
                // Headless browsers are sized by the explicit --window-size arguments below instead.
                // maximize() in headless Chrome/Edge resizes the window to the headless environment's
                // virtual screen, silently *undoing* --window-size=1920,1080.
                driver.manage().window().maximize();
            }
        } catch (RuntimeException e) {
            // The session already exists here - quit it rather than leaking a live browser process or
            // remote session that nothing will ever close.
            driver.quit();
            throw e;
        }

        return new BrowserSession(driver, userDataDir);
    }

    private static ChromeOptions chromeOptions(TestSettings settings, Path userDataDir) {
        ChromeOptions options = new ChromeOptions();
        options.addArguments("--user-data-dir=" + userDataDir);
        options.addArguments("--window-size=1920,1080");
        options.addArguments("--no-sandbox");
        options.addArguments("--disable-dev-shm-usage");

        if (settings.headless()) {
            options.addArguments("--headless=new");
            options.addArguments("--disable-gpu");
        }

        return options;
    }

    private static EdgeOptions edgeOptions(TestSettings settings, Path userDataDir) {
        EdgeOptions options = new EdgeOptions();
        options.addArguments("--user-data-dir=" + userDataDir);
        options.addArguments("--window-size=1920,1080");
        options.addArguments("--no-sandbox");
        options.addArguments("--disable-dev-shm-usage");

        if (settings.headless()) {
            options.addArguments("--headless=new");
        }

        return options;
    }

    private static FirefoxOptions firefoxOptions(TestSettings settings) {
        FirefoxOptions options = new FirefoxOptions();

        if (settings.headless()) {
            // Mirrors Chrome/Edge's --window-size: maximize() does not reliably resize a headless
            // viewport, so headless Firefox would otherwise default to a small window.
            options.addArguments("-headless");
            options.addArguments("-width=1920");
            options.addArguments("-height=1080");
        }

        return options;
    }

    private static WebDriver createLocalOrRemoteDriver(TestSettings settings, MutableCapabilities options) {
        if (settings.hasRemoteUrl()) {
            return new RemoteWebDriver(toUrl(settings.remoteUrl()), options);
        }

        if (options instanceof ChromeOptions chromeOptions) {
            return new ChromeDriver(chromeOptions);
        }
        if (options instanceof EdgeOptions edgeOptions) {
            return new EdgeDriver(edgeOptions);
        }
        if (options instanceof FirefoxOptions firefoxOptions) {
            return new FirefoxDriver(firefoxOptions);
        }
        throw new IllegalStateException("Unsupported options type: " + options.getClass());
    }

    private static URL toUrl(String remoteUrl) {
        try {
            return new URI(remoteUrl).toURL();
        } catch (URISyntaxException | MalformedURLException e) {
            throw new IllegalStateException("RemoteUrl is not a valid URL: \"" + remoteUrl + "\".", e);
        }
    }

    /**
     * A dedicated profile directory per session. Chrome usually creates its own, but concurrent sessions
     * in a CI container intermittently fail with "user data directory is already in use" without it -
     * and this framework runs three browsers at once by design.
     */
    private static Path createUserDataDir() {
        try {
            return Files.createTempDirectory(USER_DATA_DIR_PREFIX);
        } catch (IOException e) {
            throw new UncheckedIOException("Could not create a temporary browser profile directory.", e);
        }
    }
}
