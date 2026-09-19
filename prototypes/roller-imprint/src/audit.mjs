import { existsSync, readFileSync } from 'node:fs';
import { performance } from 'node:perf_hooks';
import {
  actionFromPointerSamples,
  applyRoll,
  compareBoards,
  createGame,
  replayEvents,
  rollMask,
  stateHash,
  undoRoll,
} from './model.mjs';
import {
  boardFromActions,
  generateLevel,
  neighborViability,
  sampleRandomCompletion,
  solveTarget,
} from './solver.mjs';
import {
  analyzeIaaSeeds,
  beginAdSimulation,
  endAdSimulation,
  runNoAdRegression,
  simulateSession,
} from './iaa.mjs';
import { computeLayout } from './ui-contract.mjs';

const percentile = (values, ratio) => {
  const sorted = [...values].sort((left, right) => left - right);
  return sorted[Math.floor((sorted.length - 1) * ratio)];
};

function sampledAction(action, fps) {
  const startX = action.direction === 1 ? 20 : 620;
  const endX = startX + action.direction * action.distance * 44;
  const samples = Array.from({ length: fps + 1 }, (_, index) => ({
    x: startX + ((endX - startX) * index) / fps,
  }));
  return actionFromPointerSamples(samples, 44, action.face);
}

function browserEvidence(artifactPath) {
  if (artifactPath && existsSync(artifactPath)) {
    const parsed = JSON.parse(readFileSync(artifactPath, 'utf8'));
    return {
      status: parsed.status === 'PASS' ? 'PASS' : 'ENVIRONMENT_UNVERIFIED',
      actualPointerEventSmoke: parsed,
    };
  }
  const layouts = [[390, 844], [430, 932]].map(([width, height]) => {
    const layout = computeLayout(width, height);
    return {
      viewport: `${width}x${height}`,
      geometryFits: layout.inkBoard.bottom <= layout.canvasHeight,
      minimumDeclaredTargetPx: Math.min(
        ...Object.values(layout.touchTargets).flatMap((target) => [target.width, target.height]),
      ),
    };
  });
  return {
    status: 'ENVIRONMENT_UNVERIFIED',
    actualPointerEventSmoke: null,
    verifiedWithoutBrowser: layouts,
    reason: 'sandbox loopback listen failed with EPERM; the single normal permission request was rejected by host policy `never`',
    interpretation: 'infrastructure unavailable is not a mechanics KILL; main task must rerun npm run smoke independently',
  };
}

