/**
 * Single source of truth for how many tests run concurrently.
 *
 * Unlike the Cucumber sibling - where the same number had to be hand-duplicated as a plain literal in
 * `cucumber.js`, because Cucumber reads that file before any TypeScript loader exists - playwright.config.ts
 * is itself transpiled by Playwright and can simply import this constant. There is exactly one copy of
 * this number in this framework, and nothing to keep in sync by hand.
 *
 * `userCatalog.ts`'s `poolUsers` must still hold at least this many checkout-capable accounts;
 * `assignedUser.ts` asserts that at import time.
 */
export const MAX_PARALLEL_WORKERS = 3;
