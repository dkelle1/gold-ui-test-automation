package com.saucedemo.uitests.support;

import com.saucedemo.uitests.config.ConfigurationLoader;
import com.saucedemo.uitests.config.ParallelSettings;
import com.saucedemo.uitests.config.TestSettings;
import com.saucedemo.uitests.users.UserCatalog;
import java.io.IOException;
import java.io.UncheckedIOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.List;
import org.openqa.selenium.remote.RemoteWebDriver;

/**
 * Writes Allure's environment.properties into the results directory so the report's "Environment" tab
 * shows what this run actually exercised (base URL, browser, worker/pool sizing, CI context).
 *
 * <p>Written once at the start of the run rather than at the end, so the file still exists - with all its
 * context - even if the run crashes or is cancelled partway through.
 */
public final class EnvironmentWriter {

    /** Matches allure.results.directory in src/test/resources/allure.properties. */
    private static final String RESULTS_DIRECTORY = "allure-results";

    private EnvironmentWriter() {
    }

    public static void write() {
        TestSettings settings = ConfigurationLoader.settings();
        Path resultsDir = Paths.get(RESULTS_DIRECTORY).toAbsolutePath();

        List<String> lines = List.of(
                "BaseUrl=" + settings.baseUrl(),
                "Browser=" + ConfigurationLoader.browserDisplayName(settings.browser()),
                "Headless=" + settings.headless(),
                "Workers=" + ParallelSettings.MAX_PARALLEL_WORKERS,
                "UserPoolSize=" + UserCatalog.POOL_USERS.size(),
                "SeleniumVersion=" + seleniumVersion(),
                "Java=" + System.getProperty("java.version"),
                "OS=" + System.getProperty("os.name") + " " + System.getProperty("os.version"),
                "CI=" + "true".equalsIgnoreCase(String.valueOf(System.getenv("CI"))),
                "GITHUB_RUN_ID=" + orEmpty(System.getenv("GITHUB_RUN_ID")));

        try {
            Files.createDirectories(resultsDir);
            Files.write(resultsDir.resolve("environment.properties"),
                    String.join(System.lineSeparator(), lines).getBytes(StandardCharsets.UTF_8));
        } catch (IOException e) {
            throw new UncheckedIOException("Could not write environment.properties to " + resultsDir, e);
        }
    }

    private static String seleniumVersion() {
        String version = RemoteWebDriver.class.getPackage().getImplementationVersion();
        return version != null ? version : "unknown";
    }

    private static String orEmpty(String value) {
        return value != null ? value : "";
    }
}
