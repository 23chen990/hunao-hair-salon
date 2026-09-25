import assert from 'node:assert/strict';
import { createReadStream } from 'node:fs';
import { access, mkdir, readFile, stat, writeFile } from 'node:fs/promises';
import { createServer } from 'node:http';
import { dirname, extname, join, normalize, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = dirname(dirname(fileURLToPath(import.meta.url)));
const dist = join(root, 'dist');
const artifacts = join(root, 'artifacts');
const mime = new Map([
  ['.html', 'text/html; charset=utf-8'],
  ['.css', 'text/css; charset=utf-8'],
  ['.js', 'text/javascript; charset=utf-8'],
  ['.mjs', 'text/javascript; charset=utf-8'],
  ['.json', 'application/json; charset=utf-8'],
]);

class EnvironmentError extends Error {}

async function importAutomation() {
  const candidates = [
    process.env.PLAYWRIGHT_MODULE,
    'playwright',
    'playwright-core',
    join(root, '..', 'node_modules', 'playwright', 'index.mjs'),
    join(root, '..', '..', 'node_modules', 'playwright', 'index.mjs'),
  ].filter(Boolean);
  const failures = [];
  for (const candidate of candidates) {
    try {
      const specifier = candidate.startsWith('/') ? pathToFileURL(candidate).href : candidate;
      const loaded = await import(specifier);
      if (loaded.chromium) return loaded;
    } catch (error) {
      failures.push(`${candidate}: ${error.code ?? error.message}`);
    }
  }
  throw new EnvironmentError(`Playwright runtime unavailable (${failures.join(' | ')})`);
}

function startServer() {
  return new Promise((resolve, reject) => {
    const server = createServer(async (request, response) => {
      try {
        const requestUrl = new URL(request.url ?? '/', 'http://127.0.0.1');
        const pathname = requestUrl.pathname === '/' ? '/index.html' : requestUrl.pathname;
        const decoded = decodeURIComponent(pathname);
        const relativePath = normalize(decoded).replace(/^[/\\]+/, '');
        const file = join(dist, relativePath);
        if (file !== dist && !file.startsWith(`${dist}${sep}`)) {
          response.writeHead(403).end('forbidden');
          return;
        }
        const info = await stat(file);
        if (!info.isFile()) throw new Error('not a file');
        response.writeHead(200, {
          'content-type': mime.get(extname(file)) ?? 'application/octet-stream',
          'cache-control': 'no-store',
        });
        createReadStream(file).pipe(response);
      } catch {
        response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' }).end('not found');
      }
    });
    server.once('error', (error) => reject(new EnvironmentError(`local server failed: ${error.message}`)));
    server.listen(0, '127.0.0.1', () => {
      const address = server.address();
      resolve({ server, origin: `http://127.0.0.1:${address.port}` });
    });
  });
}

async function stopServer(server) {
  if (!server) return;
  await new Promise((resolve) => server.close(resolve));
}

async function inspectLayout(page) {
  return page.evaluate(() => {
    const canvas = document.querySelector('#bridge').getBoundingClientRect();
    const restart = document.querySelector('#restart').getBoundingClientRect();
    const next = document.querySelector('#next-level').getBoundingClientRect();
    const within = (rect) =>
      rect.left >= -0.5 && rect.top >= -0.5 && rect.right <= innerWidth + 0.5 && rect.bottom <= innerHeight + 0.5;
    return {
      viewport: { width: innerWidth, height: innerHeight },
      document: {
        scrollWidth: document.documentElement.scrollWidth,
        scrollHeight: document.documentElement.scrollHeight,
      },
      canvas: { x: canvas.x, y: canvas.y, width: canvas.width, height: canvas.height, within: within(canvas) },
      restart: { x: restart.x, y: restart.y, width: restart.width, height: restart.height, within: within(restart) },
      next: { x: next.x, y: next.y, width: next.width, height: next.height, within: within(next) },
      noCrop:
        document.documentElement.scrollWidth <= innerWidth &&
        document.documentElement.scrollHeight <= innerHeight &&
        within(canvas) && within(restart) && within(next),
    };
  });
}

async function exerciseMouse(page, canvas) {
  await page.mouse.move(canvas.x + canvas.width * 0.5, canvas.y + canvas.height * 0.66);
  await page.mouse.down();
  await page.waitForTimeout(1_000);
  const during = await page.evaluate(() => window.__bridgeDebug.snapshot());
  await page.mouse.up();
  await page.waitForTimeout(120);
  const after = await page.evaluate(() => window.__bridgeDebug.snapshot());
  assert.ok(during.pressure > 0, 'mouse hold must inflate the membrane');
  assert.equal(during.held, true, 'mouse press must set held');
  assert.equal(after.held, false, 'mouse release must clear held');
  assert.deepEqual(after.inputLog.map((event) => event.held), [true, false]);
  return { during, after };
}

async function exerciseTouch(context, page, canvas) {
  const session = await context.newCDPSession(page);
  const x = canvas.x + canvas.width * 0.5;
  const y = canvas.y + canvas.height * 0.66;
  await session.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [{ x, y, radiusX: 8, radiusY: 8, force: 0.65, id: 1 }],
  });
  await page.waitForTimeout(1_000);
  const during = await page.evaluate(() => window.__bridgeDebug.snapshot());
  await session.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
  await page.waitForTimeout(120);
  const after = await page.evaluate(() => window.__bridgeDebug.snapshot());
  assert.ok(during.pressure > 0, 'native touch hold must inflate the membrane');
  assert.equal(during.held, true, 'native touch start must set held');
  assert.equal(after.held, false, 'native touch end must clear held');
  assert.deepEqual(after.inputLog.map((event) => event.held), [true, false]);
  return { during, after };
}

