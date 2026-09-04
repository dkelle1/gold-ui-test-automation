# ts-playwright-showcase

A worked example per Playwright capability, against a fixture application that ships in this folder.

**This is a catalog, not a comparison entry.** The four saucedemo frameworks in this repository all
implement the same twelve scenarios so the stacks can be compared apples-to-apples. This one has a
different job: to demonstrate, once each, the browser mechanics that saucedemo.com structurally cannot
exercise. Judge it on coverage of the capability list, not on parity with its siblings.

Implements phase 2 of [`../PLAYWRIGHT-SHOWCASE-PLAN.md`](../PLAYWRIGHT-SHOWCASE-PLAN.md).

## Why a fixture app rather than a public practice site

saucedemo.com has no iframes, no native dialogs, no file input, no download link, no second-tab flow,
and - the one that matters most - no HTTP API of its own. Its product list is baked into its bundle, so
there is no inventory request to intercept, no failure response to inject, and no empty state reachable
through the UI at all.

The usual answer is to point tests at a public practice site. This folder ships
[`fixtures-app/`](fixtures-app/) instead: ~200 lines of `node:http` and static HTML, with no
dependencies and no build step, started automatically by Playwright's own `webServer` block. That buys
three things a third-party site cannot:

- **It runs offline.** No network, no rate limits, no CI failing because someone else's demo is down.
- **It is deterministic.** The assertions below are exact, not "contains something plausible".
- **It can be given exactly the flaw a test needs** - a 500, an empty list, a slow response - without
  waiting for a backend to misbehave on cue.

`webServer` starting and stopping it is itself one of the roadmap items being shown.

## Capability index

Every roadmap item below has at least one runnable, CI-verified example. 27 tests in total.

| Roadmap item             | Spec                                                     | What the example actually shows                                                                                                                                                                            |
| ------------------------ | -------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| **Frames**               | [`frames.spec.ts`](tests/mechanics/frames.spec.ts)       | that a `Locator` does _not_ cross a frame boundary; `frameLocator()` chaining through nested frames; driving a form inside the inner one                                                                   |
| **Alerts**               | [`dialogs.spec.ts`](tests/mechanics/dialogs.spec.ts)     | that an unhandled dialog is **auto-dismissed**, not blocking; reading `type()`/`message()`/`defaultValue()`; accept, dismiss, and answering a prompt with text                                             |
| **Uploads**              | [`uploads.spec.ts`](tests/mechanics/uploads.spec.ts)     | in-memory buffer upload with nothing committed to disk, and the bytes read back through the page; upload from a path; multiple files; clearing a selection                                                 |
| **Downloads**            | [`downloads.spec.ts`](tests/mechanics/downloads.spec.ts) | starting the wait _before_ the click; `suggestedFilename()`; reading the bytes via `createReadStream()`; `saveAs()` into `testInfo.outputPath()` and attaching it to the report; a blob-generated download |
| **Multiple Tabs**        | [`windows.spec.ts`](tests/mechanics/windows.spec.ts)     | `target=_blank` via `context.waitForEvent('page')`; `window.open` via the narrower `page.waitForEvent('popup')`; that the opener stays live with no "switch to window" step                                |
| **Network Interception** | [`network.spec.ts`](tests/mechanics/network.spec.ts)     | observing and asserting on the real request/response; aborting a route                                                                                                                                     |
| **API Mocking**          | [`network.spec.ts`](tests/mechanics/network.spec.ts)     | forcing empty / 500 / slow states that are unreachable through the UI; rewriting a real response via `route.fetch()` so the mock cannot drift from the API's actual shape                                  |
| **Mobile Emulation**     | [`mobile.spec.ts`](tests/mechanics/mobile.spec.ts)       | what emulation changes (viewport, DPR, UA, touch) asserted individually; a CSS-driven layout swap; `tap()` as real touch input                                                                             |
| **webServer**            | [`playwright.config.ts`](playwright.config.ts)           | the whole suite running offline with no manual server step                                                                                                                                                 |

The mocking tests are the highest-value entries here. They add negative-path UI coverage that **no
framework in this repository currently has**, because the error and empty states of a catalog simply
cannot be produced by clicking around a site whose data is hardcoded.

## Running

