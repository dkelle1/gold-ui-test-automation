package com.saucedemo.uitests.users;

import java.util.function.Consumer;

/** Holds a leased {@link UserAccount}; closing returns it to its pool exactly once. */
public final class UserLease implements AutoCloseable {

    private final UserAccount account;
    private final Consumer<UserAccount> release;
    private boolean closed;

    UserLease(UserAccount account, Consumer<UserAccount> release) {
        this.account = account;
        this.release = release;
    }

    /**
     * Wraps an account that was never leased from a pool - a {@code @user:<username>} tag override that
     * reads straight from {@link UserCatalog}. Closing it is a no-op, because there is no pool to return
     * it to.
     */
    public UserLease(UserAccount account) {
        this(account, null);
    }

    public UserAccount account() {
        return account;
    }

    @Override
    public void close() {
        if (closed) {
            return;
        }
        closed = true;

        if (release != null) {
            release.accept(account);
        }
    }
}
