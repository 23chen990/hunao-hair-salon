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
const solver = await optionalModule('../src/solver.js');

test('reverse generation starts blocked and BFS finds a 3–8 move solution', () => {
  assert.ok(model, 'model implementation is missing');
  assert.ok(solver, 'solver implementation is missing');
  for (let seed = 1; seed <= 40; seed += 1) {
    const level = model.generateLevel(seed);
    assert.equal(model.dropBead(level, level.initialOffsets).passed, false);
    const solution = solver.solveLevel(level, level.initialOffsets);
    assert.ok(solution, `seed ${seed} must be solvable`);
    assert.ok(solution.distance >= 3 && solution.distance <= 8, `seed ${seed}: ${solution.distance}`);
    const solvedOffsets = solver.applyMoves(level, level.initialOffsets, solution.moves);
    assert.equal(model.dropBead(level, solvedOffsets).passed, true);
  }
});

test('generated sheets expose geometric solution holes and non-colliding visible decoys', () => {
  assert.ok(model, 'model implementation is missing');
  for (let seed = 1; seed <= 40; seed += 1) {
    const level = model.generateLevel(seed);
    assert.ok(level.layerCount === 3 || level.layerCount === 4);
    assert.equal(new Set(level.layers.map((layer) => layer.color)).size, level.layerCount);
    for (const layer of level.layers) {
      const solutionHoles = layer.holes.filter((hole) => hole.role === 'solution');
      const decoys = layer.holes.filter((hole) => hole.role === 'decoy');
      assert.equal(solutionHoles.length, 1);
      assert.ok(decoys.length >= 2);
      for (const decoy of decoys) {
        for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
          const centerDistance = Math.abs((decoy.localSlot + slot) * level.slotSpacing);
          assert.ok(
            centerDistance + level.beadRadius > decoy.radius,
            `seed ${seed} decoy ${decoy.id} must visibly block at slot ${slot}`,
          );
        }
      }
    }
  }
});

test('every legal offset state remains connected to a solution', () => {
  assert.ok(model, 'model implementation is missing');
  assert.ok(solver, 'solver implementation is missing');
  const level = model.generateLevel(90210);
  for (const offsets of solver.enumerateOffsets(level)) {
    assert.ok(solver.solveLevel(level, offsets), `dead state: ${offsets.join(',')}`);
  }
});

test('a legal move shifts one layer by one adjacent slot without mutating input', () => {
  assert.ok(model, 'model implementation is missing');
  const level = model.generateLevel(7);
  const before = [...level.initialOffsets];
  const layer = before.findIndex((offset) => offset < level.slotMax);
  const after = model.applyOffsetMove(level, before, { layer, delta: 1 });
  assert.deepEqual(before, level.initialOffsets);
  assert.equal(after[layer], before[layer] + 1);
  assert.throws(() => model.applyOffsetMove(level, before, { layer, delta: 2 }), /adjacent/i);
});
