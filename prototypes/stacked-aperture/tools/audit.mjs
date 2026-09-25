import assert from 'node:assert/strict';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { performance } from 'node:perf_hooks';
import {
  advanceClock,
  beginAdSimulation,
  completeRound,
  createEligibilitySession,
  endAdSimulation,
  registerRewardEntry,
} from '../src/eligibility.js';
import { snapCatchWidth } from '../src/input.js';
import {
  attemptDrop,
  createGameState,
  deserializeGameState,
  dropBead,
  generateLevel,
  hashGameState,
  moveGameState,
  retryLevel,
  serializeGameState,
} from '../src/model.js';
import { createRng } from '../src/prng.js';
import { recordRun, replayRun } from '../src/replay.js';
import {
  auditDurations,
  auditRandomSuccess,
  auditRewardEligibility,
} from '../src/simulation.js';
import { solveLevel } from '../src/solver.js';

const root = resolve(import.meta.dirname, '..');
const evidenceRoot = resolve(root, 'evidence');
await mkdir(evidenceRoot, { recursive: true });

function percentile(values, proportion) {
  const sorted = [...values].sort((a, b) => a - b);
  return sorted[Math.max(0, Math.ceil(sorted.length * proportion) - 1)];
}

function moveStateAlong(level, state, moves) {
  return moves.reduce((current, move) => moveGameState(level, current, move), state);
}

const seedCount = 500;
const solverTimesMs = [];
const shortestDistances = [];
let solvableSeeds = 0;
let initiallyBlockedSeeds = 0;
let replayHashMatches = 0;
let replayFramesVerified = 0;
let serializationHashMatches = 0;
let noAdFlowPasses = 0;
let distinctLayerColorPasses = 0;
let decoyGeometryPasses = 0;
let sampledIntermediateStates = 0;
let sampledIntermediateSolvable = 0;
let minimumDecoyClearanceGapPx = Number.POSITIVE_INFINITY;

for (let seed = 1; seed <= seedCount; seed += 1) {
  const level = generateLevel(seed);
  if (!dropBead(level, level.initialOffsets).passed) initiallyBlockedSeeds += 1;

  const startedAt = performance.now();
  const solution = solveLevel(level, level.initialOffsets);
  solverTimesMs.push(performance.now() - startedAt);
  if (solution) solvableSeeds += 1;
  assert.ok(solution, `seed ${seed} is unsolvable`);
  shortestDistances.push(solution.distance);

  const colors = new Set(level.layers.map((layer) => layer.color));
  if (colors.size === level.layerCount) distinctLayerColorPasses += 1;
  let seedDecoysPass = true;
  for (const layer of level.layers) {
    for (const hole of layer.holes.filter((candidate) => candidate.role === 'decoy')) {
      for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
        const centerDistance = Math.abs((hole.localSlot + slot) * level.slotSpacing);
        const gap = centerDistance + level.beadRadius - hole.radius;
        minimumDecoyClearanceGapPx = Math.min(minimumDecoyClearanceGapPx, gap);
        if (gap <= 0) seedDecoysPass = false;
      }
    }
  }
  if (seedDecoysPass) decoyGeometryPasses += 1;

  const actions = solution.moves.map((move, index) => ({
    type: 'move',
    ...move,
    advanceMs: 900 + index,
  }));
  actions.push({ type: 'drop', advanceMs: 2_000 });
  const recorded = recordRun(level, actions);
  const replayed = replayRun(level, recorded.events);
  replayFramesVerified += recorded.events.length;
  if (replayed.hash === recorded.hash) replayHashMatches += 1;
  const serialized = serializeGameState(recorded.state);
  const restored = deserializeGameState(serialized);
  if (hashGameState(restored) === recorded.hash) serializationHashMatches += 1;

  const sampleRng = createRng(`intermediate:${seed}`);
  for (let sample = 0; sample < 4; sample += 1) {
    const offsets = Array.from(
      { length: level.layerCount },
      () => sampleRng.int(level.slotMin, level.slotMax),
    );
    sampledIntermediateStates += 1;
    if (solveLevel(level, offsets)) sampledIntermediateSolvable += 1;
  }

  let gameplay = createGameState(level);
  const failed = attemptDrop(level, gameplay);
  const retainedOffsets = [...failed.offsets];
  gameplay = retryLevel(failed);
  const retryPreservedLayout = JSON.stringify(gameplay.offsets) === JSON.stringify(retainedOffsets);
  const retrySolution = solveLevel(level, gameplay.offsets);
  gameplay = moveStateAlong(level, gameplay, retrySolution.moves);
  gameplay = attemptDrop(level, gameplay);
  const disabledSession = completeRound(
    createEligibilitySession({ placeholdersEnabled: false }),
    60_000,
  );
  const nextLevel = generateLevel(seed + 1);
  if (
    retryPreservedLayout
    && gameplay.status === 'passed'
    && disabledSession.events.length === 0
    && solveLevel(nextLevel)
  ) {
    noAdFlowPasses += 1;
  }
}

const randomSuccess = auditRandomSuccess(10_000, 20_001);
const durations = auditDurations(500, 10_001);
const rewards = auditRewardEligibility(10_000, 30_001);
const releaseBandWidthPx = snapCatchWidth(54);
const releaseHalfTolerancePx = releaseBandWidthPx / 2;

const sessionOpportunityCounts = {};
const sessionOpportunityRounds = {};
for (const minutes of [8, 9, 10, 11, 12]) {
  let session = createEligibilitySession({ placeholdersEnabled: true });
  const rounds = [];
  for (let round = 1; round <= minutes; round += 1) {
    const result = completeRound(session, 60_000);
    session = result.session;
    if (result.events.length) rounds.push(round);
  }
  sessionOpportunityCounts[minutes] = rounds.length;
  sessionOpportunityRounds[minutes] = rounds;
}

