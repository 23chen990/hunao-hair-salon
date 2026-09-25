import test from 'node:test';
import assert from 'node:assert/strict';

import { LocalEventLog, createMemoryStorage } from '../src/events.ts';
import { computeLayout } from '../src/layout.ts';

test('the local log records the required event sequence without networking', () => {
  const storage = createMemoryStorage();
  const log = new LocalEventLog(storage, 'test-events', () => 1234);

  log.record('level_start', { seed: 1 });
  log.record('first_click', { seed: 1, x: 12, y: -4, time: 0.7 });
  log.record('level_result', { seed: 1, outcome: 'success', spread: 0.2 });
  log.record('retry', { seed: 1 });
  log.record('next_level', { fromSeed: 1, toSeed: 2 });

  assert.deepEqual(log.read().map((event) => event.type), [
    'level_start',
    'first_click',
    'level_result',
    'retry',
    'next_level',
  ]);
  assert.equal(log.read()[0]?.at, 1234);
});

for (const [width, height] of [[390, 844], [430, 932]] as const) {
  test(`${width}x${height} keeps the arena and controls inside the safe viewport`, () => {
    const layout = computeLayout(width, height);

    assert.ok(layout.arena.left >= layout.safeInset);
    assert.ok(layout.arena.right <= width - layout.safeInset);
    assert.ok(layout.arena.top >= layout.header.bottom);
    assert.ok(layout.controls.bottom <= height - layout.safeInset);
    assert.ok(layout.controls.top >= layout.arena.bottom);
    assert.ok(layout.arena.size >= 300);
  });
}
