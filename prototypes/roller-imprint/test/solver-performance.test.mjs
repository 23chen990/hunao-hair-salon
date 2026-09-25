import test from 'node:test';
import assert from 'node:assert/strict';
import { performance } from 'node:perf_hooks';
import { generateLevel, solveTarget } from '../src/solver.mjs';

test('cold generation plus strict shortest solving has development-machine p95 <=30ms', () => {
  const timings = [];
  for (let seed = 10001; seed <= 10100; seed += 1) {
    const started = performance.now();
    const level = generateLevel(seed);
    const solution = solveTarget(level.target, 5);
    timings.push(performance.now() - started);
    assert.equal(solution.depth, level.shortest);
  }
  timings.sort((left, right) => left - right);
  const p95 = timings[Math.floor((timings.length - 1) * 0.95)];
  assert.ok(p95 <= 30, `development-machine proxy p95 ${p95.toFixed(3)}ms exceeds 30ms`);
});
