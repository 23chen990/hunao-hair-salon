import test from 'node:test';
import assert from 'node:assert/strict';

import { TutorialController } from '../src/onboarding.ts';

test('the tutorial starts directly with one guided interaction instead of an instruction modal', () => {
  const tutorial = new TutorialController(2);

  assert.equal(tutorial.lesson, 1);
  assert.equal(tutorial.lessonTotal, 2);
  assert.equal(tutorial.phase, 'guided');
  assert.equal(tutorial.tickAllowance(0.5, 0.25, 0.62), 0.12);
  assert.equal(tutorial.isWaitingForTarget(0.62, 0.62), true);
});

test('lesson one pauses on the reversal, then waits on the successful result', () => {
  const tutorial = new TutorialController(2);

  assert.equal(tutorial.completeTargetTap({ x: 47, y: -54 }, { x: 40, y: -60 }), true);
  assert.equal(tutorial.phase, 'ripple');
  assert.equal(tutorial.observeReversals(1), true);
  assert.equal(tutorial.phase, 'impact');
  assert.equal(tutorial.tickAllowance(1, 0.25, 0.62), 0);
  assert.equal(tutorial.continueAfterImpact(), true);
  assert.equal(tutorial.phase, 'ripple');
  assert.equal(tutorial.observeOutcome('success'), true);
  assert.equal(tutorial.phase, 'result');
});

test('lesson two freezes separately on the first and second reversal before completing onboarding', () => {
  const tutorial = new TutorialController(2);
  tutorial.completeTargetTap({ x: 0, y: 0 }, { x: 0, y: 0 });
  tutorial.observeReversals(1);
  tutorial.continueAfterImpact();
  tutorial.observeOutcome('success');

  assert.equal(tutorial.advanceLesson(), 'next');
  assert.equal(tutorial.lesson, 2);
  assert.equal(tutorial.phase, 'guided');

  tutorial.completeTargetTap({ x: 0, y: 0 }, { x: 0, y: 0 });
  assert.equal(tutorial.observeReversals(1), true);
  assert.equal(tutorial.impactIndex, 1);
  tutorial.continueAfterImpact();
  assert.equal(tutorial.observeReversals(2), true);
  assert.equal(tutorial.impactIndex, 2);
  tutorial.continueAfterImpact();
  tutorial.observeOutcome('success');

  assert.equal(tutorial.advanceLesson(), 'complete');
  assert.equal(tutorial.phase, 'complete');
});
