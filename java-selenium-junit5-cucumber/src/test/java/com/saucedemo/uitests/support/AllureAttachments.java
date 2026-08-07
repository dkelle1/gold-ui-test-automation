package com.saucedemo.uitests.support;

import io.qameta.allure.Allure;
import java.io.ByteArrayInputStream;
import java.nio.charset.StandardCharsets;
import java.util.List;
import java.util.stream.Collectors;
import org.openqa.selenium.WebDriver;
import org.openqa.selenium.WebDriverException;
import org.openqa.selenium.logging.LogEntries;
import org.openqa.selenium.logging.LogEntry;
import org.openqa.selenium.logging.LogType;

/**
 * Attaches failure evidence to the current Allure test result.
 *
 * <p>Allure's Java lifecycle keys the "current test" off a {@code ThreadLocal}, and Cucumber runs each
 * scenario on a single thread start to finish, so attaching from a scenario's own thread lands on the
 * right result even with three scenarios in flight at once.
 */
public final class AllureAttachments {

    private AllureAttachments() {
    }

    public static void attachFailureEvidence(WebDriver driver) {
        Allure.addAttachment(
                "failure-screenshot", "image/png", new ByteArrayInputStream(ScreenshotHelper.capturePng(driver)), ".png");
        Allure.addAttachment("page-source", "text/html", driver.getPageSource(), ".html");
        Allure.addAttachment("final-url", "text/plain", driver.getCurrentUrl(), ".txt");

        tryAttachBrowserConsoleLog(driver);
    }

    /**
     * Best-effort: not every browser/driver combination exposes the browser console log (geckodriver does
     * not), and asking for it there throws rather than returning empty.
     */
    private static void tryAttachBrowserConsoleLog(WebDriver driver) {
        try {
            LogEntries entries = driver.manage().logs().get(LogType.BROWSER);
            List<LogEntry> all = entries.getAll();
            if (all.isEmpty()) {
                return;
            }

            String text = all.stream()
                    .map(entry -> "[" + entry.getLevel() + "] " + entry.getTimestamp() + " " + entry.getMessage())
                    .collect(Collectors.joining(System.lineSeparator()));

            Allure.addAttachment(
                    "browser-console-log",
                    "text/plain",
                    new ByteArrayInputStream(text.getBytes(StandardCharsets.UTF_8)),
                    ".txt");
        } catch (WebDriverException | UnsupportedOperationException e) {
            // No console log available from this driver - evidence gathering must never mask the real
            // scenario failure that triggered it.
        }
    }
}