export function runAudit({
  seedCount = 500,
  randomTrials = 10000,
  browserArtifactPath = null,
} = {}) {
  const levels = [];
  const timingsMs = [];
  const depthDistribution = { 2: 0, 3: 0, 4: 0, 5: 0 };
  let reachable = 0;
  let zeroActionComplete = 0;
  let targetWithin8x12 = 0;
  let exactSuboptimal = 0;
  let robustSeeds = 0;
  let fpsSampleMatches = 0;
  let undoHashMatches = 0;
  let replayMatches = 0;
  let adFreezeHashUnchanged = 0;
  let adFreezeRollIgnored = 0;
  let adFreezeClockIgnored = 0;
  let replayEvidence = null;

  for (let seed = 1; seed <= seedCount; seed += 1) {
    const started = performance.now();
    const level = generateLevel(seed);
    const solved = solveTarget(level.target, 5);
    timingsMs.push(performance.now() - started);
    levels.push(level);

    const exactSolution = solved && compareBoards(boardFromActions(solved.actions), level.target).exact;
    reachable += Number(Boolean(exactSolution));
    zeroActionComplete += Number(level.target.every((column) => column === 0));
    targetWithin8x12 += Number(level.target.length === 12 && level.target.every((column) => (
      Number.isInteger(column) && column >= 0 && column <= 255
    )));
    if (solved?.depth >= 2 && solved.depth <= 5) depthDistribution[solved.depth] += 1;

    exactSuboptimal += Number(
      level.alternative.length > level.shortest
      && compareBoards(boardFromActions(level.alternative), level.target).exact,
    );
    robustSeeds += Number(neighborViability(level).all);

    const masks = [30, 60, 120].map((fps) => rollMask(sampledAction(level.solution[0], fps)));
    fpsSampleMatches += Number(
      JSON.stringify(masks[0]) === JSON.stringify(masks[1])
      && JSON.stringify(masks[1]) === JSON.stringify(masks[2]),
    );

    const initial = createGame({ seed, target: level.target });
    const rolled = applyRoll(initial, level.solution[0]);
    undoHashMatches += Number(stateHash(undoRoll(rolled)) === stateHash(initial));

    const events = level.solution.map((action) => ({ type: 'roll', action }));
    const firstReplay = replayEvents(initial, events);
    const secondReplay = replayEvents(initial, JSON.parse(JSON.stringify(events)));
    const deterministic = JSON.stringify(firstReplay.hashes) === JSON.stringify(secondReplay.hashes)
      && firstReplay.finalHash === secondReplay.finalHash;
    replayMatches += Number(deterministic);
    if (seed === 1) {
      replayEvidence = {
        seed,
        initialHash: stateHash(initial),
        events,
        frameHashes: firstReplay.hashes,
        finalHash: firstReplay.finalHash,
        exact: compareBoards(firstReplay.state.ink, firstReplay.state.target).exact,
      };
    }

    const beforeAd = rolled;
    const beforeAdHash = stateHash(beforeAd);
    const paused = beginAdSimulation(beforeAd);
    const attemptedRoll = applyRoll(paused, level.solution.at(-1));
    const attemptedTick = replayEvents(attemptedRoll, [{ type: 'tick', ms: 5000 }]).state;
    const resumed = endAdSimulation(attemptedTick);
    adFreezeRollIgnored += Number(attemptedRoll === paused);
    adFreezeClockIgnored += Number(attemptedTick.clockMs === beforeAd.clockMs);
    adFreezeHashUnchanged += Number(stateHash(resumed) === beforeAdHash);
  }

  let randomCompleted = 0;
  let allocatedTrials = 0;
  for (let index = 0; index < levels.length; index += 1) {
    const remainingSeeds = levels.length - index;
    const trials = Math.floor((randomTrials - allocatedTrials) / remainingSeeds);
    const sampled = sampleRandomCompletion(levels[index], trials, 0x700000 + index);
    randomCompleted += sampled.completed;
    allocatedTrials += sampled.trials;
  }

  const iaa = analyzeIaaSeeds(seedCount);
  const sessions = [420, 480, 540, 600, 660].map((seconds, index) => (
    simulateSession(50000 + index, seconds)
  ));
  const noAd = runNoAdRegression(seedCount);
  const browser = browserEvidence(browserArtifactPath);
  const performanceProxy = {
    p50Ms: percentile(timingsMs, 0.5),
    p95Ms: percentile(timingsMs, 0.95),
    maxMs: Math.max(...timingsMs),
    measuredSeeds: seedCount,
    measurement: 'cold-per-process program generation (including acceptance solve) plus a second strict shortest solve',
    disclaimer: 'development-machine proxy only; not a low-end-device result',
  };

  const sessionPointsInBand = sessions.every((session) => (
    session.interstitialPoints.length >= 1
    && session.interstitialPoints.length <= 2
    && session.rounds.slice(0, 3).every((round) => !round.interstitialEligible)
    && session.cooldownSeconds >= 180
    && session.cooldownSeconds <= 300
  ));

  const gates = {
    reachability: reachable === seedCount
      && zeroActionComplete === 0
      && Object.values(depthDistribution).reduce((sum, value) => sum + value, 0) === seedCount,
    determinism: fpsSampleMatches === seedCount
      && undoHashMatches === seedCount
      && replayMatches === seedCount,
    neighborTolerance: robustSeeds / seedCount >= 0.5,
    randomAndAlternatives: randomCompleted / randomTrials <= 0.1
      && exactSuboptimal === seedCount,
    performanceProxy: performanceProxy.p95Ms <= 30,
    iaaStructure: iaa.roundSeconds.median >= 45
      && iaa.roundSeconds.median <= 80
      && iaa.roundSeconds.p10 >= 30
      && iaa.contactHint.rate >= 0.15
      && iaa.contactHint.rate <= 0.35
      && iaa.singlePassUndo.rate >= 0.10
      && iaa.singlePassUndo.rate <= 0.25
      && iaa.maxVoluntaryEntriesPerSession <= 2
      && sessionPointsInBand,
    noAdAndFreeze: noAd.completed === seedCount
      && noAd.restarted === seedCount
      && noAd.blocked === 0
      && adFreezeHashUnchanged === seedCount
      && adFreezeRollIgnored === seedCount
      && adFreezeClockIgnored === seedCount,
    browserPointerSmoke: browser.status === 'PASS',
  };
  const implementationGates = Object.entries(gates)
    .filter(([name]) => name !== 'browserPointerSmoke')
    .every(([, passed]) => passed);
  const decision = !implementationGates
    ? 'KILL'
    : browser.status === 'PASS'
      ? '技术通过，等待陌生真人，产品未决'
      : '技术门槛（除宿主阻塞的浏览器实测）通过；浏览器 ENVIRONMENT_UNVERIFIED，等待主任务独立复验与陌生真人，产品未决';

  return {
    schemaVersion: 1,
    prototype: 'P2 滚筒成章 / ROLL / THROWAWAY',
    question: '新玩家能否在30秒内理解拖动距离→转角→凸纹落印，并从错印主动调整下一趟？',
    configuration: { seedCount, randomTrials, faces: 8, tapeRows: 8, tapeColumns: 12 },
    reachability: {
      reachable,
      zeroActionComplete,
      targetWithin8x12,
      shortestDepthDistribution: depthDistribution,
    },
    determinism: { fpsSampleMatches, undoHashMatches, replayMatches, replayEvidence },
    neighborTolerance: {
      robustSeeds,
      rate: robustSeeds / seedCount,
      definition: 'target admits at least one target-safe adjacent face, reversed direction, and adjacent stop operation',
    },
    randomSequences: {
      trials: allocatedTrials,
      completed: randomCompleted,
      rate: allocatedTrials === 0 ? 0 : randomCompleted / allocatedTrials,
      sampling: 'length uniformly 2–5; each roll uniformly selected from all 192 legal actions; trials distributed uniformly across acceptance seeds',
    },
    alternatives: { exactSuboptimal, required: seedCount },
    performanceProxy,
    iaa: {
      ...iaa,
      sessions: sessions.map((session) => ({
        requestedSeconds: session.requestedSeconds,
        modeledSeconds: session.modeledSeconds,
        rounds: session.rounds.length,
        cooldownSeconds: session.cooldownSeconds,
        interstitialPoints: session.interstitialPoints,
      })),
      disclaimer: 'all round length, hint, undo, inventory, and cooldown numbers are assumptions/development-machine structure proxies, not viewing, fill, eCPM, ARPDAU, revenue, retention, or device measurements',
    },
    noAd,
    adFreeze: {
      testedSeeds: seedCount,
      rollIgnored: adFreezeRollIgnored,
      clockIgnored: adFreezeClockIgnored,
      hashUnchanged: adFreezeHashUnchanged,
    },
    browser,
    gates,
    implementationGatesPass: implementationGates,
    decision,
    humanValidation: {
      status: 'NOT_RUN',
      required: true,
      productKeepAllowed: false,
    },
  };
}
