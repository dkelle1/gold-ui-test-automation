/**
 * Single source of truth for how many scenarios run concurrently. Must stay in sync with two things
 * that can't simply import this constant:
 *  - `cucumber.js`'s `parallel` option, which cucumber-js reads before any loader (including tsx, the
 *    one that would let it import a .ts file) is registered - it has to be a plain literal there.
 *  - `userCatalog.ts`'s `poolUsers`, which needs at least this many distinct checkout-capable accounts
 *    (see `assignedUser.ts`).
 */
export const MAX_PARALLEL_WORKERS = 3;
