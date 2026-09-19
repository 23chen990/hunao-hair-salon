import assert from 'node:assert/strict';
import { spawnSync } from 'node:child_process';
import { createServer } from 'node:http';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { extname, join, normalize, resolve } from 'node:path';

const ROOT = resolve(new URL('..', import.meta.url).pathname);
const DIST = join(ROOT, 'dist');
const ARTIFACTS = join(ROOT, 'artifacts');
const PLAYWRIGHT_PATH = '/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright-core/index.mjs';
const BROWSER_CANDIDATES = [
  '/Users/kker/.cache/puppeteer/chrome-headless-shell/mac_arm-147.0.7727.57/chrome-headless-shell-mac-arm64/chrome-headless-shell',
  '/Users/kker/Library/Caches/ms-playwright/chromium_headless_shell-1234/chrome-headless-shell-mac-arm64/chrome-headless-shell',
  '/Users/kker/Library/Caches/ms-playwright/chromium_headless_shell-1223/chrome-headless-shell-mac-arm64/chrome-headless-shell',
];

function build() {
  const result = spawnSync(process.execPath, ['tools/build.mjs'], { cwd: ROOT, encoding: 'utf8' });
  assert.equal(result.status, 0, `build failed\n${result.stdout}\n${result.stderr}`);
}

function serve() {
  const types = new Map([
    ['.html', 'text/html; charset=utf-8'],
    ['.css', 'text/css; charset=utf-8'],
    ['.mjs', 'text/javascript; charset=utf-8'],
    ['.json', 'application/json; charset=utf-8'],
  ]);
  const server = createServer((request, response) => {
    const requested = request.url === '/' ? '/index.html' : request.url.split('?')[0];
    const file = normalize(join(DIST, requested));
    if (!file.startsWith(`${DIST}/`) || !existsSync(file)) {
      response.writeHead(404).end('not found');
      return;
    }
    response.writeHead(200, { 'content-type': types.get(extname(file)) ?? 'application/octet-stream' });
    response.end(readFileSync(file));
  });
  return new Promise((resolveServer) => {
    server.listen(0, '127.0.0.1', () => resolveServer(server));
  });
}

async function tap(page, cdp, selector, pointerType) {
  const box = await page.locator(selector).boundingBox();
  assert.ok(box, `${selector} has no box`);
  const x = box.x + box.width / 2;
  const y = box.y + box.height / 2;
  if (pointerType === 'mouse') {
    await page.mouse.click(x, y);
    return;
  }
  await cdp.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [{ x, y, radiusX: 4, radiusY: 4, force: 1, id: 1 }],
  });
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

async function selectFace(page, cdp, face, pointerType) {
  const current = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot().selectedFace);
  const turns = (face - current + 8) % 8;
  for (let index = 0; index < turns; index += 1) {
    await tap(page, cdp, '#face-next', pointerType);
  }
}

async function dragAction(page, cdp, action, pointerType) {
  const regions = await page.evaluate(() => window.__ROLLER_AUDIT__.regions());
  const start = action.direction === 1 ? regions.leftHandle : regions.rightHandle;
  const end = { x: start.x + action.direction * action.distance * regions.cell, y: start.y };
  if (pointerType === 'mouse') {
    await page.mouse.move(start.x, start.y);
    await page.mouse.down();
    await page.mouse.move(end.x, end.y, { steps: 10 });
    await page.mouse.up();
    return;
  }
  await cdp.send('Input.dispatchTouchEvent', {
    type: 'touchStart',
    touchPoints: [{ x: start.x, y: start.y, radiusX: 5, radiusY: 5, force: 1, id: 7 }],
  });
  for (let step = 1; step <= 10; step += 1) {
    await cdp.send('Input.dispatchTouchEvent', {
      type: 'touchMove',
      touchPoints: [{
        x: start.x + ((end.x - start.x) * step) / 10,
        y: start.y,
        radiusX: 5,
        radiusY: 5,
        force: 1,
        id: 7,
      }],
    });
  }
  await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] });
}

