import test from 'node:test';
import assert from 'node:assert/strict';

let model = {};
try {
  model = await import('../src/model.mjs');
} catch {
  // A missing implementation is the expected first RED for this throwaway spike.
}

function need(name) {
  assert.equal(typeof model[name], 'function', `${name} implementation is missing`);
  return model[name];
}

const targetWith = (...columns) => {
  const target = Array(12).fill(0);
  for (const [column, bits] of columns) target[column] = bits;
  return target;
};

test('drag distance maps to discrete turns and clamps to the 12-column tape', () => {
  const actionFromPointerSamples = need('actionFromPointerSamples');
  assert.deepEqual(
    actionFromPointerSamples([{ x: 10 }, { x: 142 }], 44, 3),
    { direction: 1, distance: 3, face: 3 },
  );
  assert.deepEqual(
    actionFromPointerSamples([{ x: 600 }, { x: -20 }], 44, 7),
    { direction: -1, distance: 12, face: 7 },
  );
});

test('forward and backward rolls contact deterministic columns and faces', () => {
  const traceRoll = need('traceRoll');
  assert.deepEqual(traceRoll({ direction: 1, distance: 3, face: 2 }), [
    { column: 0, face: 2 },
    { column: 1, face: 3 },
    { column: 2, face: 4 },
  ]);
  assert.deepEqual(traceRoll({ direction: -1, distance: 3, face: 2 }), [
    { column: 11, face: 2 },
    { column: 10, face: 1 },
    { column: 9, face: 0 },
  ]);
});

test('a roll stamps raised face bits and reports wrong and missing cells', () => {
  const createGame = need('createGame');
  const applyRoll = need('applyRoll');
  const compareBoards = need('compareBoards');
  const faceBits = need('faceBits');
  const target = targetWith([0, faceBits(0)], [1, faceBits(1)], [2, 1]);
  const initial = createGame({ seed: 9, target });
  const rolled = applyRoll(initial, { direction: 1, distance: 2, face: 0 });
  assert.equal(rolled.ink[0], faceBits(0));
  assert.equal(rolled.ink[1], faceBits(1));
  assert.deepEqual(compareBoards(rolled.ink, target), {
    missing: 1,
    wrong: 0,
    exact: false,
  });
});

test('whole-pass undo restores the exact prior playable-state hash', () => {
  const createGame = need('createGame');
  const applyRoll = need('applyRoll');
  const undoRoll = need('undoRoll');
  const stateHash = need('stateHash');
  const initial = createGame({ seed: 27, target: Array(12).fill(255) });
  const before = stateHash(initial);
  const after = applyRoll(initial, { direction: 1, distance: 6, face: 4 });
  assert.notEqual(stateHash(after), before);
  assert.equal(stateHash(undoRoll(after)), before);
});

test('free restart restores the seed initial state without an ad branch', () => {
  const createGame = need('createGame');
  const applyRoll = need('applyRoll');
  const restartGame = need('restartGame');
  const stateHash = need('stateHash');
  const initial = createGame({ seed: 42, target: Array(12).fill(255) });
  const changed = applyRoll(initial, { direction: -1, distance: 5, face: 6 });
  assert.equal(stateHash(restartGame(changed)), stateHash(initial));
});

test('serialized event replay reproduces every frame hash deterministically', () => {
  const createGame = need('createGame');
  const replayEvents = need('replayEvents');
  const serializeState = need('serializeState');
  const deserializeState = need('deserializeState');
  const initial = createGame({ seed: 81, target: Array(12).fill(255) });
  const events = [
    { type: 'tick', ms: 480 },
    { type: 'roll', action: { direction: 1, distance: 4, face: 2 } },
    { type: 'tick', ms: 120 },
    { type: 'undo' },
    { type: 'roll', action: { direction: -1, distance: 7, face: 5 } },
  ];
  const first = replayEvents(initial, events);
  const restored = deserializeState(serializeState(initial));
  const second = replayEvents(restored, JSON.parse(JSON.stringify(events)));
  assert.deepEqual(second.hashes, first.hashes);
  assert.equal(second.finalHash, first.finalHash);
});

test('30/60/120fps pointer sampling yields identical stamped cells', () => {
  const actionFromPointerSamples = need('actionFromPointerSamples');
  const rollMask = need('rollMask');
  const samples = (count) => Array.from({ length: count + 1 }, (_, index) => ({
    x: 20 + (264 * index) / count,
  }));
  const masks = [30, 60, 120].map((fps) => rollMask(
    actionFromPointerSamples(samples(fps), 44, 1),
  ));
  assert.deepEqual(masks[1], masks[0]);
  assert.deepEqual(masks[2], masks[0]);
});
