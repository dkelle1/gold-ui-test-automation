import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  EnvironmentError,
  ProductDefectError,
  TestBugError,
  isClassifiedError
} from '../../src/support/errors';
import { getAssignedUser } from '../../src/users/assignedUser';
import { userByUsername } from '../../src/users/userCatalog';

describe('failure taxonomy', () => {
  it('carries a kind and a stable name for each classification', () => {
    assert.equal(new ProductDefectError('x').kind, 'product-defect');
    assert.equal(new EnvironmentError('x').kind, 'environment');
    assert.equal(new TestBugError('x').kind, 'test-bug');

    // The name is what categories.json matches on, so it is asserted explicitly rather than left to
    // whatever the class happens to be called after a transpile.
    assert.equal(new ProductDefectError('x').name, 'ProductDefectError');
    assert.equal(new EnvironmentError('x').name, 'EnvironmentError');
    assert.equal(new TestBugError('x').name, 'TestBugError');
  });

  it('stays a real Error, so nothing downstream has to special-case it', () => {
    const error = new EnvironmentError('the demo site is unreachable');

    assert.ok(error instanceof Error);
    assert.equal(error.message, 'the demo site is unreachable');
    assert.ok(error.stack?.includes('EnvironmentError'));
  });

  it('preserves a cause when one is given', () => {
    const cause = new Error('ECONNREFUSED');
    const error = new EnvironmentError('could not reach saucedemo', { cause });

    assert.equal(error.cause, cause);
  });

  it('narrows with isClassifiedError and rejects unclassified throws', () => {
    assert.ok(isClassifiedError(new TestBugError('x')));
    assert.equal(isClassifiedError(new Error('x')), false);
    assert.equal(isClassifiedError('not even an error'), false);
  });
});

describe('taxonomy at real throw sites', () => {
  it('classifies an unknown account as a test bug, not an app or environment failure', () => {
    assert.throws(() => userByUsername('nope_not_a_user'), TestBugError);
  });

  it('classifies an impossible parallel index as a test bug', () => {
    assert.throws(() => getAssignedUser(-1), TestBugError);
    assert.throws(() => getAssignedUser(1.5), TestBugError);
  });
});
