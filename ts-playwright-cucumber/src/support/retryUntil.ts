function sleep(ms: number): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

/**
 * Polls `poll()` until `predicate` accepts its result or `timeoutMs` elapses, then returns whatever the
 * last poll produced either way - the caller is expected to assert on that return value, which is what
 * turns "gave up waiting" into a normal, readable assertion failure instead of a bespoke timeout error.
 *
 * The direct equivalent of the C# siblings' `Assert.That(() => ..., Is.EqualTo(x).After(ms, interval))`:
 * chai has no built-in polling constraint, so this is the minimal stand-in step definitions use for
 * "eventually equals" assertions against state that a Playwright action already dispatched but that
 * hasn't necessarily been reflected in the DOM yet.
 */
export async function retryUntil<T>(
  poll: () => Promise<T>,
  predicate: (value: T) => boolean,
  { timeoutMs = 5000, intervalMs = 250 }: { timeoutMs?: number; intervalMs?: number } = {}
): Promise<T> {
  const deadline = Date.now() + timeoutMs;
  let last = await poll();

  while (!predicate(last) && Date.now() < deadline) {
    await sleep(intervalMs);
    last = await poll();
  }

  return last;
}
