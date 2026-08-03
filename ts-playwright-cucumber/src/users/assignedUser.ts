import { MAX_PARALLEL_WORKERS } from '../config/parallelSettings.ts';
import { poolUsers } from './userCatalog.ts';
import type { UserAccount } from './userAccount.ts';

if (poolUsers.length < MAX_PARALLEL_WORKERS) {
  throw new Error(
    `poolUsers has ${poolUsers.length} account(s) but MAX_PARALLEL_WORKERS is ${MAX_PARALLEL_WORKERS}. ` +
      'Every parallel worker needs its own account, or two workers would collide on the same login session.'
  );
}

/**
 * Returns the saucedemo account permanently assigned to the current worker for this whole run - not a
 * leased-and-returned pool slot like the C# siblings' `UserPool`. Cucumber.js's `--parallel` workers are
 * Node `worker_threads`, each with its own isolated module scope and a distinct, stable
 * `process.env.CUCUMBER_WORKER_ID` ("0", "1", "2", ...) for its entire lifetime - so instead of a
 * blocking collection every scenario leases from and returns to, each worker can just be handed one
 * account for good, computed once, with no locking or release step needed at all.
 *
 * Falls back to worker "0" when the env var is unset, which happens when cucumber-js runs without
 * `--parallel` (no worker_threads involved at all, so nothing sets it).
 */
export function getAssignedUser(): UserAccount {
  const workerId = Number.parseInt(process.env.CUCUMBER_WORKER_ID ?? '0', 10);
  const index = workerId % poolUsers.length;
  const user = poolUsers[index];
  if (!user) {
    throw new Error(
      `No pooled user at index ${index} (worker ${workerId}) - poolUsers has ${poolUsers.length} entries.`
    );
  }
  return user;
}