let pausedSession = createEligibilitySession({ placeholdersEnabled: true });
pausedSession = advanceClock(pausedSession, 5_000);
const pauseGameplay = createGameState(generateLevel(99_999));
const beforePauseHash = hashGameState(pauseGameplay);
pausedSession = beginAdSimulation(pausedSession, 'rewarded');
pausedSession = advanceClock(pausedSession, 45_000);
const duringPauseGameClockMs = pausedSession.gameClockMs;
pausedSession = endAdSimulation(pausedSession);
const afterPauseHash = hashGameState(pauseGameplay);

let cappedRewardSession = createEligibilitySession({ placeholdersEnabled: true });
const cappedRewardEvents = [];
for (const type of ['hint', 'diagnostic', 'hint']) {
  const result = registerRewardEntry(cappedRewardSession, type);
  cappedRewardSession = result.session;
  cappedRewardEvents.push(...result.events);
}

const gates = {
  seedsSolvable: solvableSeeds === seedCount,
  noInitialStraightThrough: initiallyBlockedSeeds === seedCount,
  shortestDistanceInRange: Math.min(...shortestDistances) >= 3 && Math.max(...shortestDistances) <= 8,
  serializationAndReplayDeterministic: replayHashMatches === seedCount && serializationHashMatches === seedCount,
  touchTolerance: releaseHalfTolerancePx >= 24,
  visibleSafeDecoys: decoyGeometryPasses === seedCount
    && distinctLayerColorPasses === seedCount
    && minimumDecoyClearanceGapPx > 0
    && sampledIntermediateSolvable === sampledIntermediateStates,
  randomSuccessAtMost15Percent: randomSuccess.rate <= 0.15,
  solverP95AtMost20Ms: percentile(solverTimesMs, 0.95) <= 20,
  interstitialStructure: Object.values(sessionOpportunityCounts).every((count) => count >= 1 && count <= 2)
    && Object.values(sessionOpportunityRounds).every((rounds) => rounds.every((round) => round >= 4)),
  hintEligibilityInBand: rewards.hintRate >= 0.15 && rewards.hintRate <= 0.35,
  diagnosticEligibilityInBand: rewards.diagnosticRate >= 0.10 && rewards.diagnosticRate <= 0.30,
  rewardEntrySessionCap: cappedRewardSession.rewardEligibilityCount === 2 && cappedRewardEvents.length === 2,
  noAdRegression: noAdFlowPasses === seedCount,
  adSimulationPausesLogicClock: pausedSession.wallClockMs === 50_000
    && duringPauseGameClockMs === 5_000
    && beforePauseHash === afterPauseHash,
};

const report = {
  auditedAt: new Date().toISOString(),
  environment: {
    node: process.version,
    platform: `${process.platform}-${process.arch}`,
    solverTimingScope: 'development-machine proxy only; not a low-end-device result',
  },
  fixedSeedSet: { first: 1, last: seedCount, count: seedCount },
  generationAndSolve: {
    solvableSeeds,
    initiallyBlockedSeeds,
    shortestDistanceMin: Math.min(...shortestDistances),
    shortestDistanceMax: Math.max(...shortestDistances),
    shortestDistanceHistogram: Object.fromEntries(
      [...new Set(shortestDistances)].sort((a, b) => a - b).map((distance) => [
        distance,
        shortestDistances.filter((value) => value === distance).length,
      ]),
    ),
  },
  determinism: {
    replayHashMatches,
    serializationHashMatches,
    replayFramesVerified,
  },
  interactionAndDecoys: {
    slotSpacingPx: 54,
    releaseBandWidthPx,
    releaseHalfTolerancePx,
    distinctLayerColorPasses,
    decoyGeometryPasses,
    minimumDecoyClearanceGapPx,
    sampledIntermediateStates,
    sampledIntermediateSolvable,
  },
  randomSuccess,
  solverTimingMs: {
    samples: solverTimesMs.length,
    median: percentile(solverTimesMs, 0.5),
    p95: percentile(solverTimesMs, 0.95),
    max: Math.max(...solverTimesMs),
  },
  durationProxy: durations,
  eligibilityProxy: rewards,
  rewardedEntryCap: {
    attemptedEligibilityEvents: 3,
    emittedEligibilityEvents: cappedRewardEvents.length,
    capPerSession: 2,
  },
  interstitialSessions: {
    opportunityCountsByMinutes: sessionOpportunityCounts,
    opportunityRoundsByMinutes: sessionOpportunityRounds,
    firstThreeRoundsEligible: 0,
    capPerSession: 2,
    cooldownMs: 180_000,
  },
  noAdAndPause: {
    noAdFlowPasses,
    noAdFlowSeeds: seedCount,
    pausedWallClockMs: pausedSession.wallClockMs,
    pausedGameClockMs: duringPauseGameClockMs,
    gameplayHashUnchanged: beforePauseHash === afterPauseHash,
  },
  gates,
  allMachineGatesPassed: Object.values(gates).every(Boolean),
  browserSmoke: {
    status: 'not included in this audit; run npm run browser:smoke separately',
  },
};

await writeFile(resolve(evidenceRoot, 'audit-report.json'), `${JSON.stringify(report, null, 2)}\n`);
process.stdout.write(`${JSON.stringify(report, null, 2)}\n`);
if (!report.allMachineGatesPassed) process.exitCode = 1;
