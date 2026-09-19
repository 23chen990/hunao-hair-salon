import type {
  AttemptResult,
  ClickInput,
  GameState,
  LevelDefinition,
  PieceState,
  Point,
} from './types.ts';

export const DEFAULT_RULES = Object.freeze({
  arenaRadius: 150,
  pieceRadius: 7,
  rippleSpeed: 260,
  syncWindow: 0.8,
  fixedDt: 1 / 120,
  maxDuration: 8,
});

const EPSILON = 1e-9;

export function createInitialState(level: LevelDefinition): GameState {
  return {
    level,
    time: 0,
    click: null,
    pieces: level.pieces.map((piece): PieceState => ({
      id: piece.id,
      angle: piece.angle,
      radius: piece.startRadius,
      speed: piece.speed,
      reversed: false,
      triggerTime: null,
      arrivalTime: null,
    })),
    triggerOrder: [],
    firstArrival: null,
    outcome: 'playing',
    resultTime: null,
  };
}

export function issueClick(state: GameState, point: Point): GameState {
  if (state.outcome !== 'playing' || state.click !== null) return state;
  return {
    ...state,
    click: { x: point.x, y: point.y, time: state.time },
  };
}

function distanceToClick(piece: PieceState, radius: number, click: ClickInput): number {
  const x = Math.cos(piece.angle) * radius;
  const y = Math.sin(piece.angle) * radius;
  return Math.hypot(x - click.x, y - click.y);
}

function finishState(
  state: GameState,
  time: number,
  pieces: readonly PieceState[],
  triggerOrder: readonly number[],
  firstArrival: number | null,
  outcome: 'success' | 'timeout' | 'boundary',
): GameState {
  return {
    ...state,
    time,
    pieces,
    triggerOrder,
    firstArrival,
    outcome,
    resultTime: time,
  };
}

export function advanceState(state: GameState, dt: number): GameState {
  if (state.outcome !== 'playing' || dt <= 0) return state;

  const targetTime = state.time + dt;
  const triggerOrder = [...state.triggerOrder];
  let boundaryHit = false;
  let firstArrival = state.firstArrival;

  const pieces = state.pieces.map((piece): PieceState => {
    if (piece.arrivalTime !== null) return piece;

    if (piece.reversed) {
      const nextRadius = piece.radius - piece.speed * dt;
      if (nextRadius <= EPSILON) {
        const arrived = { ...piece, radius: 0, arrivalTime: targetTime };
        firstArrival ??= targetTime;
        return arrived;
      }
      return { ...piece, radius: nextRadius };
    }

    const nextRadius = piece.radius + piece.speed * dt;
    if (state.click !== null && targetTime >= state.click.time) {
      const waveRadius = DEFAULT_RULES.rippleSpeed * (targetTime - state.click.time);
      if (waveRadius + EPSILON >= distanceToClick(piece, nextRadius, state.click)) {
        triggerOrder.push(piece.id);
        return {
          ...piece,
          radius: nextRadius,
          reversed: true,
          triggerTime: targetTime,
        };
      }
    }

    if (nextRadius + DEFAULT_RULES.pieceRadius >= DEFAULT_RULES.arenaRadius) {
      boundaryHit = true;
    }
    return { ...piece, radius: nextRadius };
  });

  if (boundaryHit) {
    return finishState(state, targetTime, pieces, triggerOrder, firstArrival, 'boundary');
  }

  const arrivals = pieces
    .map((piece) => piece.arrivalTime)
    .filter((time): time is number => time !== null);
  if (arrivals.length === pieces.length) {
    const spread = Math.max(...arrivals) - Math.min(...arrivals);
    const outcome = spread <= DEFAULT_RULES.syncWindow + EPSILON ? 'success' : 'timeout';
    return finishState(state, targetTime, pieces, triggerOrder, firstArrival, outcome);
  }

  if (
    (firstArrival !== null && targetTime - firstArrival > DEFAULT_RULES.syncWindow + EPSILON) ||
    targetTime >= DEFAULT_RULES.maxDuration
  ) {
    return finishState(state, targetTime, pieces, triggerOrder, firstArrival, 'timeout');
  }

  return {
    ...state,
    time: targetTime,
    pieces,
    triggerOrder,
    firstArrival,
  };
}

function rounded(value: number): number {
  return Number(value.toFixed(6));
}

