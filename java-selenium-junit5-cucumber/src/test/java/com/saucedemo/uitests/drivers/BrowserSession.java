package com.saucedemo.uitests.drivers;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Comparator;
import org.openqa.selenium.WebDriver;

/**
 * Owns a WebDriver instance together with the throwaway browser profile directory created for it.
 * Closing quits the browser and then best-effort deletes that directory; both steps are idempotent, so
 * a double close from a hook-ordering mistake is harmless.
 */
public final class BrowserSession implements AutoCloseable {

    private final WebDriver driver;
    private final Path userDataDir;
    private boolean closed;

    BrowserSession(WebDriver driver, Path userDataDir) {
        this.driver = driver;
        this.userDataDir = userDataDir;
    }

    public WebDriver driver() {
        return driver;
    }

    @Override
    public void close() {
        if (closed) {
            return;
        }
        closed = true;

        try {
            driver.quit();
        } finally {
            deleteUserDataDir();
        }
    }

    private void deleteUserDataDir() {
        if (userDataDir == null || !Files.exists(userDataDir)) {
            return;
        }

        try (var paths = Files.walk(userDataDir)) {
            // Deepest-first, because a directory can only be deleted once it is empty.
            paths.sorted(Comparator.reverseOrder()).forEach(path -> {
                try {
                    Files.deleteIfExists(path);
                } catch (IOException ignored) {
                    // Best-effort cleanup; the OS temp directory is reclaimed eventually.
                }
            });
        } catch (IOException ignored) {
            // Same - never let profile cleanup turn into a scenario failure.
        }
    }
}
