package com.saucedemo.uitests.config;

import java.util.Arrays;
import java.util.Locale;
import java.util.stream.Collectors;

public enum BrowserName {
    CHROME,
    FIREFOX,
    EDGE;

    /** Accepts the lowercase spellings used in appsettings.json ("chrome") and the BROWSER env var. */
    public static BrowserName parse(String value) {
        try {
            return BrowserName.valueOf(value.trim().toUpperCase(Locale.ROOT));
        } catch (IllegalArgumentException e) {
            String supported = Arrays.stream(values())
                    .map(name -> name.name().toLowerCase(Locale.ROOT))
                    .collect(Collectors.joining(", "));
            throw new IllegalArgumentException(
                    "Unsupported browser \"" + value + "\" - expected one of: " + supported + ".");
        }
    }
}
