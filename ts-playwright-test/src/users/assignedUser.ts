import { MAX_PARALLEL_WORKERS } from '../config/parallelSettings';
import { TestBugError } from '../support/errors';
import { poolUsers } from './userCatalog';
import type { UserAccount } from './userAccount';

if (poolUsers.length < MAX_PARALLEL_WORKERS) {
  throw new TestBugError(
    `poolUsers has ${poolUsers.length} account(s) but MAX_PARALLEL_WORKERS is ${MAX_PARALLEL_WORKERS}. ` +
      'Every parallel worker needs its own account, or two workers would collide on the same login session.'
  );
}

/**
 * Returns the saucedemo account assigned to a worker for its whole lifetime.
 *
 * A plain function of `parallelIndex`, with no environment lookup of its own - which is the meaningful
 * difference from the Cucumber sibling, where the same logic had to read `process.env.CUCUMBER_WORKER_ID`
 * and cope with it being unset outside `--parallel`. Playwright hands `parallelIndex` to a worker-scoped
 * fixture directly (see fixtures.ts), and guarantees it is a stable integer in `[0, workers)` for that
 * worker's entire life, whether or not parallelism is actually in play. That makes this pure, trivially
 * testable, and impossible to get wrong by forgetting to set an env var.
 *
 * Worth noting for the browser matrix: `parallelIndex` is bounded by the *worker* count, not by the
 * number of projects. Running chromium, firefox and webkit together therefore does not increase how
 * many distinct accounts the run needs - two tests sharing a `parallelIndex` never execute at the same
 * time, by definition.
 */
export function getAssignedUser(parallelIndex: number): UserAccount {
  if (!Number.isInteger(parallelIndex) || parallelIndex < 0) {
    throw new TestBugError(`parallelIndex must be a non-negative integer, got ${parallelIndex}.`);
  }

  const index = parallelIndex % poolUsers.length;
  const user = poolUsers[index];
  if (!user) {
    throw new TestBugError(
      `No pooled user at index ${index} (parallelIndex ${parallelIndex}) - poolUsers has ${poolUsers.length} entries.`
    );
  }
  return user;
}
