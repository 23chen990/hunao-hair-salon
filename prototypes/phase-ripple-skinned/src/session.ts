import type { LocalEventLog } from './events.ts';
import { generateAcceptedLevel } from './generator.ts';
import { DEFAULT_RULES, advanceState, createInitialState, issueClick } from './rules.ts';
import type { GameState, LevelDefinition, Point } from './types.ts';

function arrivalSpread(state: GameState): number | null {
  const arrivals = state.pieces
    .map((piece) => piece.arrivalTime)
    .filter((time): time is number => time !== null);
  if (arrivals.length !== state.pieces.length) return null;
  return Math.max(...arrivals) - Math.min(...arrivals);
}

export class PhaseRippleSession {
  private static readonly ROUNDS_PER_STAGE = 3;
  private readonly levelSources: readonly (number | LevelDefinition)[];
  private readonly log: LocalEventLog;
  private levelIndex = 0;
  private accumulator = 0;
  private resultLogged = false;
  private streak = 0;
  private attempts = 1;
  state: GameState;

  constructor(levelSources: readonly (number | LevelDefinition)[], log: LocalEventLog) {
    if (levelSources.length === 0) throw new Error('PhaseRippleSession requires at least one level');
    this.levelSources = [...levelSources];
    this.log = log;
    this.state = createInitialState(this.level);
    this.recordLevelStart();
  }

  get level(): LevelDefinition {
    const source = this.levelSources[this.levelIndex] as number | LevelDefinition;
    return typeof source === 'number' ? generateAcceptedLevel(source) : source;
  }

  get progress(): Readonly<{
    level: number;
    total: number;
    stage: number;
    stageTotal: number;
    round: number;
    roundTotal: number;
    stageComplete: boolean;
    streak: number;
    attempts: number;
  }> {
    const roundsPerStage = PhaseRippleSession.ROUNDS_PER_STAGE;
    const stageStart = Math.floor(this.levelIndex / roundsPerStage) * roundsPerStage;
    const roundTotal = Math.min(roundsPerStage, this.levelSources.length - stageStart);
    const round = this.levelIndex - stageStart + 1;
    return {
      level: this.levelIndex + 1,
      total: this.levelSources.length,
      stage: Math.floor(this.levelIndex / roundsPerStage) + 1,
      stageTotal: Math.ceil(this.levelSources.length / roundsPerStage),
      round,
      roundTotal,
      stageComplete: this.state.outcome === 'success' && round === roundTotal,
      streak: this.streak,
      attempts: this.attempts,
    };
  }

  click(point: Point): boolean {
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

  tick(frameSeconds: number): void {
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

  restart(reason: 'tap' | 'background' | 'manual' = 'manual'): void {
    this.log.record('retry', {
      seed: this.level.seed,
      reason,
      priorOutcome: this.state.outcome,
      priorTime: this.state.time,
    });
    if (this.state.outcome === 'boundary' || this.state.outcome === 'timeout') {
      this.streak = 0;
      this.attempts += 1;
    }
    this.resetCurrentLevel();
  }

  recoverFromBackground(): void {
    this.restart('background');
  }

  nextLevel(): void {
    const fromSeed = this.level.seed;
    if (this.state.outcome === 'success') this.streak += 1;
    this.levelIndex = (this.levelIndex + 1) % this.levelSources.length;
    this.attempts = 1;
    this.log.record('next_level', { fromSeed, toSeed: this.level.seed });
    this.resetCurrentLevel();
  }

  private resetCurrentLevel(): void {
    this.accumulator = 0;
    this.resultLogged = false;
    this.state = createInitialState(this.level);
    this.recordLevelStart();
  }

  private recordLevelStart(): void {
    this.log.record('level_start', {
      seed: this.level.seed,
      pieceCount: this.level.pieces.length,
      generationAttempt: this.level.generationAttempt,
    });
  }

  private recordResultOnce(): void {
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
