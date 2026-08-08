package com.saucedemo.uitests.testdata;

import com.saucedemo.uitests.users.UserCatalog;
import net.datafaker.Faker;

/**
 * Generates random credentials for negative login scenarios, guaranteed not to collide with a real
 * {@link UserCatalog} account.
 */
public final class InvalidCredentialsFactory {

    private static final int PASSWORD_LENGTH = 12;

    private InvalidCredentialsFactory() {
    }

    public record Credentials(String username, String password) {
    }

    public static Credentials create() {
        Faker faker = new Faker();

        String username = faker.internet().username();
        while (UserCatalog.ALL_USERS.containsKey(username)) {
            username = faker.internet().username();
        }

        return new Credentials(username, faker.internet().password(PASSWORD_LENGTH, PASSWORD_LENGTH + 1));
    }
}
