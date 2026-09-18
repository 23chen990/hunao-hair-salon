import test from 'node:test';
import assert from 'node:assert/strict';

import { generateLevel } from '../src/generator.ts';
import { computeLayout } from '../src/layout.ts';
import { createInitialState } from '../src/rules.ts';
import { canvasPointToModel, renderGame } from '../src/render.ts';

test('the visual center maps to the rule-layer origin at both acceptance viewports', () => {
  for (const [width, height] of [[390, 844], [430, 932]] as const) {
    const layout = computeLayout(width, height);
    const point = canvasPointToModel(
      layout,
      (layout.arena.left + layout.arena.right) / 2,
      (layout.arena.top + layout.arena.bottom) / 2,
    );
    assert.ok(Math.abs(point.x) < 1e-9);
    assert.ok(Math.abs(point.y) < 1e-9);
  }
});

test('the renderer draws the boundary, target, and every generated piece using only canvas calls', () => {
  const calls: string[] = [];
  const context = new Proxy({} as CanvasRenderingContext2D, {
    get: (target, property) => {
      if (property in target) return target[property as keyof CanvasRenderingContext2D];
      return (..._arguments: unknown[]) => { calls.push(String(property)); };
    },
    set: (target, property, value) => {
      Object.defineProperty(target, property, { value, writable: true, configurable: true });
      return true;
    },
  });
  const level = generateLevel(17);
  const state = createInitialState(level);

  renderGame(context, state, 390, 844);

  assert.ok(calls.filter((call) => call === 'arc').length >= level.pieces.length + 2);
  assert.ok(calls.includes('fillText'));
  assert.ok(calls.includes('stroke'));
});
