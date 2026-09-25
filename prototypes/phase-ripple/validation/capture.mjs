import assert from 'node:assert/strict';
import { mkdir, rename, rm, writeFile } from 'node:fs/promises';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { ACCEPTANCE_SEEDS } from '../src/levels.ts';
import { buildCapturePlan, modelToCanvas } from './capture-lib.mjs';
import { CAPTURE, SCENARIOS } from './scenarios.mjs';

const validationRoot = dirname(fileURLToPath(import.meta.url));
const prototypeRoot = resolve(validationRoot, '..');
const rawRoot = join(validationRoot, 'raw');
const evidenceRoot = join(validationRoot, 'evidence');
const rawVideoTarget = join(rawRoot, 'phase-ripple-continuous.webm');
const evidenceTarget = join(evidenceRoot, 'capture-run.json');
const sourceUrl = pathToFileURL(join(prototypeRoot, 'index.html')).href;
const playwrightCandidates = [
  process.env.PHASE_RIPPLE_PLAYWRIGHT,
  '/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/playwright/index.mjs',
  '/Users/kker/.npm/_npx/e41f203b7505f1fb/node_modules/playwright/index.mjs',
].filter(Boolean);

const capturePlan = buildCapturePlan(SCENARIOS, ACCEPTANCE_SEEDS);
let browser = null;
let context = null;

function monotonicSeconds(origin) {
  return (performance.now() - origin) / 1000;
}

async function importPlaywright() {
  const failures = [];
  for (const candidate of playwrightCandidates) {
    try {
      return await import(pathToFileURL(candidate).href);
    } catch (error) {
      failures.push(`${candidate}: ${error instanceof Error ? error.message : String(error)}`);
    }
  }
  throw new Error(`Playwright unavailable:\n${failures.join('\n')}`);
}

async function installCaptureOverlay(page) {
  await page.addStyleTag({ content: `
    .debug-link { display: none !important; }
    #validation-hook {
      position: fixed;
      z-index: 50;
      left: 16px;
      right: 16px;
      bottom: 20px;
      min-height: 52px;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 10px 14px;
      border: 1px solid #29454c;
      color: #eaf8f5;
      background: #0d191dee;
      font: 700 18px/1.25 -apple-system, BlinkMacSystemFont, "PingFang SC", sans-serif;
      letter-spacing: .02em;
      text-align: center;
      pointer-events: none;
    }
  ` });
  await page.evaluate(() => {
    const hook = document.createElement('div');
    hook.id = 'validation-hook';
    hook.textContent = '真实玩法准备中';
    document.body.append(hook);
  });
}

async function setHook(page, value) {
  await page.evaluate((text) => {
    const hook = document.querySelector('#validation-hook');
    if (hook === null) throw new Error('Missing validation hook');
    hook.textContent = text;
  }, value);
}

async function readState(page) {
  return page.evaluate(() => window.phaseRippleDebug.getState());
}

async function restartCurrentSeed(page) {
  await page.evaluate(() => window.phaseRippleDebug.restart());
  await page.waitForFunction(() => {
    const state = window.phaseRippleDebug.getState();
    return state.outcome === 'playing' && state.click === null && state.time < 0.15;
  }, undefined, { timeout: 2_000 });
}

async function clickAtGameTime(page, input) {
  const canvasPoint = modelToCanvas(input, CAPTURE.width, CAPTURE.height);
  await page.waitForFunction((targetTime) => {
    const state = window.phaseRippleDebug.getState();
    return state.outcome === 'playing' && state.click === null && state.time >= targetTime;
  }, input.time, { timeout: 3_000, polling: 'raf' });
  await page.mouse.click(canvasPoint.x, canvasPoint.y);
  await page.waitForFunction(() => window.phaseRippleDebug.getState().click !== null, undefined, {
    timeout: 1_000,
    polling: 'raf',
  });
  return canvasPoint;
}

async function waitForResult(page, timeout = 7_000) {
  await page.waitForFunction(() => window.phaseRippleDebug.getState().outcome !== 'playing', undefined, {
    timeout,
    polling: 'raf',
  });
  return readState(page);
}

async function playReferenceAndAdvance(page) {
  let state = await readState(page);
  if (state.outcome !== 'success') {
    await restartCurrentSeed(page);
    state = await readState(page);
    await clickAtGameTime(page, state.level.referenceInput);
    state = await waitForResult(page);
    assert.equal(state.outcome, 'success', `seed ${state.level.seed} reference input failed`);
  }
  const priorSeed = state.level.seed;
  await page.mouse.click(CAPTURE.width / 2, CAPTURE.height / 2);
  await page.waitForFunction((seed) => window.phaseRippleDebug.getState().level.seed !== seed, priorSeed, {
    timeout: 2_000,
    polling: 'raf',
  });
}

async function advanceToSeed(page, targetSeed) {
  await setHook(page, '真实玩法准备中');
  for (let guard = 0; guard < ACCEPTANCE_SEEDS.length; guard += 1) {
    const state = await readState(page);
    if (state.level.seed === targetSeed) return;
    const currentIndex = ACCEPTANCE_SEEDS.indexOf(state.level.seed);
    const targetIndex = ACCEPTANCE_SEEDS.indexOf(targetSeed);
    if (currentIndex < 0 || targetIndex < 0 || currentIndex > targetIndex) {
      throw new Error(`Cannot advance from seed ${state.level.seed} to ${targetSeed}`);
    }
    process.stdout.write(`advance seed ${state.level.seed} -> ${ACCEPTANCE_SEEDS[currentIndex + 1]}\n`);
    await playReferenceAndAdvance(page);
  }
  throw new Error(`Failed to reach seed ${targetSeed}`);
}

