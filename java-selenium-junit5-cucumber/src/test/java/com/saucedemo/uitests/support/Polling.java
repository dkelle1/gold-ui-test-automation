package com.saucedemo.uitests.support;

import static org.assertj.core.api.Assertions.assertThat;

import java.time.Duration;
import java.util.function.Supplier;

/**
 * Polls a supplier until it returns the expected value or a timeout elapses - for state that updates
 * asynchronously (e.g. a cart badge re-rendering after a click).
 *
 * <p>Deliberately a plain loop with a direct equality check rather than something built on
 * {@code WebDriverWait.until}: {@code until} stops as soon as its callback returns anything falsy-looking
 * and treats that as "not ready", which would misfire for a legitimately expected falsy value (asserting
 * a cart count of exactly 0, say). A direct equality check is the correct general-purpose contract here,
 * not just a stylistic difference.
 */
public final class Polling {

    private static final Duration DEFAULT_TIMEOUT = Duration.ofSeconds(5);
    private static final Duration DEFAULT_POLL_INTERVAL = Duration.ofMillis(250);

    private Polling() {
    }

    public static <T> void assertEventually(Supplier<T> read, T expected, String description) {
        assertEventually(read, expected, description, DEFAULT_TIMEOUT);
    }

    public static <T> void assertEventually(Supplier<T> read, T expected, String description, Duration timeout) {
        long deadline = System.nanoTime() + timeout.toNanos();
        T actual = read.get();

        while (!expected.equals(actual) && System.nanoTime() < deadline) {
            sleep(DEFAULT_POLL_INTERVAL);
            actual = read.get();
        }

        assertThat(actual).as("%s (after polling for %s)", description, timeout).isEqualTo(expected);
    }

    private static void sleep(Duration duration) {
        try {
            Thread.sleep(duration.toMillis());
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
            throw new IllegalStateException("Interrupted while polling.", e);
        }
    }
}
