import type { UserAccount } from './userAccount.ts';

/**
 * The fixed roster of saucedemo.com accounts (all share the password "secret_sauce"). Login
 * credentials are never faked with Faker - these accounts must exist on the real demo site.
 *
 * Only the three checkout-capable accounts ({@link poolUsers}) are safe to assign to a parallel worker:
 * the other three are deliberately broken in different ways (locked out entirely, or broken partway
 * through checkout) and would make positive-path scenarios flaky if assigned interchangeably. They are
 * instead targeted directly via the `@user:<username>` tag in scenarios that specifically exercise that
 * behaviour - see `tagHelpers.ts`.
 */
const SHARED_PASSWORD = 'secret_sauce';

export const standardUser: UserAccount = {
  username: 'standard_user',
  password: SHARED_PASSWORD,
  canLogIn: true,
  canCompleteCheckout: true
};

export const performanceGlitchUser: UserAccount = {
  username: 'performance_glitch_user',
  password: SHARED_PASSWORD,
  canLogIn: true,
  canCompleteCheckout: true
};

export const visualUser: UserAccount = {
  username: 'visual_user',
  password: SHARED_PASSWORD,
  canLogIn: true,
  canCompleteCheckout: true
};

/** Logs in fine, but the checkout last-name field is broken - cannot complete a purchase. */
export const problemUser: UserAccount = {
  username: 'problem_user',
  password: SHARED_PASSWORD,
  canLogIn: true,
  canCompleteCheckout: false
};

/** Logs in fine, but fails on cart removal / checkout completion. */
export const errorUser: UserAccount = {
  username: 'error_user',
  password: SHARED_PASSWORD,
  canLogIn: true,
  canCompleteCheckout: false
};

/** Login is rejected outright by design. */
export const lockedOutUser: UserAccount = {
  username: 'locked_out_user',
  password: SHARED_PASSWORD,
  canLogIn: false,
  canCompleteCheckout: false
};

const all = [standardUser, performanceGlitchUser, visualUser, problemUser, errorUser, lockedOutUser];

/** All six accounts, keyed by username, for tag-based lookup (e.g. `@user:problem_user`). */
export const allUsers: ReadonlyMap<string, UserAccount> = new Map(all.map((u) => [u.username, u]));

/**
 * The accounts safe to assign one-per-worker, derived from `canCompleteCheckout` rather than listed by
 * hand, so this can never silently drift from the flags above. Length must be at least
 * `MAX_PARALLEL_WORKERS` - `assignedUser.ts` asserts this at import time.
 */
export const poolUsers: readonly UserAccount[] = all.filter((u) => u.canCompleteCheckout);

export function userByUsername(username: string): UserAccount {
  const account = allUsers.get(username);
  if (!account) {
    throw new Error(
      `No saucedemo user named "${username}" in the catalog. Known users: ${[...allUsers.keys()].join(', ')}.`
    );
  }
  return account;
}