export function simulateAttempt(level: LevelDefinition, input: ClickInput): AttemptResult {
  let state = createInitialState(level);
  let guard = Math.ceil(DEFAULT_RULES.maxDuration / DEFAULT_RULES.fixedDt) + 2;
  while (state.outcome === 'playing' && guard > 0) {
    if (state.click === null && state.time + EPSILON >= input.time) {
      state = issueClick(state, input);
    }
    state = advanceState(state, DEFAULT_RULES.fixedDt);
    guard -= 1;
  }

  const triggerTimes = state.pieces
    .map((piece) => piece.triggerTime)
    .filter((time): time is number => time !== null);
  const arrivalTimes = state.pieces
    .map((piece) => piece.arrivalTime)
    .filter((time): time is number => time !== null);
  const spread = arrivalTimes.length > 1
    ? Math.max(...arrivalTimes) - Math.min(...arrivalTimes)
    : Number.POSITIVE_INFINITY;

  return {
    outcome: state.outcome === 'playing' ? 'timeout' : state.outcome,
    triggerOrder: [...state.triggerOrder],
    triggerTimes: triggerTimes.map(rounded),
    arrivalTimes: arrivalTimes.map(rounded),
    arrivalSpread: Number.isFinite(spread) ? rounded(spread) : spread,
    resultTime: rounded(state.resultTime ?? state.time),
  };
}

export function snapshotState(state: GameState): unknown {
  return {
    seed: state.level.seed,
    time: rounded(state.time),
    click: state.click === null ? null : {
      x: rounded(state.click.x),
      y: rounded(state.click.y),
      time: rounded(state.click.time),
    },
    pieces: state.pieces.map((piece) => ({
      id: piece.id,
      radius: rounded(piece.radius),
      reversed: piece.reversed,
      triggerTime: piece.triggerTime === null ? null : rounded(piece.triggerTime),
      arrivalTime: piece.arrivalTime === null ? null : rounded(piece.arrivalTime),
    })),
    triggerOrder: [...state.triggerOrder],
    outcome: state.outcome,
  };
}

export function evaluateAttemptAnalytic(
  level: LevelDefinition,
  input: ClickInput,
): AttemptResult {
  const rippleSpeedSquared = DEFAULT_RULES.rippleSpeed ** 2;
  const limit = DEFAULT_RULES.arenaRadius - DEFAULT_RULES.pieceRadius;
  const triggers: Array<{ id: number; trigger: number; arrival: number }> = [];

  for (const piece of level.pieces) {
    const boundaryTime = (limit - piece.startRadius) / piece.speed;
    if (input.time >= boundaryTime) {
      return {
        outcome: 'boundary', triggerOrder: [], triggerTimes: [], arrivalTimes: [],
        arrivalSpread: Number.POSITIVE_INFINITY, resultTime: rounded(boundaryTime),
      };
    }

    const ux = Math.cos(piece.angle);
    const uy = Math.sin(piece.angle);
    const radiusAtClick = piece.startRadius + piece.speed * input.time;
    const dx = ux * radiusAtClick - input.x;
    const dy = uy * radiusAtClick - input.y;
    const a = piece.speed ** 2 - rippleSpeedSquared;
    const b = 2 * piece.speed * (dx * ux + dy * uy);
    const c = dx * dx + dy * dy;
    const discriminant = Math.max(0, b * b - 4 * a * c);
    const tau = (-b - Math.sqrt(discriminant)) / (2 * a);
    const trigger = input.time + Math.max(0, tau);
    if (!Number.isFinite(trigger) || trigger >= boundaryTime) {
      return {
        outcome: 'boundary', triggerOrder: [], triggerTimes: [], arrivalTimes: [],
        arrivalSpread: Number.POSITIVE_INFINITY, resultTime: rounded(boundaryTime),
      };
    }
    triggers.push({
      id: piece.id,
      trigger,
      arrival: piece.startRadius / piece.speed + 2 * trigger,
    });
  }

  const arrivalTimes = triggers.map((entry) => entry.arrival);
  const firstArrival = Math.min(...arrivalTimes);
  const lastArrival = Math.max(...arrivalTimes);
  const spread = lastArrival - firstArrival;
  const triggerOrder = [...triggers].sort((left, right) =>
    left.trigger - right.trigger || left.id - right.id,
  );

  return {
    outcome: spread <= DEFAULT_RULES.syncWindow + EPSILON ? 'success' : 'timeout',
    triggerOrder: triggerOrder.map((entry) => entry.id),
    triggerTimes: triggers.map((entry) => rounded(entry.trigger)),
    arrivalTimes: arrivalTimes.map(rounded),
    arrivalSpread: rounded(spread),
    resultTime: rounded(spread <= DEFAULT_RULES.syncWindow ? lastArrival : firstArrival + DEFAULT_RULES.syncWindow),
  };
}
