import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { pathToFileURL } from 'node:url';

const playwrightPath = '/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs';
const { chromium } = await import(pathToFileURL(playwrightPath).href);
const root = resolve(import.meta.dirname, '..');
const screenshotRoot = resolve(root, 'screenshots');
const reportRoot = resolve(root, 'reports');
await mkdir(screenshotRoot, { recursive: true });
await mkdir(reportRoot, { recursive: true });

const browser = await chromium.launch({ headless: true, args: ['--allow-file-access-from-files'] });
const results = [];

async function openPage(width, height) {
  const context = await browser.newContext({
    viewport: { width, height },
    screen: { width, height },
    deviceScaleFactor: 1,
    locale: 'zh-CN',
    colorScheme: 'light',
  });
  const page = await context.newPage();
  const errors = [];
  page.on('pageerror', (error) => errors.push(`pageerror: ${String(error)}`));
  page.on('console', (message) => {
    if (message.type() === 'error') errors.push(`console: ${message.text()}`);
  });
  await page.goto(pathToFileURL(resolve(root, 'index.html')).href, { waitUntil: 'load' });
  await page.waitForFunction(() => window.phaseRippleDebug?.getState?.(), undefined, {
    timeout: 5_000,
    polling: 'raf',
  });
  return { context, page, errors };
}

{
  const { context, page, errors } = await openPage(390, 844);
  await page.evaluate(() => window.phaseRippleDebug.completeTutorial());
  await page.evaluate(() => window.phaseRippleDebug.restart());
  const initial = await page.evaluate(() => window.phaseRippleDebug.getState());
  const reference = initial.level.referenceInput;
  await page.waitForFunction((time) => window.phaseRippleDebug.getState().time >= time, reference.time, {
    timeout: 3_000,
    polling: 'raf',
  });
  const point = await page.evaluate(() => window.phaseRippleDebug.getReferenceCanvasPoint());
  await page.mouse.click(point.x, point.y);
  await page.waitForFunction(() => window.phaseRippleDebug.getState().outcome !== 'playing', undefined, {
    timeout: 7_000,
    polling: 'raf',
  });
  const success = await page.evaluate(() => window.phaseRippleDebug.getState());
  assert.equal(success.outcome, 'success', 'real reference pointer click must succeed');
  assert.equal(success.triggerOrder.length, success.level.pieces.length);
  await page.screenshot({ path: resolve(screenshotRoot, 'phase-ripple-skinned-390x844.png') });

  const priorSeed = success.level.seed;
  const nextAction = await page.evaluate(() => window.phaseRippleDebug.getResultActionPoint('next'));
  await page.mouse.click(nextAction.x, nextAction.y);
  await page.waitForFunction((seed) => window.phaseRippleDebug.getState().level.seed !== seed, priorSeed, {
    timeout: 2_000,
    polling: 'raf',
  });
  const advanced = await page.evaluate(() => window.phaseRippleDebug.getState());
  assert.equal(errors.length, 0, errors.join('\n'));
  results.push({
    viewport: '390x844',
    referenceSeed: priorSeed,
    referenceOutcome: success.outcome,
    referenceSpread: Math.max(...success.pieces.map((piece) => piece.arrivalTime)) - Math.min(...success.pieces.map((piece) => piece.arrivalTime)),
    nextSeed: advanced.level.seed,
    consoleErrors: errors,
  });
  await context.close();
}

{
  const { context, page, errors } = await openPage(430, 932);
  await page.evaluate(() => window.phaseRippleDebug.completeTutorial());
  await page.evaluate(() => window.phaseRippleDebug.restart());
  const badPoint = await page.evaluate(() => window.phaseRippleDebug.getCanvasPoint({ x: -100, y: -100 }));
  await page.mouse.click(badPoint.x, badPoint.y);
  await page.waitForFunction(() => window.phaseRippleDebug.getState().outcome !== 'playing', undefined, {
    timeout: 7_000,
    polling: 'raf',
  });
  const failure = await page.evaluate(() => window.phaseRippleDebug.getState());
  assert.notEqual(failure.outcome, 'success', 'deliberately poor click must fail');
  await page.screenshot({ path: resolve(screenshotRoot, 'phase-ripple-skinned-430x932.png') });

  const eventsBeforeRetry = await page.evaluate(() => window.phaseRippleDebug.getEvents().length);
  const retryAction = await page.evaluate(() => window.phaseRippleDebug.getResultActionPoint('retry'));
  await page.mouse.click(retryAction.x, retryAction.y);
  await page.waitForFunction(() => {
    const state = window.phaseRippleDebug.getState();
    return state.outcome === 'playing' && state.click === null && state.time < 0.3;
  }, undefined, { timeout: 2_000, polling: 'raf' });
  const eventsAfterRetry = await page.evaluate((offset) => window.phaseRippleDebug.getEvents().slice(offset), eventsBeforeRetry);
  assert.ok(eventsAfterRetry.some((event) => event.type === 'retry'));
  assert.equal(errors.length, 0, errors.join('\n'));
  results.push({
    viewport: '430x932',
    failureSeed: failure.level.seed,
    failureOutcome: failure.outcome,
    retryRecorded: true,
    consoleErrors: errors,
  });
  await context.close();
}

await browser.close();
const report = { verifiedAt: new Date().toISOString(), results };
await writeFile(resolve(reportRoot, 'browser-verification.json'), `${JSON.stringify(report, null, 2)}\n`);
console.log(JSON.stringify(report, null, 2));
