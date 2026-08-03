// This file's default export IS the flat configuration object directly - NOT wrapped in a nested
// `{ default: {...} }` key. That nesting is the natural instinct coming from CJS examples elsewhere
// (`module.exports = { default: {...}, ci: {...} }`, where multiple sibling keys are real named
// profiles) but it is wrong here: verified directly against @cucumber/cucumber@13.2.0's own
// `fromFile()` source (node_modules/@cucumber/cucumber/lib/configuration/from_file.js) that for an
// ESM config file, `import()` yields a module NAMESPACE object whose only own key is always `default`
// - so a nested `default: {...}` key inside that produces a DOUBLY-wrapped object that never gets
// unwrapped, and every path in this file (features, step bindings, hooks) silently resolves to nothing
// instead of throwing. Confirmed by actually running `cucumber-js --dry-run` with `DEBUG=cucumber` and
// watching "Found support files to load via `import` based on configuration" come back empty with the
// nested shape, and populated with this flat one.
//
// There is deliberately no `loader` field here. `loader: ['tsx/esm']` looks like the obvious way to get
// tsx to transpile the `.ts` paths below, and @cucumber/cucumber does call `node:module`'s `register()`
// with whatever specifier you put there - but tsx's own loader entry point requires being registered
// through its own `tsx/esm/api` wrapper (which supplies a required MessageChannel `data` payload);
// registered as a bare specifier the way Cucumber does it, tsx throws "tsx must be loaded with --import
// instead of --loader" at the first `.ts` file it tries to load. Every place this suite actually runs
// (package.json scripts, scripts/run-tests.sh|ps1, the GitHub Actions workflow, the Jenkinsfile) invokes
// Node with `--import tsx` instead of a bare `cucumber-js`/`npx cucumber-js`.
//
// That said, `--import tsx` only fixes the COORDINATOR thread - confirmed directly (a worker thread
// logs the SAME `process.execArgv` the coordinator was launched with, e.g. `['--import', 'tsx']`, but a
// `.js`-specifier import that only resolves via tsx's sibling-`.ts` fallback still throws
// `ERR_MODULE_NOT_FOUND` inside a `--parallel` worker). `--parallel` workers are separate `worker_threads`
// (see browserManager.ts), and Node does not re-run a `--import`-registered loader's hooks inside a new
// worker's own module scope, even though the worker's `execArgv` shows the flag - it falls back to
// Node's own native TypeScript support (stable, unflagged, on by default in current Node 22.x) instead,
// which only strips ERASABLE syntax and has no `.js`-to-`.ts` fallback resolution at all. Rather than
// fight that boundary, this project's own source is written to need neither: every internal relative
// import uses the real, literal `.ts` extension (paired with `allowImportingTsExtensions` in
// tsconfig.json), and constructor-parameter-property shorthand is banned by the
// `@typescript-eslint/parameter-properties` lint rule (it needs real code generation, not erasure - see
// basePage.ts's own comment for the exact error). With both of those true, native stripping alone is
// sufficient everywhere - the coordinator's `--import tsx` is a version-independent safety net for
// older Node 22.x patches that predate native stripping being on by default, not a strict requirement
// on the version this project actually targets.
export default {
  paths: ['features/**/*.feature'],
  import: ['src/hooks.ts', 'src/world.ts', 'src/steps/**/*.ts'],
  // Must be a literal, not `MAX_PARALLEL_WORKERS` from src/config/parallelSettings.ts: Cucumber reads
  // this file before any loader (including the `--import tsx` above) has transpiled a single `.ts` file,
  // so a `.ts` import here would fail. Keep this in sync with that file by hand - see its own comment.
  parallel: 3,
  format: ['progress-bar', 'allure-cucumberjs/reporter'],
  // A step with no matching definition must fail the run loudly, never pass silently - Cucumber.js
  // already defaults `strict` to true, so this just makes that choice explicit rather than relying on
  // an unstated default (matching the Reqnroll siblings' own explicit `MissingOrPendingStepsOutcome`).
  strict: true
};
