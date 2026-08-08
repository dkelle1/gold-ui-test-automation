package com.saucedemo.uitests.users;

/**
 * The one {@link UserPool} every parallel scenario in this JVM leases from.
 *
 * <p>Built by class initialisation rather than in a {@code @BeforeAll} hook: the JVM already guarantees
 * that runs exactly once, under its own lock, even with three scenario threads racing to touch it first.
 * A misconfigured pool therefore fails on first use with the constructor's own diagnostic message,
 * before any browser is launched.
 */
public final class SharedUserPool {

    private static final UserPool INSTANCE = new UserPool(UserCatalog.POOL_USERS);

    private SharedUserPool() {
    }

    public static UserPool get() {
        return INSTANCE;
    }
}
