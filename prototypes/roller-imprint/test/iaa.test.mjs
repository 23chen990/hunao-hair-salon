import test from 'node:test';
import assert from 'node:assert/strict';
import { applyRoll, createGame, stateHash } from '../src/model.mjs';
import { generateLevel } from '../src/solver.mjs';

let iaa = {};
try {
  iaa = await import('../src/iaa.mjs');
} catch {
  // Expected RED until the local-only eligibility proxy exists.
}

function need(name) {
  assert.equal(typeof iaa[name], 'function', `${name} implementation is missing`);
  return iaa[name];
}

test('local ad policy defaults off, forbids requests, and caps voluntary entries at two', () => {
  const createLocalAdPolicy = need('createLocalAdPolicy');
  assert.deepEqual(createLocalAdPolicy(), {
    enabled: false,
    networkAllowed: false,
    networkRequests: 0,
    voluntaryRewardLimit: 2,
  });
});

test('simulated ad freezes rolls, tape, and clock then restores the exact hash', () => {
  const beginAdSimulation = need('beginAdSimulation');
  const endAdSimulation = need('endAdSimulation');
  const level = generateLevel(4);
  const before = createGame({ seed: level.seed, target: level.target });
  const hashBefore = stateHash(before);
  const paused = beginAdSimulation(before);
  const attempted = applyRoll(paused, level.solution[0]);
  assert.equal(attempted, paused, 'roll must be ignored while paused');
  assert.equal(attempted.clockMs, before.clockMs, 'clock must not advance while paused');
  const resumed = endAdSimulation(attempted);
  assert.equal(stateHash(resumed), hashBefore);
});

test('no-ad regression completes and freely restarts every sampled seed', () => {
  const runNoAdRegression = need('runNoAdRegression');
  const result = runNoAdRegression(64);
  assert.equal(result.completed, 64);
  assert.equal(result.restarted, 64);
  assert.equal(result.blocked, 0);
});

test('round-length and voluntary eligibility proxies stay inside declared bands', () => {
  const analyzeIaaSeeds = need('analyzeIaaSeeds');
  const metrics = analyzeIaaSeeds(128);
  assert.ok(metrics.roundSeconds.median >= 45 && metrics.roundSeconds.median <= 80);
  assert.ok(metrics.roundSeconds.p10 >= 30);
  assert.ok(metrics.contactHint.rate >= 0.15 && metrics.contactHint.rate <= 0.35);
  assert.ok(metrics.singlePassUndo.rate >= 0.10 && metrics.singlePassUndo.rate <= 0.25);
  assert.ok(metrics.maxVoluntaryEntriesPerSession <= 2);
});

test('7–11 minute sessions have one or two settlement-only points after three ad-free rounds', () => {
  const simulateSession = need('simulateSession');
  for (const seconds of [420, 540, 660]) {
    const session = simulateSession(7000 + seconds, seconds);
    assert.deepEqual(session.rounds.slice(0, 3).map((round) => round.interstitialEligible), [
      false,
      false,
      false,
    ]);
    assert.ok(session.interstitialPoints.length >= 1 && session.interstitialPoints.length <= 2);
    assert.ok(session.cooldownSeconds >= 180 && session.cooldownSeconds <= 300);
    assert.ok(session.interstitialPoints.every((point) => point.atSettlement));
  }
});
