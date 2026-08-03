import { afterEach, describe, it } from 'node:test';
import { expect } from 'chai';
import { getAssignedUser } from '../src/users/assignedUser.ts';
import { poolUsers } from '../src/users/userCatalog.ts';

/**
 * Plain Node test-runner unit tests (no Cucumber, no browser) covering the one genuinely novel piece of
 * this framework's user-assignment scheme: that distinct worker IDs get distinct accounts, the mapping
 * is stable, and it degrades sensibly outside its designed range. The C# siblings' equivalent
 * (`UserPoolTests`) tests a `BlockingCollection`-based lease/release pool instead - a different
 * mechanism entirely, needed there because their parallelism model can't give each worker a fixed
 * account for its whole lifetime the way `CUCUMBER_WORKER_ID` lets this one.
 */
describe('getAssignedUser', () => {
  const originalWorkerId = process.env.CUCUMBER_WORKER_ID;

  afterEach(() => {
    if (originalWorkerId === undefined) {
      delete process.env.CUCUMBER_WORKER_ID;
    } else {
      process.env.CUCUMBER_WORKER_ID = originalWorkerId;
    }
  });

  it('assigns a distinct account to each of the pool-sized worker IDs', () => {
    const usernames = poolUsers.map((_, index) => {
      process.env.CUCUMBER_WORKER_ID = String(index);
      return getAssignedUser().username;
    });

    expect(
      new Set(usernames).size,
      'Every concurrently-running worker must get a different account.'
    ).to.equal(poolUsers.length);
  });

  it('is deterministic - the same worker ID always gets the same account', () => {
    process.env.CUCUMBER_WORKER_ID = '1';
    const first = getAssignedUser().username;
    const second = getAssignedUser().username;

    expect(first).to.equal(second);
  });

  it('wraps around via modulo when there are more workers than pooled accounts', () => {
    process.env.CUCUMBER_WORKER_ID = String(poolUsers.length);
    expect(getAssignedUser().username).to.equal(poolUsers[0]?.username);
  });

  it('falls back to worker 0 when CUCUMBER_WORKER_ID is unset (no --parallel)', () => {
    delete process.env.CUCUMBER_WORKER_ID;
    expect(getAssignedUser().username).to.equal(poolUsers[0]?.username);
  });

  it('only ever returns checkout-capable accounts', () => {
    for (let workerId = 0; workerId < 10; workerId++) {
      process.env.CUCUMBER_WORKER_ID = String(workerId);
      expect(getAssignedUser().canCompleteCheckout).to.be.true;
    }
  });
});