```bash
npm ci
npx playwright install --with-deps chromium

npm test                   # chromium
npm run test:all-browsers  # chromium + firefox + webkit
npm run test:ui            # UI mode
npm run report             # Playwright HTML report from the last run
```

Playwright starts the fixture app itself. To poke at it by hand:

```bash
npm run fixtures-app       # http://127.0.0.1:8100/
```

A server already running on that port is reused locally (and never in CI, where reusing a stale process
would hide a broken start).

## The fixture app

```
fixtures-app/
  server.mjs         node:http - static files, GET /api/products, GET /download/report.csv
  public/
    index.html       links to every fixture page
    frames.html      + frame-outer.html + frame-inner.html (nested, with a form)
    dialogs.html     alert / confirm / prompt
    uploads.html     single + multiple inputs, reads the first file back so bytes can be asserted
    downloads.html   an attachment link and a blob-generated download
    windows.html     + popup.html (target=_blank and window.open)
    catalog.html     fetches /api/products - the thing worth intercepting
    responsive.html  reports viewport, touch, DPR and UA; swaps nav by media query
```

Static serving is path-contained: a request resolving outside `public/` is refused. A fixture app is
still a web server, and `../` in a URL should not be able to read the repository it lives in.

## Notable findings

Things confirmed by hitting them here, not assumed - kept in the same spirit as the sibling frameworks'
_Known limitations_ registers:

- **`test.use({ ...devices['Pixel 7'] })` fails inside a `describe`.** Playwright rejects it with
  "Cannot use({ defaultBrowserType }) in a describe group, because it forces a new worker": device
  descriptors carry `defaultBrowserType` alongside the emulation options, and that one is worker-scoped.
  Naming the emulation fields explicitly is the fix, and doubles as documentation of what emulation
  changes. See [`mobile.spec.ts`](tests/mechanics/mobile.spec.ts).
- **A descriptor's `screen` is not a valid `use` option** in Playwright 1.56 - it exists at runtime but
  is neither on the `DeviceDescriptor` type nor accepted by `use`, so it type-errors while the runtime
  quietly ignores it. Spreading the descriptor hides this; listing fields surfaces it.
- **`import.meta.url` in a spec file breaks the transpile.** Playwright compiles spec files to CommonJS
  unless something forces ESM, and `import.meta` is exactly such a construct - after which the file's own
  transpiled `require` calls fail with "require is not defined in ES module scope". Use `__dirname`.
- **`allure-playwright@3.12.0` raised its peer floor to `@playwright/test >= 1.62.0`.** This repository
  pins Playwright 1.56.1 across every framework, so a `^3.10.2` range silently resolves to something
  incompatible and `npm install` fails outright. Both Playwright frameworks now pin `~3.11.0`.
- **`isMobile` is unsupported in Firefox**, so the emulation describe skips there rather than failing
  confusingly in the nightly matrix.

## Known limitations

- **Only Chromium has actually run this suite.** All 27 tests pass there. The Firefox and WebKit
  projects are configured and installed by CI, but nothing has run green on them yet - the first nightly
  matrix run is what will confirm them. The one known non-Chromium difference is already handled
  (`isMobile` in Firefox).
- **Emulation is not device testing.** `mobile.spec.ts` says so in its own header, and it bears
  repeating: viewport, DPR, UA and touch are emulated; the engine, GPU, memory pressure and network
  stack are not. This catches responsive-layout regressions cheaply. It is not mobile coverage.
- **The fixture app is a fixture, not a product.** It has no accessibility work, no error handling worth
  the name, and its markup is shaped by what the tests need to demonstrate. It should not be read as an
  example of how to build a web app.
- **A catalog rots faster than a comparison suite.** Nothing forces these examples to stay current the
  way twelve shared scenarios force the saucedemo frameworks to. Every example here runs in CI for
  exactly that reason - if one stops being true of the current Playwright release, the build says so.

## Adding an example

1. If it needs a DOM feature the fixture app lacks, add a page under `fixtures-app/public/` and, if it
   needs a server behaviour, a route in `server.mjs`. Keep both dependency-free.
2. Add a spec under `tests/mechanics/` whose file header states **which roadmap item it covers and what
   the non-obvious part is** - the headers are the teaching material, and are why this folder exists.
3. Add a row to the capability index above.
4. Run `npm run typecheck && npm run lint && npm run format` - CI runs all three.

## License

[MIT](../LICENSE)
