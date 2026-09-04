import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { getAssignedUser } from '../../src/users/assignedUser';
import { poolUsers } from '../../src/users/userCatalog';

/**
 * Plain Node test-runner unit tests (no Playwright, no browser) covering the user-assignment scheme.
 *
 * Notice what these do *not* have to do, compared with the Cucumber sibling's equivalent: no saving and
 * restoring of `process.env.CUCUMBER_WORKER_ID` around every case, and no "falls back when the env var
 * is unset" case at all. Taking `parallelIndex` as an argument makes the function pure, so the tests are
 * just inputs and outputs. That is the main reason it was written that way.
 *
 * Excluded from Playwright's own run by `testMatch: '**\/*.spec.ts'` in playwright.config.ts; run with
 * `npm run test:unit`.
 */
describe('getAssignedUser', () => {
  it('assigns a distinct account to each of the pool-sized parallel indexes', () => {
    const usernames = poolUsers.map((_, index) => getAssignedUser(index).username);

    assert.equal(
      new Set(usernames).size,
      poolUsers.length,
      'Every concurrently-running worker must get a different account.'
    );
  });

  it('is deterministic - the same parallel index always gets the same account', () => {
    assert.equal(getAssignedUser(1).username, getAssignedUser(1).username);
  });

  it('wraps around via modulo when there are more workers than pooled accounts', () => {
    assert.equal(getAssignedUser(poolUsers.length).username, poolUsers[0]?.username);
  });

  it('only ever returns checkout-capable accounts', () => {
    for (let parallelIndex = 0; parallelIndex < 10; parallelIndex++) {
      assert.equal(getAssignedUser(parallelIndex).canCompleteCheckout, true);
    }
  });

  it('rejects a negative or non-integer parallel index rather than silently misassigning', () => {
    assert.throws(() => getAssignedUser(-1), /non-negative integer/);
    assert.throws(() => getAssignedUser(1.5), /non-negative integer/);
  });
});
