import test from 'node:test';
import assert from 'node:assert/strict';

import { ACCEPTANCE_SEEDS } from '../src/levels.ts';
import { generateAcceptedLevel } from '../src/generator.ts';
import { auditSeeds, sampleSolutions } from '../src/solver.ts';

test('the 20 browser seeds have a finite continuous solution region and resist random clicks', () => {
  assert.equal(ACCEPTANCE_SEEDS.length, 20);

  for (const seed of ACCEPTANCE_SEEDS) {
    const level = generateAcceptedLevel(seed);
    const sample = sampleSolutions(level);

    assert.ok(sample.successCount > 0, `seed ${seed} has no solution`);
    assert.ok(sample.largestComponent >= 2, `seed ${seed} has no adjacent solution samples`);
    assert.ok(sample.successRate <= 0.15, `seed ${seed} random success rate is ${sample.successRate}`);
    assert.ok(sample.successRate < 0.5, `seed ${seed} solution covers most of the domain`);
  }
});

test('200 deterministic seeds pass the technical acceptance audit', () => {
  const report = auditSeeds(200);

  assert.equal(report.accepted, 200);
  assert.equal(report.unsolved, 0);
  assert.equal(report.nonContinuous, 0);
  assert.equal(report.tooEasy, 0);
  assert.equal(report.determinismFailures, 0);
  assert.equal(report.resetFailures, 0);
  assert.ok(report.maxSuccessRate <= 0.15);
});
