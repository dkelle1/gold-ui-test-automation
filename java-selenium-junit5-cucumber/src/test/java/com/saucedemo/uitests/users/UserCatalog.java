package com.saucedemo.uitests.users;

import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.stream.Collectors;

/**
 * The fixed roster of saucedemo.com accounts (all share the password "secret_sauce"). Login credentials
 * are never faked with Datafaker - these accounts must exist on the real demo site.
 *
 * <p>Only the three checkout-capable accounts ({@link #POOL_USERS}) are safe to lease to a parallel
 * scenario: the other three are deliberately broken in different ways (locked out entirely, or broken
 * partway through checkout) and would make positive-path scenarios flaky if handed out
 * interchangeably. They are instead targeted directly via the {@code @user:<username>} tag in the
 * scenarios that specifically exercise that behaviour.
 */
public final class UserCatalog {

    private static final String SHARED_PASSWORD = "secret_sauce";

    /** Baseline account: login and checkout both work normally. */
    public static final UserAccount STANDARD_USER = new UserAccount("standard_user", SHARED_PASSWORD, true, true);

    /** Login and checkout work, but the UI has ~5s artificial delays - needs generous waits. */
    public static final UserAccount PERFORMANCE_GLITCH_USER =
            new UserAccount("performance_glitch_user", SHARED_PASSWORD, true, true);

    /** Login and checkout work; the site has cosmetic/layout defects only. */
    public static final UserAccount VISUAL_USER = new UserAccount("visual_user", SHARED_PASSWORD, true, true);

    /** Logs in fine, but the checkout last-name field is broken - cannot complete a purchase. */
    public static final UserAccount PROBLEM_USER = new UserAccount("problem_user", SHARED_PASSWORD, true, false);

    /**
     * Logs in fine, but fails on cart removal / checkout completion. Not currently targeted by any
     * scenario here - the cart-removal scenario that used to exercise it never passed reliably against
     * the live site across any of this gallery's frameworks, so it was removed as flaky. Kept in the
     * catalog for completeness, matching the other five documented saucedemo accounts.
     */
    public static final UserAccount ERROR_USER = new UserAccount("error_user", SHARED_PASSWORD, true, false);

    /** Login is rejected outright by design. */
    public static final UserAccount LOCKED_OUT_USER =
            new UserAccount("locked_out_user", SHARED_PASSWORD, false, false);

    private static final List<UserAccount> ALL = List.of(
            STANDARD_USER, PERFORMANCE_GLITCH_USER, VISUAL_USER, PROBLEM_USER, ERROR_USER, LOCKED_OUT_USER);

    /** All six accounts, keyed by username, for tag-based lookup (e.g. {@code @user:problem_user}). */
    public static final Map<String, UserAccount> ALL_USERS = ALL.stream()
            .collect(Collectors.toMap(
                    UserAccount::username, account -> account, (a, b) -> a, LinkedHashMap::new));

    /**
     * The accounts safe to lease from the parallel pool, derived from {@code canCompleteCheckout} rather
     * than listed by hand so the pool can never silently drift from the capability flags above. Size must
     * be at least {@code ParallelSettings.MAX_PARALLEL_WORKERS} - {@link UserPool}'s constructor enforces
     * that.
     */
    public static final List<UserAccount> POOL_USERS =
            ALL.stream().filter(UserAccount::canCompleteCheckout).toList();

    private UserCatalog() {
    }

    public static UserAccount byUsername(String username) {
        UserAccount account = ALL_USERS.get(username);
        if (account == null) {
            throw new IllegalArgumentException("No saucedemo user named \"" + username
                    + "\" in the catalog. Known users: " + String.join(", ", ALL_USERS.keySet()) + ".");
        }
        return account;
    }
}
