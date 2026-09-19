import test from 'node:test';
import assert from 'node:assert/strict';

async function optionalModule(path) {
  try {
    return await import(path);
  } catch {
    return null;
  }
}

const eligibility = await optionalModule('../src/eligibility.js');
const model = await optionalModule('../src/model.js');

test('interstitial eligibility is zero for rounds 1–3 and only appears at settlement after cooldown', () => {
  assert.ok(eligibility, 'eligibility implementation is missing');
  let session = eligibility.createEligibilitySession({ placeholdersEnabled: true });
  const opportunities = [];
  for (let round = 1; round <= 10; round += 1) {
    const result = eligibility.completeRound(session, 60_000);
    session = result.session;
    if (result.events.length) opportunities.push({ round, ...result.events[0] });
  }
  assert.deepEqual(opportunities.map((entry) => entry.round), [4, 7]);
  assert.ok(opportunities.every((entry) => entry.page === 'settlement'));
  assert.ok(opportunities.every((entry) => entry.type === 'interstitial_eligible'));
});

test('8–12 minute sessions create 1–2 natural interstitial points, never one every round', () => {
  assert.ok(eligibility, 'eligibility implementation is missing');
  for (const minutes of [8, 9, 10, 11, 12]) {
    let session = eligibility.createEligibilitySession({ placeholdersEnabled: true });
    let count = 0;
    const rounds = minutes;
    for (let round = 0; round < rounds; round += 1) {
      const result = eligibility.completeRound(session, 60_000);
      session = result.session;
      count += result.events.length;
    }
    assert.ok(count >= 1 && count <= 2, `${minutes} minutes produced ${count}`);
    assert.ok(count < rounds);
  }
});

test('hint requires two consecutive real failures and diagnostic requires a chosen late blocker detail', () => {
  assert.ok(eligibility, 'eligibility implementation is missing');
  assert.ok(model, 'model implementation is missing');
  const level = model.generateLevel(81);
  let state = model.createGameState(level);
  state = model.attemptDrop(level, state);
  assert.equal(eligibility.rewardEligibility(level, state, { viewDetails: false }).hint, false);
  state = model.retryLevel(state);
  state = model.attemptDrop(level, state);
  const second = eligibility.rewardEligibility(level, state, { viewDetails: false });
  assert.equal(second.hint, true);
  assert.equal(second.diagnostic, false);

  const offsets = level.layers.map((layer) => layer.solutionSlot);
  const lateIndex = Math.ceil(level.layerCount / 2);
  offsets[lateIndex] = offsets[lateIndex] === level.slotMax ? offsets[lateIndex] - 1 : offsets[lateIndex] + 1;
  const lateState = model.attemptDrop(level, { ...model.createGameState(level), offsets });
  assert.equal(eligibility.rewardEligibility(level, lateState, { viewDetails: false }).diagnostic, false);
  assert.equal(eligibility.rewardEligibility(level, lateState, { viewDetails: true }).diagnostic, true);
});

test('disabled placeholders never block core flow and simulated ads pause only the logic clock', () => {
  assert.ok(eligibility, 'eligibility implementation is missing');
  assert.ok(model, 'model implementation is missing');
  let disabled = eligibility.createEligibilitySession({ placeholdersEnabled: false });
  for (let round = 0; round < 12; round += 1) {
    const result = eligibility.completeRound(disabled, 60_000);
    disabled = result.session;
    assert.deepEqual(result.events, []);
  }

  const gameplay = model.createGameState(model.generateLevel(91));
  const beforeHash = model.hashGameState(gameplay);
  let enabled = eligibility.createEligibilitySession({ placeholdersEnabled: true });
  enabled = eligibility.advanceClock(enabled, 5_000);
  enabled = eligibility.beginAdSimulation(enabled, 'rewarded');
  enabled = eligibility.advanceClock(enabled, 45_000);
  assert.equal(enabled.wallClockMs, 50_000);
  assert.equal(enabled.gameClockMs, 5_000);
  enabled = eligibility.endAdSimulation(enabled);
  assert.equal(model.hashGameState(gameplay), beforeHash);
});

test('a session emits at most two optional rewarded-entry eligibility events', () => {
  assert.ok(eligibility, 'eligibility implementation is missing');
  assert.equal(typeof eligibility.registerRewardEntry, 'function', 'reward entry cap is missing');
  let session = eligibility.createEligibilitySession({ placeholdersEnabled: true });
  const emitted = [];
  for (const type of ['hint', 'diagnostic', 'hint']) {
    const result = eligibility.registerRewardEntry(session, type);
    session = result.session;
    emitted.push(...result.events);
  }
  assert.deepEqual(emitted.map((event) => event.type), ['hint_eligible', 'diagnostic_eligible']);
  assert.equal(session.rewardEligibilityCount, 2);
});