async function captureScenario(page, scenario, recordOrigin) {
  await setHook(page, scenario.hook);
  const eventOffset = await page.evaluate(() => window.phaseRippleDebug.getEvents().length);
  await restartCurrentSeed(page);
  const rawStartSeconds = monotonicSeconds(recordOrigin);
  const wallStart = performance.now();
  const canvasPoint = await clickAtGameTime(page, scenario.input);
  const resultState = await waitForResult(page);
  assert.equal(resultState.level.seed, scenario.seed, scenario.id);
  assert.equal(resultState.level.pieces.length, scenario.pieceCount, scenario.id);
  assert.equal(resultState.outcome, scenario.expectedOutcome, scenario.id);

  const events = await page.evaluate((offset) => window.phaseRippleDebug.getEvents().slice(offset), eventOffset);
  const firstClick = events.find((event) => event.type === 'first_click');
  const levelResult = events.find((event) => event.type === 'level_result');
  assert.ok(firstClick, `${scenario.id} did not emit first_click`);
  assert.ok(levelResult, `${scenario.id} did not emit level_result`);
  assert.equal(levelResult.data.outcome, scenario.expectedOutcome, scenario.id);

  const remainingMs = CAPTURE.durationSeconds * 1000 - (performance.now() - wallStart);
  if (remainingMs > 0) await page.waitForTimeout(remainingMs);
  const rawEndSeconds = monotonicSeconds(recordOrigin);
  process.stdout.write(
    `captured ${scenario.id}: ${resultState.outcome}, click=${Number(firstClick.data.time).toFixed(3)}s, ` +
    `result=${Number(levelResult.data.resultTime).toFixed(3)}s\n`,
  );
  return {
    id: scenario.id,
    seed: scenario.seed,
    pieceCount: scenario.pieceCount,
    category: scenario.category,
    hook: scenario.hook,
    requestedInput: scenario.input,
    canvasPoint,
    expectedOutcome: scenario.expectedOutcome,
    actualOutcome: resultState.outcome,
    rawStartSeconds,
    rawEndSeconds,
    requestedDurationSeconds: CAPTURE.durationSeconds,
    firstClick,
    levelResult,
    triggerOrder: [...resultState.triggerOrder],
  };
}

async function main() {
  await mkdir(rawRoot, { recursive: true });
  await mkdir(evidenceRoot, { recursive: true });
  const { chromium } = await importPlaywright();
  browser = await chromium.launch({
    headless: true,
    args: ['--allow-file-access-from-files'],
  });
  context = await browser.newContext({
    viewport: { width: CAPTURE.width, height: CAPTURE.height },
    screen: { width: CAPTURE.width, height: CAPTURE.height },
    deviceScaleFactor: 1,
    locale: 'zh-CN',
    colorScheme: 'dark',
    recordVideo: {
      dir: rawRoot,
      size: { width: CAPTURE.width, height: CAPTURE.height },
    },
  });

  const recordOrigin = performance.now();
  const page = await context.newPage();
  const video = page.video();
  assert.ok(video, 'Playwright video recorder did not start');
  const pageErrors = [];
  page.on('pageerror', (error) => pageErrors.push(String(error)));
  await page.goto(sourceUrl, { waitUntil: 'load' });
  await page.waitForFunction(() => window.phaseRippleDebug?.getState !== undefined, undefined, {
    timeout: 5_000,
  });
  await installCaptureOverlay(page);

  const captures = [];
  for (const scenario of capturePlan) {
    await advanceToSeed(page, scenario.seed);
    captures.push(await captureScenario(page, scenario, recordOrigin));
  }
  await setHook(page, '真实玩法捕获完成');
  await page.waitForTimeout(250);

  const recordWallDurationSeconds = monotonicSeconds(recordOrigin);
  const allEvents = await page.evaluate(() => window.phaseRippleDebug.getEvents());
  await context.close();
  context = null;
  const generatedVideoPath = await video.path();
  await rm(rawVideoTarget, { force: true });
  await rename(generatedVideoPath, rawVideoTarget);

  const evidence = {
    generatedAt: new Date().toISOString(),
    sourceUrl,
    sourcePrototype: prototypeRoot,
    viewport: { width: CAPTURE.width, height: CAPTURE.height, deviceScaleFactor: 1 },
    rawVideo: rawVideoTarget,
    recordWallDurationSeconds,
    pageErrors,
    captures,
    allEvents,
  };
  await writeFile(evidenceTarget, `${JSON.stringify(evidence, null, 2)}\n`, 'utf8');
  assert.deepEqual(pageErrors, []);
  process.stdout.write(`raw video: ${rawVideoTarget}\n`);
  process.stdout.write(`evidence: ${evidenceTarget}\n`);
}

try {
  await main();
} finally {
  if (context !== null) await context.close().catch(() => {});
  if (browser !== null) await browser.close().catch(() => {});
}
