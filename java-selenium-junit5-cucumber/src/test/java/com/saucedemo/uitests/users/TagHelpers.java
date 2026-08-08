package com.saucedemo.uitests.users;

import io.cucumber.java.Scenario;
import java.util.Optional;

/** Parses typed data out of the Gherkin {@code @tag} values on the current scenario. */
public final class TagHelpers {

    private static final String USER_TAG_PREFIX = "@user:";

    private TagHelpers() {
    }

    /**
     * Returns the account named by a {@code @user:<username>} tag on the scenario, if present. Lets a
     * scenario bypass the shared pool and target one specific (often deliberately broken) saucedemo
     * account directly, e.g. {@code @user:locked_out_user}.
     *
     * <p>Cucumber reports tags with their leading {@code @} intact in {@link Scenario#getSourceTagNames()},
     * hence the prefix constant above.
     */
    public static Optional<UserAccount> taggedUser(Scenario scenario) {
        return scenario.getSourceTagNames().stream()
                .filter(tag -> tag.startsWith(USER_TAG_PREFIX))
                .findFirst()
                .map(tag -> UserCatalog.byUsername(tag.substring(USER_TAG_PREFIX.length())));
    }
}
