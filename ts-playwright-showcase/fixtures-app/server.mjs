/**
 * The fixture application: a deliberately tiny web app whose only purpose is to have the DOM features
 * saucedemo.com does not have.
 *
 * saucedemo has no iframes, no native dialogs, no file input, no download link, no second-tab flow and
 * no HTTP API of its own - so the whole "core concepts" and "advanced" half of the Playwright roadmap
 * is unreachable against it. Rather than pick a third-party practice site and inherit its uptime,
 * markup drift and rate limits, this app ships in the repo and is started by Playwright's own
 * `webServer` block (see ../playwright.config.ts), which is itself one of the features being shown.
 *
 * No dependencies and no build step on purpose: node:http plus static files. Everything a test asserts
 * on is deterministic and offline.
 */
import { createServer } from 'node:http';
import { readFile } from 'node:fs/promises';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const publicDir = path.join(path.dirname(fileURLToPath(import.meta.url)), 'public');
const port = Number(process.env.FIXTURES_PORT || 8100);

/** The catalog that catalog.html fetches. Route-interception tests replace, break and delay this response. */
const PRODUCTS = [
  { id: 1, name: 'Quantum Widget', price: 24.5 },
  { id: 2, name: 'Recursive Sprocket', price: 12 },
  { id: 3, name: 'Idempotent Flange', price: 87.25 }
];

const CSV_REPORT = ['id,name,price', ...PRODUCTS.map((p) => `${p.id},${p.name},${p.price}`)].join('\n');

const CONTENT_TYPES = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.svg', 'image/svg+xml']
]);

async function serveStatic(urlPath, res) {
  const relative = urlPath === '/' ? 'index.html' : urlPath.replace(/^\/+/, '');

  // Contain every request inside public/ - a fixture app is still a web server, and `../` in a URL
  // should not be able to read the repository it lives in.
  const resolved = path.resolve(publicDir, relative);
  if (resolved !== publicDir && !resolved.startsWith(publicDir + path.sep)) {
    res.writeHead(403).end('forbidden');
    return;
  }

  try {
    const body = await readFile(resolved);
    const type = CONTENT_TYPES.get(path.extname(resolved)) ?? 'application/octet-stream';
    res.writeHead(200, { 'content-type': type }).end(body);
  } catch {
    res.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' }).end('not found');
  }
}

const server = createServer(async (req, res) => {
  const urlPath = (req.url ?? '/').split('?')[0];

  if (urlPath === '/api/products') {
    res.writeHead(200, { 'content-type': 'application/json; charset=utf-8' }).end(JSON.stringify(PRODUCTS));
    return;
  }

  if (urlPath === '/download/report.csv') {
    // Content-Disposition: attachment is what makes the browser treat this as a download rather than a
    // navigation - without it there is no `download` event for a test to wait for.
    res
      .writeHead(200, {
        'content-type': 'text/csv; charset=utf-8',
        'content-disposition': 'attachment; filename="report.csv"'
      })
      .end(CSV_REPORT);
    return;
  }

  await serveStatic(urlPath, res);
});

server.listen(port, '127.0.0.1', () => {
  // Playwright's webServer block waits for the URL to answer, not for this line - but it is what makes
  // a manually started server (npm run fixtures-app) tell you where it is.
  console.log(`fixtures-app listening on http://127.0.0.1:${port}/`);
});
