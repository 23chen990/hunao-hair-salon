import assert from 'node:assert/strict';
import test from 'node:test';
import * as bridge from '../src/sim.mjs';

function required(name) {
  assert.equal(typeof bridge[name], 'function', `${name} must be exported`);
  return bridge[name];
}

test('a small audit exercises solver, timing noise, random control, stability, IAA, and pause contracts', () => {
  const runMachineAudit = required('runMachineAudit');
  const audit = runMachineAudit({
    seedCount: 12,
    robustnessTrials: 10,
    randomRuns: 120,
    stabilityRuns: 120,
    sessionCount: 24,
  });

  assert.equal(audit.schemaVersion, 1);
  assert.equal(audit.solver.denominator, 12);
  assert.equal(audit.robustness.trialsPerSeed, 10);
  assert.equal(audit.random.denominator, 120);
  assert.equal(audit.stability.denominator, 120);
  assert.equal(audit.pauseResume.networkRequests, 0);
  assert.ok(audit.performance.p50Ms <= audit.performance.p95Ms);
  assert.ok(audit.performance.p95Ms <= audit.performance.maxMs);
  assert.equal(audit.policy.adsEnabled, false);
  assert.equal(audit.policy.networkAllowed, false);
});

test('IAA eligibility obeys round/cooldown/failure/checkpoint/reward caps', () => {
  const evaluateIaa = required('evaluateIaa');
  const report = evaluateIaa(120);
  assert.equal(report.firstThreeInterstitialEligible, 0);
  assert.ok(report.interstitialPerSession.max <= 2);
  assert.equal(report.interstitialRules.rounds.join(','), '4,8');
  assert.ok(report.interstitialRules.cooldownSeconds >= 180);
  assert.ok(report.rewardEntriesPerSession.max <= 2);
});

