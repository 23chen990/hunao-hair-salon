import test from 'node:test';
import assert from 'node:assert/strict';

async function optionalModule(path) {
  try {
    return await import(path);
  } catch {
    return null;
  }
}

const simulation = await optionalModule('../src/simulation.js');

test('500-seed duration proxy stays inside the declared 30–75 second structure', () => {
  assert.ok(simulation, 'simulation implementation is missing');
  const result = simulation.auditDurations(500, 10_001);
  assert.ok(result.medianMs >= 45_000 && result.medianMs <= 75_000, `${result.medianMs}ms median`);
  assert.ok(result.p10Ms >= 30_000, `${result.p10Ms}ms p10`);
});

test('10,000 uniformly sampled legal layouts do not pass by random shifting more than 15%', () => {
  assert.ok(simulation, 'simulation implementation is missing');
  const result = simulation.auditRandomSuccess(10_000, 20_001);
  assert.ok(result.rate <= 0.15, `${result.successes}/${result.samples} = ${result.rate}`);
});

test('bounded-noise novice proxy produces honest hint and diagnostic eligibility bands', () => {
  assert.ok(simulation, 'simulation implementation is missing');
  const result = simulation.auditRewardEligibility(10_000, 30_001);
  assert.ok(result.hintRate >= 0.15 && result.hintRate <= 0.35, `hint=${result.hintRate}`);
  assert.ok(result.diagnosticRate >= 0.10 && result.diagnosticRate <= 0.30, `diagnostic=${result.diagnosticRate}`);
});
