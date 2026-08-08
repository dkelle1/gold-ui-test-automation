package com.saucedemo.uitests.config;

import com.fasterxml.jackson.databind.JsonNode;
import com.fasterxml.jackson.databind.ObjectMapper;
import java.io.IOException;
import java.net.URI;
import java.net.URISyntaxException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Locale;

/**
 * Loads settings once per JVM from appsettings.json, layers appsettings.ci.json on top whenever the
 * standard {@code CI=true} env var is present (GitHub Actions sets it automatically), then applies env
 * var overrides, which always win.
 *
 * <p>Files are resolved against the working directory, which Maven sets to the framework root - the
 * same "read the JSON next to the project" contract the sibling frameworks use.
 */
public final class ConfigurationLoader {

    private static final String SETTINGS_SECTION = "TestSettings";
    private static final int DEFAULT_EXPLICIT_WAIT_SECONDS = 20;
    private static final int DEFAULT_PAGE_LOAD_TIMEOUT_SECONDS = 30;

    /**
     * Holder idiom: the JVM guarantees a class is initialised exactly once, under its own lock, so the
     * settings are built exactly once even though parallel scenario threads all race to read them.
     */
    private static final class Holder {
        private static final TestSettings INSTANCE = load();
    }

    private ConfigurationLoader() {
    }

    public static TestSettings settings() {
        return Holder.INSTANCE;
    }

    private static TestSettings load() {
        ObjectMapper mapper = new ObjectMapper();
        JsonNode base = readSettingsSection(mapper, "appsettings.json");
        JsonNode ciOverrides = isCi() ? readSettingsSection(mapper, "appsettings.ci.json") : null;

        String baseUrl = firstNonBlank(System.getenv("BASE_URL"), textOf(base, ciOverrides, "BaseUrl"));
        if (baseUrl == null) {
            throw new IllegalStateException(
                    "TestSettings.BaseUrl must be configured (appsettings.json or the BASE_URL env var).");
        }
        requireAbsoluteUrl(baseUrl);

        String browserValue =
                firstNonBlank(System.getenv("BROWSER"), textOf(base, ciOverrides, "Browser"), "chrome");

        String headlessValue = firstNonBlank(System.getenv("HEADLESS"), textOf(base, ciOverrides, "Headless"));
        boolean headless = "true".equalsIgnoreCase(headlessValue);

        String remoteUrl = firstNonBlank(System.getenv("REMOTE_URL"), textOf(base, ciOverrides, "RemoteUrl"));

        return new TestSettings(
                baseUrl,
                BrowserName.parse(browserValue),
                headless,
                remoteUrl,
                intSetting(
                        System.getenv("EXPLICIT_WAIT_SECONDS"),
                        textOf(base, ciOverrides, "ExplicitWaitSeconds"),
                        DEFAULT_EXPLICIT_WAIT_SECONDS),
                intSetting(
                        System.getenv("PAGE_LOAD_TIMEOUT_SECONDS"),
                        textOf(base, ciOverrides, "PageLoadTimeoutSeconds"),
                        DEFAULT_PAGE_LOAD_TIMEOUT_SECONDS));
    }

    private static boolean isCi() {
        return "true".equalsIgnoreCase(String.valueOf(System.getenv("CI")));
    }

    private static JsonNode readSettingsSection(ObjectMapper mapper, String fileName) {
        Path path = Paths.get(fileName).toAbsolutePath();
        if (!Files.isRegularFile(path)) {
            return null;
        }

        try {
            JsonNode section = mapper.readTree(path.toFile()).get(SETTINGS_SECTION);
            return section != null && section.isObject() ? section : null;
        } catch (IOException e) {
            throw new IllegalStateException("Could not read " + path + ": " + e.getMessage(), e);
        }
    }

    /** CI overrides win over the base file; a JSON null counts as "not set", matching RemoteUrl: null. */
    private static String textOf(JsonNode base, JsonNode ciOverrides, String key) {
        String fromOverrides = textOf(ciOverrides, key);
        return fromOverrides != null ? fromOverrides : textOf(base, key);
    }

    private static String textOf(JsonNode node, String key) {
        if (node == null) {
            return null;
        }
        JsonNode value = node.get(key);
        if (value == null || value.isNull()) {
            return null;
        }
        return value.asText();
    }

    private static String firstNonBlank(String... values) {
        for (String value : values) {
            if (value != null && !value.isBlank()) {
                return value;
            }
        }
        return null;
    }

    private static int intSetting(String envValue, String configValue, int defaultValue) {
        String chosen = firstNonBlank(envValue, configValue);
        if (chosen == null) {
            return defaultValue;
        }
        try {
            return Integer.parseInt(chosen.trim());
        } catch (NumberFormatException e) {
            throw new IllegalStateException("Expected an integer setting but got \"" + chosen + "\".", e);
        }
    }

    private static void requireAbsoluteUrl(String baseUrl) {
        try {
            if (new URI(baseUrl).getScheme() == null) {
                throw new IllegalStateException(
                        "TestSettings.BaseUrl is not a valid absolute URL: \"" + baseUrl + "\".");
            }
        } catch (URISyntaxException e) {
            throw new IllegalStateException(
                    "TestSettings.BaseUrl is not a valid absolute URL: \"" + baseUrl + "\".", e);
        }
    }

    /** Only used by EnvironmentWriter, to report the browser in the same lowercase spelling as the config. */
    public static String browserDisplayName(BrowserName browser) {
        return browser.name().toLowerCase(Locale.ROOT);
    }
}
