import test from 'node:test';
import assert from 'node:assert/strict';

import { LocalEventLog, createMemoryStorage } from '../src/events.ts';
import { ACCEPTANCE_SEEDS } from '../src/levels.ts';
import { PhaseRippleSession } from '../src/session.ts';

test('a session logs start, the single click, result, retry, and next-level transitions', () => {
  const log = new LocalEventLog(createMemoryStorage(), 'session-events', () => 10);
  const session = new PhaseRippleSession(ACCEPTANCE_SEEDS.slice(0, 2), log);

  assert.equal(log.read()[0]?.type, 'level_start');
  const answer = session.level.referenceInput;
  while (session.state.time < answer.time) session.tick(1 / 120);
  session.click(answer);
  session.click({ x: answer.x + 20, y: answer.y + 20 });
  while (session.state.outcome === 'playing') session.tick(1 / 120);

  assert.equal(log.read().filter((event) => event.type === 'first_click').length, 1);
  assert.equal(log.read().filter((event) => event.type === 'level_result').length, 1);

  session.restart('tap');
  assert.equal(session.state.time, 0);
  assert.equal(session.state.click, null);
  assert.equal(log.read().at(-2)?.type, 'retry');
  assert.equal(log.read().at(-1)?.type, 'level_start');

  session.nextLevel();
  assert.equal(session.level.seed, ACCEPTANCE_SEEDS[1]);
  assert.equal(log.read().at(-2)?.type, 'next_level');
  assert.equal(log.read().at(-1)?.type, 'level_start');
});

test('background recovery restarts the current seed without retaining its ripple', () => {
  const log = new LocalEventLog(createMemoryStorage());
  const session = new PhaseRippleSession([ACCEPTANCE_SEEDS[0]], log);
  session.click({ x: 10, y: 10 });
  session.tick(0.5);

  session.recoverFromBackground();

  assert.equal(session.state.time, 0);
  assert.equal(session.state.click, null);
  assert.ok(session.state.pieces.every((piece) => !piece.reversed));
  assert.equal(log.read().at(-2)?.data.reason, 'background');
});
