package com.saucedemo.uitests.testdata;

import java.util.Random;
import net.datafaker.Faker;

/**
 * Generates realistic checkout form data with Datafaker. Login credentials are never generated this way -
 * see {@link com.saucedemo.uitests.users.UserCatalog} - only data the app accepts freely.
 *
 * <p>Builds a fresh {@link Faker} per call rather than sharing a static one. Datafaker draws from a
 * {@code Random} that is not safe for concurrent use, and this framework runs three scenarios at once in
 * one JVM, so a shared instance would be exactly the kind of rare, unreproducible flake the sibling
 * frameworks' own comments warn about. Seeding, when asked for, goes through this instance's own
 * {@code Random} - never a global seed.
 */
public final class CheckoutDataFactory {

    private static final int POSTAL_CODE_DIGITS = 5;

    private CheckoutDataFactory() {
    }

    public static CheckoutInfo create() {
        return build(new Faker());
    }

    /** Reproducible variant for debugging a specific generated payload. */
    public static CheckoutInfo create(long seed) {
        return build(new Faker(new Random(seed)));
    }

    private static CheckoutInfo build(Faker faker) {
        return new CheckoutInfo(
                faker.name().firstName(),
                faker.name().lastName(),
                // Constrained to plain 5 digits: Datafaker's zipCode() can also emit the ZIP+4 form
                // (12345-6789), which is noisier than useful for this form.
                faker.numerify("#".repeat(POSTAL_CODE_DIGITS)));
    }
}
