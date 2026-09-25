                                                 
import { generateAcceptedLevel } from './generator.js';
import { DEFAULT_RULES, advanceState, createInitialState, issueClick } from './rules.js';
                                                                    

function arrivalSpread(state           )                {
  const arrivals = state.pieces
    .map((piece) => piece.arrivalTime)
    .filter((time)                 => time !== null);
  if (arrivals.length !== state.pieces.length) return null;
  return Math.max(...arrivals) - Math.min(...arrivals);
}

export class PhaseRippleSession {
                   seeds                   ;
                   log               ;
          levelIndex = 0;
          accumulator = 0;
          resultLogged = false;
  state           ;

  constructor(seeds                   , log               ) {
    if (seeds.length === 0) throw new Error('PhaseRippleSession requires at least one seed');
    this.seeds = [...seeds];
    this.log = log;
    this.state = createInitialState(this.level);
    this.recordLevelStart();
  }

  get level()                  {
    return generateAcceptedLevel(this.seeds[this.levelIndex]          );
  }

  click(point       )          {
    const before = this.state;
    this.state = issueClick(this.state, point);
    if (before === this.state) return false;
    this.log.record('first_click', {
      seed: this.level.seed,
      x: point.x,
      y: point.y,
      time: this.state.click?.time ?? this.state.time,
    });
    return true;
  }

  tick(frameSeconds        )       {
    if (!Number.isFinite(frameSeconds) || frameSeconds <= 0) return;
    this.accumulator += Math.min(frameSeconds, 0.25);
    while (this.accumulator + 1e-10 >= DEFAULT_RULES.fixedDt) {
      this.state = advanceState(this.state, DEFAULT_RULES.fixedDt);
      this.accumulator -= DEFAULT_RULES.fixedDt;
      if (this.state.outcome !== 'playing') {
        this.recordResultOnce();
        this.accumulator = 0;
        break;
      }
    }
  }

  restart(reason                                  = 'manual')       {
    this.log.record('retry', {
      seed: this.level.seed,
      reason,
      priorOutcome: this.state.outcome,
      priorTime: this.state.time,
    });
    this.resetCurrentLevel();
  }

  recoverFromBackground()       {
    this.restart('background');
  }

  nextLevel()       {
    const fromSeed = this.level.seed;
    this.levelIndex = (this.levelIndex + 1) % this.seeds.length;
    this.log.record('next_level', { fromSeed, toSeed: this.level.seed });
    this.resetCurrentLevel();
  }

          resetCurrentLevel()       {
    this.accumulator = 0;
    this.resultLogged = false;
    this.state = createInitialState(this.level);
    this.recordLevelStart();
  }

          recordLevelStart()       {
    this.log.record('level_start', {
      seed: this.level.seed,
      pieceCount: this.level.pieces.length,
      generationAttempt: this.level.generationAttempt,
    });
  }

          recordResultOnce()       {
    if (this.resultLogged) return;
    this.resultLogged = true;
    this.log.record('level_result', {
      seed: this.level.seed,
      outcome: this.state.outcome,
      click: this.state.click,
      triggerOrder: [...this.state.triggerOrder],
      spread: arrivalSpread(this.state),
      resultTime: this.state.resultTime,
    });
  }
}
