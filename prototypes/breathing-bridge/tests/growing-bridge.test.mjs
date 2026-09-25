import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import test from 'node:test';
import * as bridge from '../src/growing-bridge.mjs';

test('the growing-bridge simulation module exists before its behavior is implemented', () => {
  assert.equal(existsSync(new URL('../src/growing-bridge.mjs', import.meta.url)), true);
});

function required(name) {
  assert.equal(typeof bridge[name], 'function', `${name} must be exported`);
  return bridge[name];
}

test('seeded levels have different action grammars, not only jittered obstacle coordinates', () => {
  const createLevel = required('createLevel');
  const levels = Array.from({ length: 10 }, (_, index) => createLevel(index + 1));
  const archetypes = new Set(levels.map((level) => level.archetype));
  const signatures = new Set(levels.map((level) => level.signature));

  assert.ok(archetypes.size >= 5);
  assert.ok(signatures.size >= 8);
  assert.ok(levels.every((level) => level.segments.length >= 5));
  assert.ok(levels.every((level) => level.worldLength > 1_500));
  assert.ok(levels.every((level) => level.checkpoint.progress >= 0.45 && level.checkpoint.progress <= 0.70));
});

test('holding grows the bridge front and releasing slows growth while auto-run advances', () => {
  const createLevel = required('createLevel');
  const createState = required('createState');
  const setHeld = required('setHeld');
  const step = required('step');
  const level = createLevel(2);
  const state = createState(level);
  const initialFront = state.bridgeFront;
  const initialX = state.ball.x;

  setHeld(state, true);
  for (let index = 0; index < 120; index += 1) step(state, level);
  const heldGrowth = state.bridgeFront - initialFront;
  const heldTravel = state.ball.x - initialX;
  setHeld(state, false);
  const frontBeforeRelease = state.bridgeFront;
  for (let index = 0; index < 120; index += 1) step(state, level);
  const releaseGrowth = state.bridgeFront - frontBeforeRelease;

  assert.ok(heldGrowth > 45);
  assert.ok(releaseGrowth < heldGrowth * 0.55);
  assert.ok(heldTravel > 70);
  assert.ok(state.nodes.every((node) => Number.isFinite(node.x) && Number.isFinite(node.y)));
});

test('a pressure release produces a jump impulse and can complete a generated segment', () => {
  const createLevel = required('createLevel');
  const createState = required('createState');
  const setHeld = required('setHeld');
  const step = required('step');
  const level = createLevel(5);
  const state = createState(level);

  setHeld(state, true);
  for (let index = 0; index < 90 && state.status === 'playing'; index += 1) step(state, level);
  const pressureAtRelease = state.pressure;
  setHeld(state, false);
  step(state, level);

  assert.ok(pressureAtRelease > 0.35);
  assert.ok(state.jumps >= 1);
  assert.ok(state.ball.vy < 0);
});

test('known paths complete and cross-frame replays keep final and critical hashes identical', () => {
  const createLevel = required('createLevel');
  const solveLevel = required('solveLevel');
  const runReplay = required('runReplay');
  for (const seed of [1, 2, 3, 4, 5]) {
    const level = createLevel(seed);
    const solution = solveLevel(level);
    assert.equal(solution.success, true, `seed ${seed} should have a path`);
    const replays = [30, 60, 120].map((fps) => runReplay(level, solution.events, fps));
    assert.equal(new Set(replays.map((replay) => replay.status)).size, 1);
    assert.equal(new Set(replays.map((replay) => replay.finalHash)).size, 1);
    assert.equal(new Set(replays.map((replay) => replay.criticalHash)).size, 1);
  }
});

test('the level pack exposes five visible archetypes for browser next-level flow', () => {
  const buildLevelPack = required('buildLevelPack');
  const pack = buildLevelPack(1, 10);
  assert.equal(pack.length, 10);
  assert.ok(new Set(pack.map((level) => level.archetype)).size >= 5);
  assert.ok(pack.every((level, index) => level.levelNumber === index + 1));
  assert.ok(pack.every((level) => level.nextSeed !== level.seed));
});
