import { rollMask, TAPE_COLUMNS } from './model.mjs';

const ACTIONS = Object.freeze(
  [-1, 1].flatMap((direction) => (
    Array.from({ length: 8 }, (_, face) => (
      Array.from({ length: TAPE_COLUMNS }, (_unused, index) => Object.freeze({
        direction,
        distance: index + 1,
        face,
      }))
    )).flat()
  )),
);

const ACTION_MASKS = ACTIONS.map((action) => ({
  action,
  mask: boardToMask(rollMask(action)),
}));

const LEVEL_CACHE = new Map();

function mulberry32(seed) {
  let value = seed >>> 0;
  return () => {
    value = (value + 0x6d2b79f5) >>> 0;
    let mixed = value;
    mixed = Math.imul(mixed ^ (mixed >>> 15), mixed | 1);
    mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4294967296;
  };
}

function randomInt(rng, low, highInclusive) {
  return low + Math.floor(rng() * (highInclusive - low + 1));
}

function popcountBigInt(value) {
  let count = 0;
  let remaining = value;
  while (remaining) {
    remaining &= remaining - 1n;
    count += 1;
  }
  return count;
}

function isSubset(mask, target) {
  return (mask & ~target) === 0n;
}

export function boardToMask(board) {
  if (!Array.isArray(board) || board.length !== TAPE_COLUMNS) {
    throw new RangeError(`board must contain ${TAPE_COLUMNS} columns`);
  }
  let mask = 0n;
  for (let column = 0; column < TAPE_COLUMNS; column += 1) {
    mask |= BigInt(Number(board[column]) & 0xff) << BigInt(column * 8);
  }
  return mask;
}

export function maskToBoard(mask) {
  return Array.from({ length: TAPE_COLUMNS }, (_, column) => (
    Number((mask >> BigInt(column * 8)) & 0xffn)
  ));
}

export function allLegalActions() {
  return ACTIONS.map((action) => ({ ...action }));
}

export function boardFromActions(actions) {
  let mask = 0n;
  for (const action of actions) mask |= boardToMask(rollMask(action));
  return maskToBoard(mask);
}

function candidateActions(targetMask) {
  const byMask = new Map();
  for (const candidate of ACTION_MASKS) {
    if (!isSubset(candidate.mask, targetMask)) continue;
    const key = candidate.mask.toString(16);
    if (!byMask.has(key)) byMask.set(key, candidate);
  }
  const sorted = [...byMask.values()].sort((left, right) => (
    popcountBigInt(right.mask) - popcountBigInt(left.mask)
  ));
  const maximal = [];
  for (const candidate of sorted) {
    const dominated = maximal.some((kept) => (candidate.mask & ~kept.mask) === 0n);
    if (!dominated) maximal.push(candidate);
  }
  return maximal;
}

export function solveTarget(target, maxDepth = 5) {
  const targetMask = boardToMask(target);
  if (targetMask === 0n) return { depth: 0, actions: [], explored: 1, candidateCount: 0 };
  const candidates = candidateActions(targetMask);
  let explored = 1;

  const candidatesByBit = Array.from({ length: TAPE_COLUMNS * 8 }, () => []);
  for (const candidate of candidates) {
    for (let bit = 0; bit < candidatesByBit.length; bit += 1) {
      if (candidate.mask & (1n << BigInt(bit))) candidatesByBit[bit].push(candidate);
    }
  }

  for (let depthLimit = 1; depthLimit <= maxDepth; depthLimit += 1) {
    const failedWithBudget = new Map();

    function search(state, remaining, path) {
      explored += 1;
      if (state === targetMask) return path;
      if (remaining === 0) return null;
      if ((failedWithBudget.get(state) ?? -1) >= remaining) return null;

      const uncovered = targetMask & ~state;
      const uncoveredCount = popcountBigInt(uncovered);
      let maxGain = 0;
      for (const candidate of candidates) {
        maxGain = Math.max(maxGain, popcountBigInt(candidate.mask & uncovered));
      }
      if (maxGain === 0 || Math.ceil(uncoveredCount / maxGain) > remaining) {
        failedWithBudget.set(state, remaining);
        return null;
      }

      let options = null;
      for (let bit = 0; bit < candidatesByBit.length; bit += 1) {
        const flag = 1n << BigInt(bit);
        if (!(uncovered & flag)) continue;
        const covering = candidatesByBit[bit].filter((candidate) => (
          (candidate.mask & uncovered) !== 0n
        ));
        if (options === null || covering.length < options.length) options = covering;
        if (options.length === 1) break;
      }

      options.sort((left, right) => (
        popcountBigInt(right.mask & uncovered) - popcountBigInt(left.mask & uncovered)
      ));
      for (const candidate of options) {
        const combined = state | candidate.mask;
        if (combined === state) continue;
        const result = search(combined, remaining - 1, [...path, { ...candidate.action }]);
        if (result) return result;
      }
      failedWithBudget.set(state, remaining);
      return null;
    }

    const actions = search(0n, depthLimit, []);
    if (actions) {
      return {
        depth: actions.length,
        actions,
        explored,
        candidateCount: candidates.length,
      };
    }
  }
  return null;
}

