"""The fixed roster of saucedemo.com accounts (all share the password "secret_sauce"). Login
credentials are never faked with Faker - these accounts must exist on the real demo site.

Only the three checkout-capable accounts (`POOL_USERS`) are safe to hand out to a parallel worker: the
other two are deliberately broken in different ways (locked out entirely, or broken partway through
checkout) and would make positive-path scenarios flaky if assigned interchangeably. They are instead
targeted directly via the `@user:<username>` tag in scenarios that specifically exercise that behaviour.
"""

from saucedemo_uitests.users.user_account import UserAccount

_SHARED_PASSWORD = "secret_sauce"

# Baseline account: login and checkout both work normally.
STANDARD_USER = UserAccount("standard_user", _SHARED_PASSWORD, can_log_in=True, can_complete_checkout=True)

# Login and checkout work, but the UI has ~5s artificial delays - needs generous waits.
PERFORMANCE_GLITCH_USER = UserAccount(
    "performance_glitch_user", _SHARED_PASSWORD, can_log_in=True, can_complete_checkout=True
)

# Login and checkout work; the site has cosmetic/layout defects only.
VISUAL_USER = UserAccount("visual_user", _SHARED_PASSWORD, can_log_in=True, can_complete_checkout=True)

# Logs in fine, but the checkout last-name field is broken - cannot complete a purchase.
PROBLEM_USER = UserAccount("problem_user", _SHARED_PASSWORD, can_log_in=True, can_complete_checkout=False)

# Logs in fine, but fails on cart removal / checkout completion. Not currently targeted by any scenario
# here - the cart-removal scenario that used to exercise it never passed reliably against the live site
# across any of this gallery's frameworks, so it was removed as flaky. Kept in the catalog for
# completeness, matching the other five documented saucedemo accounts.
ERROR_USER = UserAccount("error_user", _SHARED_PASSWORD, can_log_in=True, can_complete_checkout=False)

# Login is rejected outright by design.
LOCKED_OUT_USER = UserAccount(
    "locked_out_user", _SHARED_PASSWORD, can_log_in=False, can_complete_checkout=False
)

_ALL: list[UserAccount] = [
    STANDARD_USER,
    PERFORMANCE_GLITCH_USER,
    VISUAL_USER,
    PROBLEM_USER,
    ERROR_USER,
    LOCKED_OUT_USER,
]

# All six accounts, keyed by username, for tag-based lookup (e.g. @user:problem_user).
ALL_USERS: dict[str, UserAccount] = {user.username: user for user in _ALL}

# The accounts safe to assign to a parallel worker, derived from can_complete_checkout rather than
# listed by hand, so the pool can never silently drift from the flags above. Size must be at least
# config.parallel_settings.MAX_PARALLEL_WORKERS - assigned_user.py asserts this at import time.
POOL_USERS: list[UserAccount] = [user for user in _ALL if user.can_complete_checkout]


def user_by_username(username: str) -> UserAccount:
    try:
        return ALL_USERS[username]
    except KeyError:
        known = ", ".join(ALL_USERS.keys())
        raise KeyError(
            f'No saucedemo user named "{username}" in the catalog. Known users: {known}.'
        ) from None
