import {
  applyRoll,
  compareBoards,
  createGame,
  restartGame,
  stateHash,
} from './model.mjs';
import { allLegalActions, generateLevel } from './solver.mjs';

const LEGAL_ACTIONS = allLegalActions();

function mix(seed, salt = 0) {
  let value = (Number(seed) ^ salt) >>> 0;
  value = Math.imul(value ^ (value >>> 16), 0x7feb352d);
  value = Math.imul(value ^ (value >>> 15), 0x846ca68b);
  return (value ^ (value >>> 16)) >>> 0;
}

function unit(seed, salt) {
  return mix(seed, salt) / 4294967296;
}

function percentile(values, ratio) {
  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.floor((sorted.length - 1) * ratio)];
}

function targetBitCount(target) {
  return target.reduce((total, byte) => {
    let bits = byte;
    let count = 0;
    while (bits) {
      bits &= bits - 1;
      count += 1;
    }
    return total + count;
  }, 0);
}

function boardError(state) {
  const error = compareBoards(state.ink, state.target);
  return error.missing + error.wrong;
}

export function createLocalAdPolicy() {
  return {
    enabled: false,
    networkAllowed: false,
    networkRequests: 0,
    voluntaryRewardLimit: 2,
  };
}

export function beginAdSimulation(state) {
  if (state.paused) return state;
  return { ...state, paused: true };
}

export function endAdSimulation(state) {
  if (!state.paused) return state;
  return { ...state, paused: false };
}

export function runNoAdRegression(count = 500) {
  let completed = 0;
  let restarted = 0;
  let blocked = 0;
  for (let seed = 1; seed <= count; seed += 1) {
    const level = generateLevel(seed);
    const initial = createGame({ seed, target: level.target });
    let state = initial;
    for (const action of level.solution) state = applyRoll(state, action);
    completed += Number(compareBoards(state.ink, state.target).exact);
    restarted += Number(stateHash(restartGame(state)) === stateHash(initial));
    blocked += Number(state.paused);
  }
  return { seeds: count, completed, restarted, blocked, adPolicy: createLocalAdPolicy() };
}

export function modeledRoundSeconds(level) {
  const observation = 12 + unit(level.seed, 0x101) * 6;
  let seconds = observation;
  for (let move = 0; move < level.shortest; move += 1) {
    const think = 6 + unit(level.seed, 0x200 + move) * 8;
    const drag = 1 + unit(level.seed, 0x300 + move);
    seconds += think + drag + 2;
  }
  return Math.round(seconds * 1000) / 1000;
}

function hasTwoRealWorseningMoves(level) {
  let state = createGame({ seed: level.seed, target: level.target });
  let previousError = boardError(state);
  for (let step = 0; step < 2; step += 1) {
    let next = null;
    for (const action of LEGAL_ACTIONS) {
      const candidate = applyRoll(state, action);
      if (boardError(candidate) > previousError) {
        next = candidate;
        break;
      }
    }
    if (!next) return false;
    state = next;
    previousError = boardError(state);
  }
  return true;
}

function contactHintEligible(level) {
  return mix(level.seed, 0x41) % 100 < 25 && hasTwoRealWorseningMoves(level);
}

function hasRealUndoThresholdCrossing(level) {
  const exact = {
    ...createGame({ seed: level.seed, target: level.target }),
    ink: [...level.target],
  };
  const denominator = Math.max(1, targetBitCount(level.target));
  for (const action of LEGAL_ACTIONS) {
    const changed = applyRoll(exact, action);
    const before = compareBoards(exact.ink, exact.target);
    const after = compareBoards(changed.ink, changed.target);
    const beforeRatio = (before.missing + before.wrong) / denominator;
    const afterRatio = (after.missing + after.wrong) / denominator;
    if (beforeRatio <= 0.2 && afterRatio > 0.2) return true;
  }
  return false;
}

function singlePassUndoEligible(level) {
  return mix(level.seed, 0x99) % 100 < 18 && hasRealUndoThresholdCrossing(level);
}

export function analyzeIaaSeeds(count = 500) {
  const durations = [];
  let hintEligible = 0;
  let undoEligible = 0;
  for (let seed = 1; seed <= count; seed += 1) {
    const level = generateLevel(seed);
    durations.push(modeledRoundSeconds(level));
    hintEligible += Number(contactHintEligible(level));
    undoEligible += Number(singlePassUndoEligible(level));
  }
  return {
    seeds: count,
    roundSeconds: {
      p10: percentile(durations, 0.1),
      median: percentile(durations, 0.5),
      min: Math.min(...durations),
      max: Math.max(...durations),
      assumption: '开发机确定性局长代理：观察 12–18 秒；每趟思考 6–14 秒、拖动 1–2 秒、对比 2 秒',
    },
    contactHint: {
      eligible: hintEligible,
      rate: hintEligible / count,
      rule: '真实状态误差连续两趟增大，且固定 seed 新手门控为 25%',
    },
    singlePassUndo: {
      eligible: undoEligible,
      rate: undoEligible / count,
      rule: '真实状态误差由 <=20% 跨到 >20%，且固定 seed 新手门控为 18%',
    },
    maxVoluntaryEntriesPerSession: 2,
  };
}

export function simulateSession(seed, requestedSeconds) {
  const durationSeconds = Math.max(0, Number(requestedSeconds) || 0);
  const cooldownSeconds = 180 + (mix(seed, 0xc001) % 121);
  const rounds = [];
  const interstitialPoints = [];
  let elapsed = 0;
  let lastInterstitial = 0;
  let roundNumber = 1;

  while (true) {
    const level = generateLevel((Number(seed) + roundNumber) >>> 0);
    const duration = modeledRoundSeconds(level);
    if (elapsed + duration > durationSeconds) break;
    elapsed += duration;
    const afterFreeOpening = roundNumber > 3;
    const cooledDown = elapsed - lastInterstitial >= cooldownSeconds;
    const interstitialEligible = afterFreeOpening
      && cooledDown
      && interstitialPoints.length < 2;
    const round = {
      number: roundNumber,
      durationSeconds: duration,
      settledAtSeconds: Math.round(elapsed * 1000) / 1000,
      interstitialEligible,
    };
    rounds.push(round);
    if (interstitialEligible) {
      interstitialPoints.push({
        round: roundNumber,
        atSeconds: round.settledAtSeconds,
        atSettlement: true,
      });
      lastInterstitial = elapsed;
    }
    roundNumber += 1;
  }

  return {
    seed: Number(seed) >>> 0,
    requestedSeconds: durationSeconds,
    modeledSeconds: Math.round(elapsed * 1000) / 1000,
    cooldownSeconds,
    rounds,
    interstitialPoints,
    voluntaryRewardLimit: 2,
    assumption: '局长与资格均为本地结构代理；不是观看率、填充率、收益或真机结论',
  };
}
