/**
 * A per-test structured logger.
 *
 * Nothing in any of the five frameworks in this repository logged anything before this. That is fine
 * right up until a test fails only in CI, only sometimes, and the report contains a screenshot of a
 * page that looks perfectly normal - at which point the one thing you want is a record of what the test
 * believed it was doing, in order, with the values it was using.
 *
 * Three design choices worth stating:
 *
 * - **Structured, not printf.** Entries carry a level, a message and an optional data object, and
 *   serialize as JSON Lines. A human can read it; `jq` can filter it.
 * - **Correlated.** Every entry carries the test's own id, so lines from three workers interleaved in a
 *   CI log can be separated again.
 * - **Buffered, not streamed.** Entries accumulate in memory and are attached to the report during
 *   teardown (see fixtures.ts). Printing to stdout from three parallel workers produces interleaved
 *   noise attached to nothing; an attachment lands on the test that produced it. By default the
 *   attachment is only made for failures - a passing test's log is cost with no reader - and
 *   `LOG_ATTACH=always` overrides that when debugging a passing-but-suspicious test.
 *
 * Deliberately free of any Playwright import: attaching is the fixture's job, which keeps this unit
 * testable with no browser and no runner.
 */
export type LogLevel = 'info' | 'warn' | 'error';

export interface LogEntry {
  readonly ts: string;
  readonly level: LogLevel;
  readonly correlationId: string;
  readonly message: string;
  readonly data?: Readonly<Record<string, unknown>>;
}

export class TestLogger {
  private readonly correlationId: string;
  private readonly entries: LogEntry[] = [];
  private readonly now: () => Date;

  /** `now` is injectable so the unit tests can assert on timestamps without freezing the clock globally. */
  constructor(correlationId: string, now: () => Date = () => new Date()) {
    this.correlationId = correlationId;
    this.now = now;
  }

  info(message: string, data?: Record<string, unknown>): void {
    this.append('info', message, data);
  }

  warn(message: string, data?: Record<string, unknown>): void {
    this.append('warn', message, data);
  }

  error(message: string, data?: Record<string, unknown>): void {
    this.append('error', message, data);
  }

  get entryCount(): number {
    return this.entries.length;
  }

  /** A defensive copy - callers (and reporters) must not be able to rewrite the record after the fact. */
  get all(): readonly LogEntry[] {
    return [...this.entries];
  }

  has(level: LogLevel): boolean {
    return this.entries.some((entry) => entry.level === level);
  }

  /** JSON Lines: one self-contained JSON object per line, appendable and greppable. */
  toJsonLines(): string {
    return this.entries.map((entry) => JSON.stringify(entry)).join('\n');
  }

  private append(level: LogLevel, message: string, data?: Record<string, unknown>): void {
    this.entries.push({
      ts: this.now().toISOString(),
      level,
      correlationId: this.correlationId,
      message,
      // Omitted entirely rather than serialized as `"data":undefined`, which is not valid JSON anyway.
      ...(data === undefined ? {} : { data })
    });
  }
}
