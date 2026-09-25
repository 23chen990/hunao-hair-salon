import test from 'node:test';
import assert from 'node:assert/strict';

async function optionalModule(path) {
  try {
    return await import(path);
  } catch {
    return null;
  }
}

const model = await optionalModule('../src/model.js');
const replay = await optionalModule('../src/replay.js');
const solver = await optionalModule('../src/solver.js');

test('canonical state serialization round-trips with the same hash', () => {
  assert.ok(model, 'model implementation is missing');
  const level = model.generateLevel(41);
  const state = model.createGameState(level);
  const serialized = model.serializeGameState(state);
  const restored = model.deserializeGameState(serialized);
  assert.deepEqual(restored, state);
  assert.equal(model.hashGameState(restored), model.hashGameState(state));
  assert.equal(model.serializeGameState(restored), serialized);
});

test('drop checks sheets in order and reports the first blocking layer', () => {
  assert.ok(model, 'model implementation is missing');
  const level = model.generateLevel(51);
  const offsets = level.layers.map((layer) => layer.solutionSlot);
  offsets[1] = offsets[1] === level.slotMax ? offsets[1] - 1 : offsets[1] + 1;
  offsets[2] = offsets[2] === level.slotMax ? offsets[2] - 1 : offsets[2] + 1;
  const result = model.dropBead(level, offsets);
  assert.equal(result.passed, false);
  assert.equal(result.blockerIndex, 1);
  assert.deepEqual(result.passedLayerIndices, [0]);
});

test('free retry preserves the failed layout and clears only transient outcome', () => {
  assert.ok(model, 'model implementation is missing');
  const level = model.generateLevel(61);
  const initial = model.createGameState(level);
  const failed = model.attemptDrop(level, initial);
  assert.equal(failed.status, 'blocked');
  const retried = model.retryLevel(failed);
  assert.equal(retried.status, 'ready');
  assert.deepEqual(retried.offsets, failed.offsets);
  assert.equal(retried.dropCount, failed.dropCount);
  assert.equal(retried.consecutiveFailures, failed.consecutiveFailures);
  assert.equal(retried.lastDrop, null);
});

test('event replay verifies every frame hash and reproduces final state', () => {
  assert.ok(model, 'model implementation is missing');
  assert.ok(replay, 'replay implementation is missing');
  assert.ok(solver, 'solver implementation is missing');
  const level = model.generateLevel(71);
  const solution = solver.solveLevel(level, level.initialOffsets);
  const actions = solution.moves.map((move, index) => ({ ...move, type: 'move', advanceMs: 900 + index }));
  actions.push({ type: 'drop', advanceMs: 2_000 });
  const recorded = replay.recordRun(level, actions);
  const replayed = replay.replayRun(level, recorded.events);
  assert.equal(replayed.hash, recorded.hash);
  assert.deepEqual(replayed.state, recorded.state);
  assert.equal(recorded.events.length, actions.length);
  assert.ok(recorded.events.every((event, index) => event.seq === index + 1 && event.stateHash));
});
