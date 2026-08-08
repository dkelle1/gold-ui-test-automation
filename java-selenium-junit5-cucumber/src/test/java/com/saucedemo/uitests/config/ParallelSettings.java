package com.saucedemo.uitests.config;

import java.io.IOException;
import java.io.InputStream;
import java.util.Properties;

/**
 * How many scenarios run concurrently.
 *
 * <p>Unlike the sibling frameworks, this value is not a second, hand-maintained copy of the runner's
 * setting: it is read back from the exact same places the Cucumber engine itself reads it from, in the
 * same precedence order (system property, then {@code junit-platform.properties}). The C# siblings need
 * a compile-time constant for {@code [assembly: LevelOfParallelism(...)]} and the Python sibling's own
 * comment concedes that keeping its constant in sync "is entirely a matter of convention" - here the
 * pool-size guard in {@link com.saucedemo.uitests.users.UserPool} validates against the number the
 * engine will actually use, so the two cannot silently drift apart.
 */
public final class ParallelSettings {

    private static final String PARALLELISM_KEY = "cucumber.execution.parallel.config.fixed.parallelism";
    private static final String PROPERTIES_RESOURCE = "junit-platform.properties";
    private static final int DEFAULT_WORKERS = 3;

    public static final int MAX_PARALLEL_WORKERS = resolveMaxParallelWorkers();

    private ParallelSettings() {
    }

    private static int resolveMaxParallelWorkers() {
        Integer fromSystemProperty = parsePositiveInt(System.getProperty(PARALLELISM_KEY));
        if (fromSystemProperty != null) {
            return fromSystemProperty;
        }

        Integer fromPropertiesFile = parsePositiveInt(readFromPropertiesFile());
        if (fromPropertiesFile != null) {
            return fromPropertiesFile;
        }

        return DEFAULT_WORKERS;
    }

    private static String readFromPropertiesFile() {
        ClassLoader classLoader = ParallelSettings.class.getClassLoader();
        try (InputStream stream = classLoader.getResourceAsStream(PROPERTIES_RESOURCE)) {
            if (stream == null) {
                return null;
            }
            Properties properties = new Properties();
            properties.load(stream);
            return properties.getProperty(PARALLELISM_KEY);
        } catch (IOException e) {
            // A malformed or unreadable properties file should not stop the run from starting - the
            // engine would fall back to its own default here too, and DEFAULT_WORKERS matches it.
            return null;
        }
    }

    /**
     * Returns null (rather than throwing) for anything that is not a positive integer. An un-substituted
     * Maven placeholder or a blank value reaches us as a plain string, and treating that as "not
     * configured" is exactly right - it is what the engine does with it too.
     */
    private static Integer parsePositiveInt(String value) {
        if (value == null || value.isBlank()) {
            return null;
        }
        try {
            int parsed = Integer.parseInt(value.trim());
            return parsed > 0 ? parsed : null;
        } catch (NumberFormatException e) {
            return null;
        }
    }
}
