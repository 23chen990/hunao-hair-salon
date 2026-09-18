import test from 'node:test';
import assert from 'node:assert/strict';

import {
  DEFAULT_RULES,
  advanceState,
  createInitialState,
  issueClick,
  simulateAttempt,
  snapshotState,
} from '../src/rules.ts';
import { generateLevel } from '../src/generator.ts';

test('a generated reference click returns every piece inside the 0.8 second window', () => {
  const level = generateLevel(17);
  const result = simulateAttempt(level, level.referenceInput);

  assert.equal(result.outcome, 'success');
  assert.equal(result.triggerOrder.length, level.pieces.length);
  assert.ok(result.arrivalSpread <= DEFAULT_RULES.syncWindow);
});

test('the fixed-step simulation is deterministic for the same seed and click', () => {
  const level = generateLevel(91);

  const first = simulateAttempt(level, level.referenceInput);
  const second = simulateAttempt(level, level.referenceInput);

  assert.deepEqual(first, second);
});

test('only the first click is accepted', () => {
  const level = generateLevel(5);
  let state = createInitialState(level);

  state = issueClick(state, { x: 10, y: -12 });
  const firstClick = state.click;
  state = issueClick(state, { x: -80, y: 60 });

  assert.deepEqual(state.click, firstClick);
});

test('a piece that reaches the red boundary before reversal fails the attempt', () => {
  const level = generateLevel(8);
  let state = createInitialState(level);

  while (state.outcome === 'playing') {
    state = advanceState(state, DEFAULT_RULES.fixedDt);
  }

  assert.equal(state.outcome, 'boundary');
});

test('a click that reverses pieces too far apart ends at the 0.8 second timeout', () => {
  const level = generateLevel(17);
  const result = simulateAttempt(level, { x: -100, y: -100, time: 0.35 });

  assert.equal(result.outcome, 'timeout');
  assert.ok(result.triggerOrder.length >= 2);
});

test('restart creates a pristine state with no prior ripple or piece flags', () => {
  const level = generateLevel(13);
  let dirty = createInitialState(level);
  dirty = issueClick(dirty, level.referenceInput);
  for (let index = 0; index < 120; index += 1) {
    dirty = advanceState(dirty, DEFAULT_RULES.fixedDt);
  }

  const restarted = createInitialState(level);
  const fresh = createInitialState(level);

  assert.deepEqual(snapshotState(restarted), snapshotState(fresh));
  assert.equal(restarted.click, null);
  assert.ok(restarted.pieces.every((piece) => !piece.reversed && piece.arrivalTime === null));
});
