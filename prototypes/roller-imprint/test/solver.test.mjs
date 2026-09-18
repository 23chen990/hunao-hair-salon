import test from 'node:test';
import assert from 'node:assert/strict';

let solver = {};
try {
  solver = await import('../src/solver.mjs');
} catch {
  // Expected RED until the generation/solver behavior exists.
}

function need(name) {
  assert.equal(typeof solver[name], 'function', `${name} implementation is missing`);
  return solver[name];
}

test('legal action space contains each face, direction, and discrete stop', () => {
  const allLegalActions = need('allLegalActions');
  const actions = allLegalActions();
  assert.equal(actions.length, 8 * 2 * 12);
  assert.equal(new Set(actions.map((action) => JSON.stringify(action))).size, actions.length);
});

test('generated targets are non-empty, exact, seeded, and shortest in 2–5 passes', () => {
  const generateLevel = need('generateLevel');
  const boardFromActions = need('boardFromActions');
  const solveTarget = need('solveTarget');
  for (let seed = 1; seed <= 32; seed += 1) {
    const first = generateLevel(seed);
    const second = generateLevel(seed);
    assert.deepEqual(second, first, `seed ${seed} must reproduce`);
    assert.equal(first.target.length, 12);
    assert.ok(first.target.some(Boolean), `seed ${seed} cannot begin solved`);
    assert.ok(first.shortest >= 2 && first.shortest <= 5, `seed ${seed} depth`);
    assert.deepEqual(boardFromActions(first.solution), first.target, `seed ${seed} solution`);
    assert.equal(solveTarget(first.target, 5).depth, first.shortest, `seed ${seed} solver`);
  }
});

test('every generated acceptance level exposes a legal, genuinely longer exact solution', () => {
  const generateLevel = need('generateLevel');
  const boardFromActions = need('boardFromActions');
  for (let seed = 1; seed <= 32; seed += 1) {
    const level = generateLevel(seed);
    assert.ok(level.alternative.length > level.shortest, `seed ${seed} must have a suboptimal path`);
    assert.deepEqual(boardFromActions(level.alternative), level.target, `seed ${seed} alternative`);
    assert.notDeepEqual(level.alternative, level.solution, `seed ${seed} alternative differs`);
  }
});

test('neighbor viability measures face, direction, and stop perturbations against real targets', () => {
  const generateLevel = need('generateLevel');
  const neighborViability = need('neighborViability');
  let robust = 0;
  for (let seed = 1; seed <= 64; seed += 1) {
    const metric = neighborViability(generateLevel(seed));
    assert.equal(typeof metric.face, 'boolean');
    assert.equal(typeof metric.direction, 'boolean');
    assert.equal(typeof metric.stop, 'boolean');
    robust += Number(metric.all);
  }
  assert.ok(robust >= 32, `expected >=32 robust seeds, received ${robust}`);
});

test('uniform random legal roll sequences are sampled by the actual model', () => {
  const generateLevel = need('generateLevel');
  const sampleRandomCompletion = need('sampleRandomCompletion');
  const sample = sampleRandomCompletion(generateLevel(11), 250, 991);
  assert.equal(sample.trials, 250);
  assert.ok(Number.isInteger(sample.completed));
  assert.equal(sample.rate, sample.completed / sample.trials);
  assert.ok(sample.rate >= 0 && sample.rate <= 1);
});
