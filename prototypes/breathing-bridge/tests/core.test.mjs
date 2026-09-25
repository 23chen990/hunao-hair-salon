import assert from 'node:assert/strict';
import test from 'node:test';
import * as bridge from '../src/sim.mjs';

function required(name) {
  assert.equal(typeof bridge[name], 'function', `${name} must be exported`);
  return bridge[name];
}

test('seeded reverse-generated levels are deterministic and bounded', () => {
  const createLevel = required('createLevel');
  const a = createLevel(19);
  const b = createLevel(19);
  const c = createLevel(20);

  assert.deepEqual(a, b);
  assert.notDeepEqual(a, c);
  assert.ok(a.nodeCount >= 8 && a.nodeCount <= 12);
  assert.ok(a.checkpoint.progress >= 0.45 && a.checkpoint.progress <= 0.70);
  assert.ok(a.checkpoint.prefixSeconds >= 20);
  assert.equal(a.freeRestart, true);
});

test('holding inflates and releasing vents while all membrane values stay finite', () => {
  const createLevel = required('createLevel');
  const createState = required('createState');
  const setHeld = required('setHeld');
  const step = required('step');
  const level = createLevel(3);
  const state = createState(level);

  setHeld(state, true);
  for (let i = 0; i < 180; i += 1) step(state, level);
  const inflated = state.pressure;
  assert.ok(inflated > 0.2 && inflated <= 1);

  setHeld(state, false);
  for (let i = 0; i < 180; i += 1) step(state, level);
  assert.ok(state.pressure < inflated);
  assert.deepEqual(state.inputLog.map(({ held }) => held), [true, false]);
  assert.ok(state.nodes.every((node) => Number.isFinite(node.y) && Number.isFinite(node.vy)));
});

test('the generated strategy succeeds and replays identically at 30/60/120 fps', () => {
  const createLevel = required('createLevel');
  const solveLevel = required('solveLevel');
  const runReplay = required('runReplay');
  const level = createLevel(77);
  const solution = solveLevel(level);
  assert.equal(solution.success, true);

  const samples = [30, 60, 120].map((fps) => runReplay(level, solution.events, fps));
  assert.deepEqual(samples.map(({ status }) => status), ['success', 'success', 'success']);
  assert.equal(new Set(samples.map(({ finalHash }) => finalHash)).size, 1);
  assert.equal(new Set(samples.map(({ criticalHash }) => criticalHash)).size, 1);
});

test('pause/resume freezes physics, clears held input, and preserves the physics hash', () => {
  const createLevel = required('createLevel');
  const createRunner = required('createRunner');
  const setRunnerHeld = required('setRunnerHeld');
  const advanceFrame = required('advanceFrame');
  const pauseRunner = required('pauseRunner');
  const resumeRunner = required('resumeRunner');
  const hashState = required('hashState');
  const runner = createRunner(createLevel(8));

  setRunnerHeld(runner, true);
  advanceFrame(runner, 1 / 30);
  pauseRunner(runner);
  const before = hashState(runner.state);
  const tickBefore = runner.state.tick;
  advanceFrame(runner, 47);
  assert.equal(runner.state.tick, tickBefore);
  resumeRunner(runner);
  advanceFrame(runner, 0);
  assert.equal(hashState(runner.state), before);
  assert.equal(runner.state.held, false);
  assert.equal(runner.accumulator, 0);
});

test('restart is always free and returns the exact initial state', () => {
  const createLevel = required('createLevel');
  const createState = required('createState');
  const restart = required('restart');
  const hashState = required('hashState');
  const level = createLevel(91);
  const initial = createState(level);
  const dirty = createState(level);
  dirty.pressure = 0.7;
  dirty.ball.x += 10;
  assert.equal(hashState(restart(dirty, level)), hashState(initial));
});

test('ad and network policy is closed by construction', () => {
  assert.equal(bridge.ADS_ENABLED, false);
  assert.equal(bridge.NETWORK_ALLOWED, false);
  assert.equal(bridge.NETWORK_REQUEST_BUDGET, 0);
});

