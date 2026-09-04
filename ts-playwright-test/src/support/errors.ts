/**
 * A typed failure taxonomy.
 *
 * The question a red build actually has to answer is not "what threw?" but "whose problem is this?".
 * A CI failure is worth someone's morning only if it is a real product defect; the other two common
 * causes - the environment misbehaving, and the test itself being wrong - need entirely different
 * responses, and conflating all three is what trains a team to ignore its own suite.
 *
 * The sibling frameworks all classify failures *after the fact*, by regex-matching stack traces in
 * `categories.json`. That works for the errors a library happens to throw with a distinctive name
 * (`TimeoutError`, "strict mode violation"), but every error this repo's own code raises was a plain
 * `Error` and therefore unclassifiable. These three types close that gap at the throw site, where the
 * code actually knows the answer, and `categories.json` matches on their names.
 *
 * Deliberately free of any Playwright import, so the unit tests can exercise it with no browser.
 */
export type FailureKind = 'product-defect' | 'environment' | 'test-bug';

export abstract class ClassifiedError extends Error {
  readonly kind: FailureKind;

  protected constructor(kind: FailureKind, name: string, message: string, options?: ErrorOptions) {
    super(message, options);
    this.kind = kind;
    // Set explicitly rather than relying on the constructor name: a minifier or a transpile step can
    // rewrite class names, and `categories.json` matches on this string.
    this.name = name;
  }
}

/**
 * The application under test is genuinely broken. This is the only kind that should wake anyone up.
 *
 * Note that a *known* product defect asserted on purpose - saucedemo's `problem_user` checkout, say -
 * is not this: that is an expected outcome the test asserts and passes on. This is for a defect
 * discovered while doing something else.
 */
export class ProductDefectError extends ClassifiedError {
  constructor(message: string, options?: ErrorOptions) {
    super('product-defect', 'ProductDefectError', message, options);
  }
}

/**
 * Something outside both the application and the test failed: configuration, the network, the demo site
 * being rate-limited or degraded, a browser that would not launch. Retrying may help; changing the code
 * will not.
 */
export class EnvironmentError extends ClassifiedError {
  constructor(message: string, options?: ErrorOptions) {
    super('environment', 'EnvironmentError', message, options);
  }
}

/**
 * The suite asked for something impossible - an account that is not in the catalog, a worker index that
 * cannot exist. The bug is in the test code, and no amount of retrying or redeploying fixes it.
 */
export class TestBugError extends ClassifiedError {
  constructor(message: string, options?: ErrorOptions) {
    super('test-bug', 'TestBugError', message, options);
  }
}

/** Narrowing helper for reporters and hooks that want to treat classified failures differently. */
export function isClassifiedError(error: unknown): error is ClassifiedError {
  return error instanceof ClassifiedError;
}
