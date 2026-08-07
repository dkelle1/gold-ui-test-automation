package com.saucedemo.uitests.config;

import java.time.Duration;

/**
 * Bound from the {@code TestSettings} section of appsettings*.json, overridable by the plain env vars
 * documented in this framework's README.
 *
 * @param remoteUrl a Selenium Grid / remote WebDriver endpoint; null means launch a local browser
 */
public record TestSettings(
        String baseUrl,
        BrowserName browser,
        boolean headless,
        String remoteUrl,
        int explicitWaitSeconds,
        int pageLoadTimeoutSeconds) {

    public Duration explicitWait() {
        return Duration.ofSeconds(explicitWaitSeconds);
    }

    public Duration pageLoadTimeout() {
        return Duration.ofSeconds(pageLoadTimeoutSeconds);
    }

    public boolean hasRemoteUrl() {
        return remoteUrl != null && !remoteUrl.isBlank();
    }
}
