import test from 'node:test';
import assert from 'node:assert/strict';

import { LocalEventLog, createMemoryStorage } from '../src/events.ts';
import { generateAcceptedLevel } from '../src/generator.ts';
import { ACCEPTANCE_SEEDS, TUTORIAL_LEVELS } from '../src/levels.ts';
import { computeLayout } from '../src/layout.ts';
import { DEFAULT_RULES, simulateAttempt } from '../src/rules.ts';
import { PhaseRippleSession } from '../src/session.ts';

test('the slice keeps the verified 20 deterministic browser seeds', () => {
  assert.equal(ACCEPTANCE_SEEDS.length, 20);
  for (const seed of ACCEPTANCE_SEEDS) {
    const first = generateAcceptedLevel(seed);
    const second = generateAcceptedLevel(seed);
    assert.deepEqual(first, second);
    const result = simulateAttempt(first, first.referenceInput);
    assert.equal(result.outcome, 'success', `seed ${seed} reference click must solve`);
    assert.ok(result.arrivalSpread <= DEFAULT_RULES.syncWindow);
  }
});

test('two hand-authored lessons teach one concept at a time and remain real solvable boards', () => {
  assert.deepEqual(TUTORIAL_LEVELS.map((level) => level.pieces.length), [1, 2]);
  for (const level of TUTORIAL_LEVELS) {
    assert.equal(simulateAttempt(level, level.referenceInput).outcome, 'success');
  }
});

test('a session can advance through hand-authored tutorial levels without mixing them into seeds', () => {
  const session = new PhaseRippleSession(TUTORIAL_LEVELS, new LocalEventLog(createMemoryStorage()));
  assert.equal(session.level, TUTORIAL_LEVELS[0]);
  session.nextLevel();
  assert.equal(session.level, TUTORIAL_LEVELS[1]);
});

test('the real one-click rule still rejects a second click and reaches a result', () => {
  const log = new LocalEventLog(createMemoryStorage(), 'slice-events', () => 99);
  const session = new PhaseRippleSession([ACCEPTANCE_SEEDS[0]], log);
  const answer = session.level.referenceInput;
  while (session.state.time < answer.time) session.tick(DEFAULT_RULES.fixedDt);

  assert.equal(session.click(answer), true);
  assert.equal(session.click({ x: answer.x + 30, y: answer.y }), false);
  while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);

  assert.equal(session.state.outcome, 'success');
  assert.equal(log.read().filter((event) => event.type === 'first_click').length, 1);
  assert.equal(log.read().filter((event) => event.type === 'level_result').length, 1);
});

test('failure tap retries the same seed and success tap advances to the next seed', () => {
  const log = new LocalEventLog(createMemoryStorage());
  const session = new PhaseRippleSession(ACCEPTANCE_SEEDS.slice(0, 2), log);
  const firstSeed = session.level.seed;
  session.click({ x: -100, y: -100 });
  while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);
  assert.notEqual(session.state.outcome, 'success');

  session.restart('tap');
  assert.equal(session.level.seed, firstSeed);
  assert.equal(session.state.click, null);

  const answer = session.level.referenceInput;
  while (session.state.time < answer.time) session.tick(DEFAULT_RULES.fixedDt);
  session.click(answer);
  while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);
  assert.equal(session.state.outcome, 'success');
  session.nextLevel();
  assert.equal(session.level.seed, ACCEPTANCE_SEEDS[1]);
});

test('session progress turns outcomes into a visible short-term reward loop', () => {
  const log = new LocalEventLog(createMemoryStorage());
  const session = new PhaseRippleSession(ACCEPTANCE_SEEDS.slice(0, 3), log);

  assert.deepEqual(session.progress, {
    level: 1, total: 3, stage: 1, stageTotal: 1, round: 1, roundTotal: 3, stageComplete: false,
    streak: 0, attempts: 1,
  });

  const answer = session.level.referenceInput;
  while (session.state.time < answer.time) session.tick(DEFAULT_RULES.fixedDt);
  session.click(answer);
  while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);
  session.nextLevel();
  assert.deepEqual(session.progress, {
    level: 2, total: 3, stage: 1, stageTotal: 1, round: 2, roundTotal: 3, stageComplete: false,
    streak: 1, attempts: 1,
  });

  while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);
  assert.equal(session.state.outcome, 'boundary');
  session.restart('tap');
  assert.deepEqual(session.progress, {
    level: 2, total: 3, stage: 1, stageTotal: 1, round: 2, roundTotal: 3, stageComplete: false,
    streak: 0, attempts: 2,
  });
});

test('three short boards form one complete challenge stage', () => {
  const log = new LocalEventLog(createMemoryStorage());
  const session = new PhaseRippleSession(ACCEPTANCE_SEEDS.slice(0, 7), log);

  for (let index = 0; index < 3; index += 1) {
    const answer = session.level.referenceInput;
    while (session.state.time < answer.time) session.tick(DEFAULT_RULES.fixedDt);
    session.click(answer);
    while (session.state.outcome === 'playing') session.tick(DEFAULT_RULES.fixedDt);
    assert.equal(session.progress.round, index + 1);
    assert.equal(session.progress.stageComplete, index === 2);
    session.nextLevel();
  }

  assert.equal(session.progress.stage, 2);
  assert.equal(session.progress.round, 1);
  assert.equal(session.progress.stageTotal, 3);
});

for (const [width, height] of [[390, 844], [430, 932]] as const) {
  test(`${width}x${height} keeps title, circular arena, and result copy in the generated plate`, () => {
    const layout = computeLayout(width, height);
    assert.ok(layout.header.top >= layout.safeInset);
    assert.ok(layout.arena.left >= 0 && layout.arena.right <= width);
    assert.ok(layout.arena.top > layout.header.bottom);
    assert.ok(layout.controls.top >= layout.arena.bottom);
    assert.ok(layout.controls.bottom <= height - layout.safeInset);
    assert.ok(layout.arena.size >= width * 0.92);
  });
}
