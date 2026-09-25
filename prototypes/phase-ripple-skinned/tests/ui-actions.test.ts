import test from 'node:test';
import assert from 'node:assert/strict';

import { actionAtPoint, resultActions } from '../src/ui-actions.ts';

test('a failed board exposes separate retry and optional hint reward actions', () => {
  const actions = resultActions(390, 844, 'timeout');
  assert.deepEqual(actions.map((entry) => entry.action), ['retry', 'hint']);

  for (const entry of actions) {
    const point = { x: entry.left + entry.width / 2, y: entry.top + entry.height / 2 };
    assert.equal(actionAtPoint(actions, point), entry.action);
  }
});

test('success keeps one unambiguous continue action', () => {
  const actions = resultActions(430, 932, 'success');
  assert.deepEqual(actions.map((entry) => entry.action), ['next']);
});
