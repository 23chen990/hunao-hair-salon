import { createRng } from './prng.js';

const COLORS = ['#35c6d0', '#f0b84b', '#d26adf', '#6fd26f'];
const DECOY_LOCAL_SLOTS = [-2.5, -1.5, -0.5, 0.5, 1.5, 2.5];

function chooseDecoys(rng, count) {
  const candidates = [...DECOY_LOCAL_SLOTS];
  const chosen = [];
  while (chosen.length < count) {
    const index = rng.int(0, candidates.length - 1);
    chosen.push(candidates.splice(index, 1)[0]);
  }
  return chosen;
}

export function generateLevel(seed) {
  const rng = createRng(`level:${seed}`);
  const layerCount = rng.next() < 0.5 ? 3 : 4;
  const slotMin = -3;
  const slotMax = 3;
  const slotSpacing = 54;
  const beadRadius = 9;
  const holeRadius = 22;
  const maxDistance = layerCount * 2;
  const targetDistance = rng.int(4, maxDistance);
  const reverseDistances = Array(layerCount).fill(1);
  let remaining = targetDistance - layerCount;
  while (remaining > 0) {
    const index = rng.int(0, layerCount - 1);
    if (reverseDistances[index] === 1) {
      reverseDistances[index] += 1;
      remaining -= 1;
    }
  }

  const layers = [];
  const initialOffsets = [];
  for (let index = 0; index < layerCount; index += 1) {
    const solutionSlot = rng.int(-1, 1);
    const direction = rng.next() < 0.5 ? -1 : 1;
    initialOffsets.push(solutionSlot + direction * reverseDistances[index]);
    const decoyCount = rng.int(2, 3);
    const holes = [
      {
        id: `L${index + 1}-solution`,
        role: 'solution',
        localSlot: -solutionSlot,
        radius: holeRadius,
      },
      ...chooseDecoys(rng, decoyCount).map((localSlot, decoyIndex) => ({
        id: `L${index + 1}-decoy-${decoyIndex + 1}`,
        role: 'decoy',
        localSlot,
        radius: holeRadius,
      })),
    ];
    layers.push({
      id: `layer-${index + 1}`,
      color: COLORS[index],
      alpha: 0.3,
      solutionSlot,
      holes,
    });
  }

  return {
    version: 1,
    seed: String(seed),
    layerCount,
    slotMin,
    slotMax,
    slotSpacing,
    beadRadius,
    targetDistance,
    layers,
    initialOffsets,
  };
}

function validateOffsets(level, offsets) {
  if (!Array.isArray(offsets) || offsets.length !== level.layerCount) {
    throw new TypeError('offset count must match layer count');
  }
  for (const offset of offsets) {
    if (!Number.isInteger(offset) || offset < level.slotMin || offset > level.slotMax) {
      throw new RangeError(`offset ${offset} is outside legal slots`);
    }
  }
}

export function applyOffsetMove(level, offsets, move) {
  validateOffsets(level, offsets);
  if (!Number.isInteger(move.layer) || move.layer < 0 || move.layer >= level.layerCount) {
    throw new RangeError('layer index is outside the board');
  }
  if (move.delta !== -1 && move.delta !== 1) {
    throw new RangeError('moves must shift one adjacent slot');
  }
  const nextValue = offsets[move.layer] + move.delta;
  if (nextValue < level.slotMin || nextValue > level.slotMax) {
    throw new RangeError('move crosses the slot boundary');
  }
  const next = [...offsets];
  next[move.layer] = nextValue;
  return next;
}

export function dropBead(level, offsets) {
  validateOffsets(level, offsets);
  const passedLayerIndices = [];
  for (let index = 0; index < level.layers.length; index += 1) {
    const layer = level.layers[index];
    const offset = offsets[index];
    const holes = layer.holes.map((hole) => {
      const centerDistancePx = Math.abs((hole.localSlot + offset) * level.slotSpacing);
      const clearancePx = hole.radius - level.beadRadius - centerDistancePx;
      return { id: hole.id, role: hole.role, centerDistancePx, clearancePx };
    });
    if (!holes.some((hole) => hole.clearancePx >= 0)) {
      const nearestHole = holes.reduce((best, hole) => (
        hole.centerDistancePx < best.centerDistancePx ? hole : best
      ));
      return {
        passed: false,
        blockerIndex: index,
        passedLayerIndices,
        nearestHole,
      };
    }
    passedLayerIndices.push(index);
  }
  return {
    passed: true,
    blockerIndex: null,
    passedLayerIndices,
    nearestHole: null,
  };
}

export function createGameState(level) {
  return {
    version: 1,
    levelSeed: level.seed,
    offsets: [...level.initialOffsets],
    status: 'ready',
    moveCount: 0,
    dropCount: 0,
    consecutiveFailures: 0,
    lastDrop: null,
  };
}

export function moveGameState(level, state, move) {
  if (state.status !== 'ready') throw new Error('cannot move while result is unresolved');
  return {
    ...state,
    offsets: applyOffsetMove(level, state.offsets, move),
    moveCount: state.moveCount + 1,
    lastDrop: null,
  };
}

export function attemptDrop(level, state) {
  if (state.status !== 'ready') throw new Error('drop requires a ready state');
  const result = dropBead(level, state.offsets);
  return {
    ...state,
    status: result.passed ? 'passed' : 'blocked',
    dropCount: state.dropCount + 1,
    consecutiveFailures: result.passed ? 0 : state.consecutiveFailures + 1,
    lastDrop: result,
  };
}

export function retryLevel(state) {
  if (state.status !== 'blocked') throw new Error('retry requires a blocked state');
  return {
    ...state,
    status: 'ready',
    lastDrop: null,
  };
}

function canonicalize(value) {
  if (Array.isArray(value)) return value.map(canonicalize);
  if (value && typeof value === 'object') {
    const sorted = {};
    for (const key of Object.keys(value).sort()) sorted[key] = canonicalize(value[key]);
    return sorted;
  }
  return value;
}

export function serializeGameState(state) {
  return JSON.stringify(canonicalize(state));
}

export function deserializeGameState(serialized) {
  const state = JSON.parse(serialized);
  if (state?.version !== 1 || !Array.isArray(state.offsets)) {
    throw new TypeError('unsupported game state');
  }
  return state;
}

export function hashGameState(state) {
  const serialized = serializeGameState(state);
  let hash = 0x811c9dc5;
  for (let index = 0; index < serialized.length; index += 1) {
    hash ^= serialized.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}
