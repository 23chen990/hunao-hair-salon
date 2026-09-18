const TARGET_RADIUS = 24;
const EPSILON = 1e-6;

class TutorialController {
           lessonTotal        ;
  lesson = 1;
  phase                = 'guided';
  impactIndex = 0;

  constructor(lessonTotal = 2) {
    if (!Number.isInteger(lessonTotal) || lessonTotal < 1) {
      throw new Error('TutorialController requires at least one lesson');
    }
    this.lessonTotal = lessonTotal;
  }

  tickAllowance(currentTime        , requestedSeconds        , targetTime        )         {
    if (this.phase === 'impact' || this.phase === 'result' || this.phase === 'complete') return 0;
    if (this.phase === 'guided') {
      return Math.max(0, Math.min(requestedSeconds, targetTime - currentTime));
    }
    return requestedSeconds;
  }

  isWaitingForTarget(currentTime        , targetTime        )          {
    return this.phase === 'guided' && currentTime + EPSILON >= targetTime;
  }

  completeTargetTap(point       , target       )          {
    if (this.phase !== 'guided') return false;
    if (Math.hypot(point.x - target.x, point.y - target.y) > TARGET_RADIUS) return false;
    this.phase = 'ripple';
    return true;
  }

  observeReversals(reversedCount        )          {
    if (this.phase !== 'ripple' || reversedCount <= this.impactIndex) return false;
    this.impactIndex = reversedCount;
    this.phase = 'impact';
    return true;
  }

  continueAfterImpact()          {
    if (this.phase !== 'impact') return false;
    this.phase = 'ripple';
    return true;
  }

  observeOutcome(outcome                )          {
    if (this.phase !== 'ripple' || outcome === 'playing') return false;
    this.phase = 'result';
    return true;
  }

  advanceLesson()                             {
    if (this.phase !== 'result') return null;
    if (this.lesson >= this.lessonTotal) {
      this.phase = 'complete';
      return 'complete';
    }
    this.lesson += 1;
    this.impactIndex = 0;
    this.phase = 'guided';
    return 'next';
  }
}

module.exports.TutorialController = TutorialController;
