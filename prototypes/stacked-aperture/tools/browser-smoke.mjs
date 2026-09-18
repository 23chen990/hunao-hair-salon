import assert from 'node:assert/strict';
import { access, mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const root = resolve(import.meta.dirname, '..');
const entryPath = resolve(root, 'index.html');
try {
  await access(entryPath);
} catch {
  assert.fail('browser entry implementation is missing');
}

const playwrightPath = '/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const { chromium } = await import(pathToFileURL(playwrightPath).href);
const evidenceRoot = resolve(root, 'evidence');
await mkdir(evidenceRoot, { recursive: true });

const browser = await chromium.launch({ headless: true, args: ['--allow-file-access-from-files'] });
const results = [];

async function openPage(width, height, hasTouch = false) {
  const context = await browser.newContext({
    viewport: { width, height },
    screen: { width, height },
    deviceScaleFactor: 1,
    hasTouch,
    isMobile: hasTouch,
    locale: 'zh-CN',
    colorScheme: 'dark',
  });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (error) => errors.push(`pageerror: ${String(error)}`));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  await page.goto(pathToFileURL(entryPath).href, { waitUntil: 'load' });
  await page.waitForFunction(() => window.stackedApertureDebug?.getState?.(), undefined, {
    timeout: 5_000,
    polling: 'raf',
  });
  return { context, page, errors };
}

async function mouseShift(page, move) {
  const point = await page.evaluate((layer) => window.stackedApertureDebug.getLayerPoint(layer), move.layer);
  await page.mouse.move(point.x, point.y);
  await page.mouse.down();
  await page.mouse.move(point.x + move.delta * 54, point.y, { steps: 5 });
  await page.mouse.up();
}

async function touchShift(page, move) {
  await page.evaluate(({ layer, delta }) => {
    const point = window.stackedApertureDebug.getLayerPoint(layer);
    const canvas = document.querySelector('canvas');
    const dispatch = (type, x) => canvas.dispatchEvent(new PointerEvent(type, {
      pointerId: 17,
      pointerType: 'touch',
      isPrimary: true,
      bubbles: true,
      clientX: x,
      clientY: point.y,
      buttons: type === 'pointerup' ? 0 : 1,
    }));
    dispatch('pointerdown', point.x);
    dispatch('pointermove', point.x + delta * 54);
    dispatch('pointerup', point.x + delta * 54);
  }, move);
}

async function controlPoint(page, name) {
  return page.evaluate((controlName) => window.stackedApertureDebug.getControlPoint(controlName), name);
}

try {
  {
    const { context, page, errors } = await openPage(390, 844, false);
    const initial = await page.evaluate(() => window.stackedApertureDebug.getState());
    const solution = await page.evaluate(() => window.stackedApertureDebug.getSolution());
    assert.ok(solution.distance >= 3 && solution.distance <= 8);
    for (const move of solution.moves) await mouseShift(page, move);
    const beforeDrop = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.deepEqual(beforeDrop.offsets, initial.solutionOffsets);
    const dropPoint = await controlPoint(page, 'drop');
    await page.mouse.click(dropPoint.x, dropPoint.y);
    await page.waitForFunction(() => window.stackedApertureDebug.getState().status === 'passed');
    await page.waitForTimeout(1_000);
    const passed = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.equal(passed.status, 'passed');
    assert.equal(passed.placeholdersEnabled, false);
    await page.screenshot({ path: resolve(evidenceRoot, 'stacked-aperture-390x844.png') });

    const nextPoint = await controlPoint(page, 'next');
    await page.mouse.click(nextPoint.x, nextPoint.y);
    const advanced = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.notEqual(advanced.seed, initial.seed);
    const firstMove = (await page.evaluate(() => window.stackedApertureDebug.getSolution())).moves[0];
    await mouseShift(page, firstMove);
    const resetPoint = await controlPoint(page, 'reset');
    await page.mouse.click(resetPoint.x, resetPoint.y);
    const reset = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.deepEqual(reset.offsets, reset.initialOffsets);
    assert.equal(errors.length, 0, errors.join('\n'));
    results.push({
      viewport: '390x844',
      input: 'mouse drag',
      solvedSeed: initial.seed,
      shortestMoves: solution.distance,
      nextSeed: advanced.seed,
      resetVerified: true,
      safeLayout: reset.safeLayout,
      consoleErrors: errors,
    });
    await context.close();
  }

  {
    const { context, page, errors } = await openPage(430, 932, true);
    const initial = await page.evaluate(() => window.stackedApertureDebug.getState());
    const firstMove = (await page.evaluate(() => window.stackedApertureDebug.getSolution())).moves[0];
    await touchShift(page, firstMove);
    const shifted = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.notDeepEqual(shifted.offsets, initial.offsets);
    const dropPoint = await controlPoint(page, 'drop');
    await page.touchscreen.tap(dropPoint.x, dropPoint.y);
    await page.waitForFunction(() => window.stackedApertureDebug.getState().status === 'blocked');
    const blocked = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.ok(Number.isInteger(blocked.lastDrop.blockerIndex));
    const retryPoint = await controlPoint(page, 'retry');
    await page.touchscreen.tap(retryPoint.x, retryPoint.y);
    const retried = await page.evaluate(() => window.stackedApertureDebug.getState());
    assert.equal(retried.status, 'ready');
    assert.deepEqual(retried.offsets, blocked.offsets);
    assert.equal(errors.length, 0, errors.join('\n'));
    results.push({
      viewport: '430x932',
      input: 'touch pointer drag + tap',
      seed: initial.seed,
      firstBlockerIndex: blocked.lastDrop.blockerIndex,
      retryPreservedLayout: true,
      safeLayout: retried.safeLayout,
      consoleErrors: errors,
    });
    await context.close();
  }
} finally {
  await browser.close();
}

const report = {
  verifiedAt: new Date().toISOString(),
  entry: 'index.html',
  evidence: 'evidence/stacked-aperture-390x844.png',
  results,
};
await writeFile(resolve(evidenceRoot, 'browser-smoke.json'), `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