async function runViewport(browser, baseUrl, viewport, pointerType) {
  const context = await browser.newContext({
    viewport,
    hasTouch: pointerType === 'touch',
    isMobile: pointerType === 'touch',
    deviceScaleFactor: 1,
  });
  const page = await context.newPage();
  const cdp = await context.newCDPSession(page);
  const errors = [];
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  page.on('pageerror', (error) => errors.push(`pageerror: ${error.message}`));
  await page.goto(baseUrl, { waitUntil: 'networkidle' });
  await page.waitForFunction(() => window.__ROLLER_AUDIT__?.ready === true);

  const controls = await page.evaluate(() => window.__ROLLER_AUDIT__.controls());
  for (const control of controls) {
    assert.ok(control.width >= 44 && control.height >= 44, `${control.id} is ${control.width}×${control.height}`);
  }

  const initial = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  const plan = await page.evaluate(() => window.__ROLLER_AUDIT__.smokePlan());
  await selectFace(page, cdp, plan.wrong.face, pointerType);
  await dragAction(page, cdp, plan.wrong, pointerType);
  const wrong = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(wrong.moves, 1);
  assert.ok(wrong.error.wrong > 0, 'wrong roll must create visible wrong ink');
  assert.ok(wrong.pointerTypes.includes(pointerType), `${pointerType} PointerEvent was not logged`);

  await tap(page, cdp, '#undo', pointerType);
  const undone = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(undone.stateHash, initial.stateHash, 'undo hash mismatch');

  for (const action of plan.solution) {
    await selectFace(page, cdp, action.face, pointerType);
    await dragAction(page, cdp, action, pointerType);
  }
  const solved = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(solved.error.exact, true, 'actual gestures did not solve target');

  await tap(page, cdp, '#restart', pointerType);
  const restarted = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(restarted.moves, 0);
  assert.equal(restarted.inkBits, 0);

  await tap(page, cdp, '#next-seed', pointerType);
  const next = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(next.seed, initial.seed + 1);

  await tap(page, cdp, '#hint', pointerType);
  const hinted = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(hinted.contactArcVisible, true);

  const beforeAd = hinted.stateHash;
  await tap(page, cdp, '#simulate-ad', pointerType);
  const paused = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(paused.paused, true);
  const pausedPlan = await page.evaluate(() => window.__ROLLER_AUDIT__.smokePlan());
  await selectFace(page, cdp, pausedPlan.wrong.face, pointerType);
  await dragAction(page, cdp, pausedPlan.wrong, pointerType);
  await page.waitForTimeout(450);
  const resumed = await page.evaluate(() => window.__ROLLER_AUDIT__.snapshot());
  assert.equal(resumed.paused, false);
  assert.equal(resumed.stateHash, beforeAd, 'simulated ad changed playable state');

  mkdirSync(ARTIFACTS, { recursive: true });
  const name = `${viewport.width}x${viewport.height}-${pointerType}`;
  await page.screenshot({ path: join(ARTIFACTS, `smoke-${name}.png`), fullPage: true });
  assert.deepEqual(errors, []);
  const result = {
    viewport,
    pointerType,
    controls,
    movesToSolve: plan.solution.length,
    wrongCellsObserved: wrong.error.wrong,
    finalSeed: resumed.seed,
    consoleErrors: errors,
  };
  await context.close();
  return result;
}

build();
assert.ok(existsSync(PLAYWRIGHT_PATH), 'local playwright-core is unavailable');
const browserPath = BROWSER_CANDIDATES.find(existsSync);
assert.ok(browserPath, 'local Chromium is unavailable');
const { chromium } = await import(PLAYWRIGHT_PATH);
const server = await serve();
const address = server.address();
const baseUrl = `http://127.0.0.1:${address.port}/`;
let browser;
try {
  browser = await chromium.launch({ executablePath: browserPath, headless: true });
  const results = [];
  results.push(await runViewport(browser, baseUrl, { width: 390, height: 844 }, 'mouse'));
  results.push(await runViewport(browser, baseUrl, { width: 430, height: 932 }, 'touch'));
  const report = {
    status: 'PASS',
    browserPath,
    baseUrl: 'loopback ephemeral port',
    externalRequests: 0,
    results,
  };
  writeFileSync(join(ARTIFACTS, 'browser-smoke.json'), `${JSON.stringify(report, null, 2)}\n`);
  process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
} finally {
  await browser?.close();
  await new Promise((resolveClose) => server.close(resolveClose));
}
