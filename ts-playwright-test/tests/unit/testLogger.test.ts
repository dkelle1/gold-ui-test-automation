import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { TestLogger } from '../../src/support/testLogger';

/** A fixed clock, so timestamps can be asserted rather than merely tolerated. */
const frozenClock = () => new Date('2026-01-01T12:00:00.000Z');

describe('TestLogger', () => {
  it('records level, message, timestamp and correlation id', () => {
    const log = new TestLogger('test-id-1', frozenClock);

    log.info('Signing in', { username: 'standard_user' });

    assert.deepEqual(log.all, [
      {
        ts: '2026-01-01T12:00:00.000Z',
        level: 'info',
        correlationId: 'test-id-1',
        message: 'Signing in',
        data: { username: 'standard_user' }
      }
    ]);
  });

  it('omits the data key entirely when there is none', () => {
    const log = new TestLogger('test-id-1', frozenClock);

    log.info('No detail here');

    const [entry] = log.all;
    assert.ok(entry);
    assert.equal('data' in entry, false, 'an absent data object must not serialize as undefined');
  });

  it('keeps entries in the order they were written', () => {
    const log = new TestLogger('test-id-1', frozenClock);

    log.info('first');
    log.warn('second');
    log.error('third');

    assert.deepEqual(
      log.all.map((e) => [e.level, e.message]),
      [
        ['info', 'first'],
        ['warn', 'second'],
        ['error', 'third']
      ]
    );
  });

  it('reports whether a level was used, so a fixture can decide what to attach', () => {
    const log = new TestLogger('test-id-1', frozenClock);
    log.info('nothing wrong');

    assert.equal(log.has('info'), true);
    assert.equal(log.has('warn'), false);
    assert.equal(log.has('error'), false);
  });

  it('serializes as JSON Lines - one parseable object per line', () => {
    const log = new TestLogger('test-id-1', frozenClock);
    log.info('first');
    log.warn('second', { attempt: 2 });

    const lines = log.toJsonLines().split('\n');

    assert.equal(lines.length, 2);
    for (const line of lines) {
      assert.doesNotThrow(() => JSON.parse(line));
    }
    assert.equal(JSON.parse(lines[1] ?? '{}').data.attempt, 2);
  });

  it('is empty, and serializes to an empty string, before anything is logged', () => {
    const log = new TestLogger('test-id-1', frozenClock);

    assert.equal(log.entryCount, 0);
    assert.equal(log.toJsonLines(), '');
  });

  it('hands out a copy, so a reporter cannot rewrite the record after the fact', () => {
    const log = new TestLogger('test-id-1', frozenClock);
    log.info('original');

    // `all` is typed `readonly`, so this cast is the test deliberately doing what the type system
    // forbids: the guarantee under test is the runtime copy, not the compile-time modifier, and only a
    // caller that ignores the type can prove it.
    const snapshot = log.all as unknown as { message: string }[];
    snapshot.push({ message: 'injected' });

    assert.equal(log.entryCount, 1);
    assert.equal(log.all[0]?.message, 'original');
  });
});