async function runProfile(browser, origin, profile) {
  const context = await browser.newContext({
    viewport: { width: profile.width, height: profile.height },
    deviceScaleFactor: 1,
    isMobile: profile.touch,
    hasTouch: profile.touch,
  });
  const page = await context.newPage();
  const consoleErrors = [];
  const pageErrors = [];
  const externalRequests = [];
  const failedRequests = [];
  page.on('console', (message) => {
    if (message.type() === 'error') consoleErrors.push(message.text());
  });
  page.on('pageerror', (error) => pageErrors.push(error.message));
  page.on('request', (request) => {
    const url = new URL(request.url());
    if (url.origin !== origin) externalRequests.push(request.url());
  });
  page.on('requestfailed', (request) => failedRequests.push(`${request.url()}: ${request.failure()?.errorText}`));

  try {
    await page.goto(`${origin}/?seed=${profile.seed}`, { waitUntil: 'networkidle', timeout: 15_000 });
  } catch (error) {
    await context.close();
    throw new EnvironmentError(`navigation failed: ${error.message}`);
  }
  await page.waitForFunction(() => window.__bridgeDebug?.ready === true, null, { timeout: 5_000 });
  const policy = await page.evaluate(() => ({
    adsEnabled: window.__bridgeDebug.adsEnabled,
    networkAllowed: window.__bridgeDebug.networkAllowed,
    networkRequests: window.__bridgeDebug.networkRequests,
  }));
  assert.deepEqual(policy, { adsEnabled: false, networkAllowed: false, networkRequests: 0 });

  const layout = await inspectLayout(page);
    assert.equal(layout.noCrop, true, `${profile.name} must not crop or scroll`);
    assert.ok(layout.canvas.width >= 44 && layout.canvas.height >= 44, `${profile.name} canvas target must be >=44px`);
    assert.ok(layout.restart.width >= 44 && layout.restart.height >= 44, `${profile.name} restart target must be >=44px`);
    assert.ok(layout.next.width >= 44 && layout.next.height >= 44, `${profile.name} next-level target must be >=44px`);

  const interaction = profile.touch
    ? await exerciseTouch(context, page, layout.canvas)
    : await exerciseMouse(page, layout.canvas);
  const screenshot = join(artifacts, `smoke-${profile.name}.png`);
  await page.screenshot({ path: screenshot, fullPage: false });

  await page.locator('#restart').click();
  await page.waitForTimeout(80);
  const restarted = await page.evaluate(() => window.__bridgeDebug.snapshot());
  assert.equal(restarted.status, 'playing');
  assert.equal(restarted.held, false);
  assert.equal(restarted.pressure, 0);
  assert.ok(restarted.tick <= 8, 'free restart must return to the opening ticks');

  await context.close();
  assert.equal(consoleErrors.length, 0, `${profile.name} console errors: ${consoleErrors.join(' | ')}`);
  assert.equal(pageErrors.length, 0, `${profile.name} page errors: ${pageErrors.join(' | ')}`);
  assert.equal(externalRequests.length, 0, `${profile.name} external requests: ${externalRequests.join(' | ')}`);
  assert.equal(failedRequests.length, 0, `${profile.name} failed requests: ${failedRequests.join(' | ')}`);

  return {
    name: profile.name,
    input: profile.touch ? 'native-cdp-touch' : 'mouse',
    viewport: { width: profile.width, height: profile.height },
    layout,
    controlsAtLeast44: { numerator: 3, denominator: 3 },
    noCrop: { numerator: Number(layout.noCrop), denominator: 1 },
    interaction: {
      holdRegistered: { numerator: Number(interaction.during.held && interaction.during.pressure > 0), denominator: 1 },
      releaseRegistered: { numerator: Number(!interaction.after.held), denominator: 1 },
      inputTransitions: interaction.after.inputLog,
    },
    freeRestart: { numerator: Number(restarted.status === 'playing' && restarted.pressure === 0), denominator: 1 },
    consoleErrors,
    pageErrors,
    externalRequests,
    failedRequests,
    screenshot,
  };
}