function randomAction(rng, minDistance = 3) {
  return {
    direction: rng() < 0.5 ? -1 : 1,
    distance: randomInt(rng, minDistance, TAPE_COLUMNS),
    face: randomInt(rng, 0, 7),
  };
}

function actionKey(action) {
  return `${action.direction}:${action.distance}:${action.face}`;
}

function targetHasAction(targetMask, action) {
  return isSubset(boardToMask(rollMask(action)), targetMask);
}

export function neighborViability(level) {
  const targetMask = boardToMask(level.target);
  const viable = ACTIONS.filter((action) => targetHasAction(targetMask, action));
  const viableKeys = new Set(viable.map(actionKey));
  const face = viable.some((action) => (
    viableKeys.has(actionKey({ ...action, face: (action.face + 1) % 8 }))
    || viableKeys.has(actionKey({ ...action, face: (action.face + 7) % 8 }))
  ));
  const direction = viable.some((action) => (
    viableKeys.has(actionKey({ ...action, direction: -action.direction }))
  ));
  const stop = viable.some((action) => (
    (action.distance > 1 && viableKeys.has(actionKey({ ...action, distance: action.distance - 1 })))
    || (action.distance < TAPE_COLUMNS
      && viableKeys.has(actionKey({ ...action, distance: action.distance + 1 })))
  ));
  return { face, direction, stop, all: face && direction && stop };
}

function intendedActions(rng, desiredDepth) {
  if (desiredDepth === 2) {
    const actions = [];
    const keys = new Set();
    while (actions.length < desiredDepth) {
      const action = randomAction(rng);
      if (!keys.has(actionKey(action))) {
        keys.add(actionKey(action));
        actions.push(action);
      }
    }
    return actions;
  }

  const base = randomAction(rng, 4);
  const actions = [
    base,
    { ...base, face: (base.face + 1) % 8 },
    { ...base, direction: -base.direction },
  ];
  const keys = new Set(actions.map(actionKey));
  while (actions.length < desiredDepth) {
    const action = randomAction(rng);
    if (!keys.has(actionKey(action))) {
      keys.add(actionKey(action));
      actions.push(action);
    }
  }
  return actions;
}

function makeAlternative(solution) {
  const splittableIndex = solution.findIndex((action) => action.distance > 1);
  if (splittableIndex < 0) return null;
  const prefix = {
    ...solution[splittableIndex],
    distance: solution[splittableIndex].distance - 1,
  };
  return [prefix, ...solution.map((action) => ({ ...action }))];
}

export function generateLevel(seed) {
  const normalizedSeed = Number(seed) >>> 0;
  if (LEVEL_CACHE.has(normalizedSeed)) return LEVEL_CACHE.get(normalizedSeed);
  const desiredDepth = 2 + ((normalizedSeed - 1) & 3);
  const rng = mulberry32(normalizedSeed ^ 0x51f15e5d);

  for (let attempt = 1; attempt <= 4000; attempt += 1) {
    const intended = intendedActions(rng, desiredDepth);
    const target = boardFromActions(intended);
    const solved = solveTarget(target, desiredDepth);
    if (!solved || solved.depth !== desiredDepth) continue;
    const alternative = makeAlternative(solved.actions);
    if (!alternative) continue;
    const level = {
      seed: normalizedSeed,
      target,
      shortest: solved.depth,
      solution: solved.actions,
      alternative,
      desiredDepth,
      generationAttempts: attempt,
    };
    if (desiredDepth >= 3 && !neighborViability(level).all) continue;
    LEVEL_CACHE.set(normalizedSeed, level);
    return level;
  }
  throw new Error(`unable to generate acceptance level for seed ${normalizedSeed}`);
}

export function sampleRandomCompletion(level, trials, randomSeed) {
  const normalizedTrials = Math.max(0, Math.floor(trials));
  const targetMask = boardToMask(level.target);
  const rng = mulberry32(Number(randomSeed) >>> 0);
  let completed = 0;
  for (let trial = 0; trial < normalizedTrials; trial += 1) {
    const length = randomInt(rng, 2, 5);
    let board = 0n;
    for (let move = 0; move < length; move += 1) {
      const candidate = ACTION_MASKS[randomInt(rng, 0, ACTION_MASKS.length - 1)];
      board |= candidate.mask;
    }
    completed += Number(board === targetMask);
  }
  return {
    trials: normalizedTrials,
    completed,
    rate: normalizedTrials === 0 ? 0 : completed / normalizedTrials,
  };
}
