import test from 'node:test';
import assert from 'node:assert/strict';

import { generateAcceptedLevel } from '../src/generator.ts';
import { TUTORIAL_LEVELS } from '../src/levels.ts';
import { computeLayout } from '../src/layout.ts';
import { advanceState, createInitialState, issueClick } from '../src/rules.ts';
import { canvasPointToModel, renderGame } from '../src/render.ts';

test('the generated plate center maps to the pure-rule origin at both viewports', () => {
  for (const [width, height] of [[390, 844], [430, 932]] as const) {
    const layout = computeLayout(width, height);
    const model = canvasPointToModel(
      layout,
      (layout.arena.left + layout.arena.right) / 2,
      (layout.arena.top + layout.arena.bottom) / 2,
    );
    assert.ok(Math.abs(model.x) < 1e-9);
    assert.ok(Math.abs(model.y) < 1e-9);
  }
});

test('the renderer uses the generated board and token bitmaps while keeping copy and ripple live', () => {
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
  const level = generateAcceptedLevel(17);
  const state = issueClick(createInitialState(level), level.referenceInput);
  const fakeImage = {} as HTMLImageElement;

  renderGame(context, state, 390, 844, {
    board: fakeImage,
    outboundPiece: fakeImage,
    returnedPiece: fakeImage,
  });

  assert.ok(calls.filter((call) => call === 'drawImage').length >= level.pieces.length + 1);
  assert.ok(calls.includes('fillText'), 'engine text must not be baked into the board');
  assert.ok(calls.includes('arc'), 'click origin and ripple radius must be live geometry');
  assert.ok(calls.includes('rotate'), 'piece direction must follow the rule state');
});

test('the first lesson exposes one goal and one guided action without an instruction modal', () => {
  const text: string[] = [];
  const context = new Proxy({} as CanvasRenderingContext2D, {
    get: (target, property) => {
      if (property in target) return target[property as keyof CanvasRenderingContext2D];
      if (property === 'fillText') return (value: string) => { text.push(value); };
      return (..._arguments: unknown[]) => {};
    },
    set: (target, property, value) => {
      Object.defineProperty(target, property, { value, writable: true, configurable: true });
      return true;
    },
  });
  const state = createInitialState(TUTORIAL_LEVELS[0]);
  const fakeImage = {} as HTMLImageElement;

  renderGame(context, state, 390, 844, {
    board: fakeImage,
    outboundPiece: fakeImage,
    returnedPiece: fakeImage,
  }, {
    progress: {
      level: 1, total: 20, stage: 1, stageTotal: 7, round: 1, roundTotal: 3,
      stageComplete: false, streak: 0, attempts: 1,
    },
    tutorial: {
      phase: 'guided', waitingForTarget: true, lesson: 1, lessonTotal: 2, impactIndex: 0,
    },
    hintActive: false,
    animationTime: 1,
  });

  assert.ok(text.includes('归心'));
  assert.ok(text.some((value) => value.includes('教学 1 / 2')));
  assert.ok(text.includes('点一下，看看会发生什么'));
  assert.equal(text.includes('玩法只做一件事'), false);
});

test('the impact tutorial freezes on a reversed piece and names the cause and effect', () => {
  const text: string[] = [];
  const context = new Proxy({} as CanvasRenderingContext2D, {
    get: (target, property) => {
      if (property in target) return target[property as keyof CanvasRenderingContext2D];
      if (property === 'fillText') return (value: string) => { text.push(value); };
      return (..._arguments: unknown[]) => {};
    },
    set: (target, property, value) => {
      Object.defineProperty(target, property, { value, writable: true, configurable: true });
      return true;
    },
  });
  const level = generateAcceptedLevel(17);
  let state = issueClick(createInitialState(level), level.referenceInput);
  while (state.triggerOrder.length === 0) state = advanceState(state, 1 / 120);

  renderGame(context, state, 390, 844, {
    board: {} as HTMLImageElement,
    outboundPiece: {} as HTMLImageElement,
    returnedPiece: {} as HTMLImageElement,
  }, {
    progress: {
      level: 1, total: 20, stage: 1, stageTotal: 7, round: 1, roundTotal: 3,
      stageComplete: false, streak: 0, attempts: 1,
    },
    tutorial: {
      phase: 'impact', waitingForTarget: false, lesson: 1, lessonTotal: 2, impactIndex: 1,
    },
    hintActive: false,
    animationTime: 1,
  });

  assert.ok(text.includes('它掉头了！'));
  assert.ok(text.includes('涟漪碰到棋子 → 棋子立刻掉头'));
  assert.ok(text.some((value) => value.includes('教学 1 / 2')));
});

test('the second lesson explains ordered reversals without reintroducing every rule at once', () => {
  const text: string[] = [];
  const context = new Proxy({} as CanvasRenderingContext2D, {
    get: (target, property) => {
      if (property in target) return target[property as keyof CanvasRenderingContext2D];
      if (property === 'fillText') return (value: string) => { text.push(value); };
      return (..._arguments: unknown[]) => {};
    },
    set: (target, property, value) => {
      Object.defineProperty(target, property, { value, writable: true, configurable: true });
      return true;
    },
  });
  let state = issueClick(createInitialState(TUTORIAL_LEVELS[1]), TUTORIAL_LEVELS[1].referenceInput);
  while (state.triggerOrder.length < 2) state = advanceState(state, 1 / 120);

  renderGame(context, state, 390, 844, {
    board: {} as HTMLImageElement,
    outboundPiece: {} as HTMLImageElement,
    returnedPiece: {} as HTMLImageElement,
  }, {
    progress: {
      level: 1, total: 20, stage: 1, stageTotal: 7, round: 1, roundTotal: 3,
      stageComplete: false, streak: 0, attempts: 1,
    },
    tutorial: {
      phase: 'impact', waitingForTarget: false, lesson: 2, lessonTotal: 2, impactIndex: 2,
    },
    hintActive: false,
    animationTime: 1,
  });

  assert.ok(text.some((value) => value.includes('教学 2 / 2')));
  assert.ok(text.includes('② 第二枚后掉头'));
  assert.ok(text.includes('点的位置，决定涟漪碰撞的先后'));
});
