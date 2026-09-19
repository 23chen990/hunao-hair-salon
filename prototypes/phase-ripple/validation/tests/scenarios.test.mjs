import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

import { generateAcceptedLevel } from '../../src/generator.ts';
import { ACCEPTANCE_SEEDS } from '../../src/levels.ts';
import { evaluateAttemptAnalytic, simulateAttempt } from '../../src/rules.ts';

const validationRoot = new URL('../', import.meta.url);

test('capture manifest contains only reproducible real-game scenarios', async () => {
  const manifestUrl = new URL('scenarios.mjs', validationRoot);
  assert.ok(existsSync(manifestUrl), 'scenarios.mjs must exist before captures can run');

  const { CAPTURE, SCENARIOS } = await import(pathToFileURL(manifestUrl.pathname));
  assert.deepEqual(
    { width: CAPTURE.width, height: CAPTURE.height },
    { width: 390, height: 844 },
  );
  assert.ok(CAPTURE.durationSeconds >= 6 && CAPTURE.durationSeconds <= 10);
  assert.ok(SCENARIOS.length >= 6 && SCENARIOS.length <= 10);

  const ids = new Set();
  const categories = new Set();
  const pieceCounts = new Set();

  for (const scenario of SCENARIOS) {
    assert.match(scenario.id, /^\d{2}-[a-z0-9-]+$/);
    assert.ok(!ids.has(scenario.id), `duplicate id: ${scenario.id}`);
    ids.add(scenario.id);
    assert.ok(scenario.hook.length > 0 && scenario.hook.length <= 14, scenario.id);

    const level = generateAcceptedLevel(scenario.seed);
    assert.equal(level.pieces.length, scenario.pieceCount, scenario.id);
    assert.ok(Math.abs(scenario.input.x) <= 145, scenario.id);
    assert.ok(Math.abs(scenario.input.y) <= 145, scenario.id);
    assert.ok(scenario.input.time >= 0 && scenario.input.time <= 1.5, scenario.id);

    const result = simulateAttempt(level, scenario.input);
    assert.equal(result.outcome, scenario.expectedOutcome, scenario.id);
    assert.ok(result.resultTime < CAPTURE.durationSeconds - 1, scenario.id);

    if (scenario.category === 'near_miss') {
      const analytic = evaluateAttemptAnalytic(level, scenario.input);
      assert.equal(analytic.outcome, 'timeout', scenario.id);
      assert.ok(analytic.arrivalSpread > 0.8 && analytic.arrivalSpread <= 0.82, scenario.id);
      assert.equal(result.triggerOrder.length, level.pieces.length, scenario.id);
    }
    if (scenario.category === 'early_click') {
      assert.ok(scenario.input.time <= 0.05, scenario.id);
      assert.equal(result.outcome, 'timeout', scenario.id);
    }

    categories.add(scenario.category);
    pieceCounts.add(scenario.pieceCount);
  }

  assert.ok(categories.has('success'));
  assert.ok(categories.has('near_miss'));
  assert.ok(categories.has('early_click'));
  assert.deepEqual([...pieceCounts].sort((a, b) => a - b), [3, 4, 5, 6]);

  const byId = new Map(SCENARIOS.map((scenario) => [scenario.id, scenario]));
  assert.ok(byId.has('04-later-success-4'));
  assert.equal(byId.get('01-sync-4').hook, byId.get('02-near-miss-4').hook);
  assert.equal(byId.get('03-early-click-4').hook, byId.get('04-later-success-4').hook);
  assert.equal(byId.get('03-early-click-4').input.x, byId.get('04-later-success-4').input.x);
  assert.equal(byId.get('03-early-click-4').input.y, byId.get('04-later-success-4').input.y);
  assert.equal(byId.get('05-sync-6').hook, byId.get('06-sync-3').hook);
  assert.equal(byId.get('06-sync-3').hook, byId.get('07-sync-5').hook);
});

test('model-to-canvas mapping matches the production 390 by 844 layout', async () => {
  const libraryUrl = new URL('capture-lib.mjs', validationRoot);
  assert.ok(existsSync(libraryUrl), 'capture-lib.mjs must exist before captures can run');
  const { modelToCanvas } = await import(pathToFileURL(libraryUrl.pathname));

  assert.deepEqual(modelToCanvas({ x: 0, y: 0 }, 390, 844), { x: 195, y: 283 });
  const mapped = modelToCanvas({ x: 37.68005381583613, y: -64.5807301973603 }, 390, 844);
  assert.ok(Math.abs(mapped.x - 236.95045991496423) < 1e-9);
  assert.ok(Math.abs(mapped.y - 211.1001203802722) < 1e-9);
});

test('capture plan reaches later seeds through successful real rounds', async () => {
  const manifestUrl = new URL('scenarios.mjs', validationRoot);
  const libraryUrl = new URL('capture-lib.mjs', validationRoot);
  const { SCENARIOS } = await import(pathToFileURL(manifestUrl.pathname));
  const { buildCapturePlan } = await import(pathToFileURL(libraryUrl.pathname));
  const plan = buildCapturePlan(SCENARIOS, ACCEPTANCE_SEEDS);

  assert.deepEqual(plan.map((scenario) => scenario.id), [
    '02-near-miss-4',
    '03-early-click-4',
    '04-later-success-4',
    '01-sync-4',
    '05-sync-6',
    '06-sync-3',
    '07-sync-5',
  ]);
  assert.deepEqual(plan.map((scenario) => scenario.seed), [17, 17, 17, 17, 167, 223, 293]);
});