async function runAttempt(attempt) {
  await access(join(dist, 'index.html'));
  const automation = await importAutomation();
  const { server, origin } = await startServer();
  let browser;
  try {
    try {
      browser = await automation.chromium.launch({ headless: true });
    } catch (error) {
      throw new EnvironmentError(`browser launch failed: ${error.message}`);
    }
    const profiles = [];
    profiles.push(await runProfile(browser, origin, { name: '390x844-mouse', width: 390, height: 844, touch: false, seed: 41 }));
    profiles.push(await runProfile(browser, origin, { name: '430x932-touch', width: 430, height: 932, touch: true, seed: 42 }));
    return {
      schemaVersion: 1,
      prototype: 'breathing-bridge',
      status: 'PASS',
      attempt,
      originKind: 'ephemeral-loopback-only',
      browser: await browser.version(),
      profiles,
      totals: {
        viewportProfiles: { numerator: profiles.length, denominator: 2 },
        controlsAtLeast44: { numerator: profiles.reduce((sum, item) => sum + item.controlsAtLeast44.numerator, 0), denominator: 6 },
        noCrop: { numerator: profiles.reduce((sum, item) => sum + item.noCrop.numerator, 0), denominator: 2 },
        consoleErrorFree: { numerator: profiles.filter((item) => item.consoleErrors.length === 0).length, denominator: 2 },
        externalRequestFree: { numerator: profiles.filter((item) => item.externalRequests.length === 0).length, denominator: 2 },
        freeRestart: { numerator: profiles.reduce((sum, item) => sum + item.freeRestart.numerator, 0), denominator: 2 },
      },
    };
  } finally {
    await browser?.close().catch(() => {});
    await stopServer(server);
  }
}

await mkdir(artifacts, { recursive: true });
let result;
let environmentFailure;
for (let attempt = 1; attempt <= 2; attempt += 1) {
  try {
    result = await runAttempt(attempt);
    break;
  } catch (error) {
    if (!(error instanceof EnvironmentError)) {
      result = {
        schemaVersion: 1,
        prototype: 'breathing-bridge',
        status: 'FAIL',
        attempt,
        error: error.stack ?? error.message,
      };
      break;
    }
    environmentFailure = error;
  }
}

if (!result) {
  result = {
    schemaVersion: 1,
    prototype: 'breathing-bridge',
    status: 'ENVIRONMENT_UNVERIFIED',
    attempts: 2,
    error: environmentFailure?.stack ?? environmentFailure?.message ?? 'unknown environment failure',
  };
}

const output = join(artifacts, 'smoke-audit.json');
await writeFile(output, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
console.log(JSON.stringify({ output, ...result }, null, 2));
if (result.status === 'FAIL') process.exitCode = 1;
if (result.status === 'ENVIRONMENT_UNVERIFIED') process.exitCode = 2;
