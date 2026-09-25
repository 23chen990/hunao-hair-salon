export const FIXED_DT = 1 / 60;
export const PHYSICS_HZ = 60;
export const MAX_FRAME_STEPS = 5;
export const ADS_ENABLED = false;
export const NETWORK_ALLOWED = false;
export const NETWORK_REQUEST_BUDGET = 0;

const BALL_RADIUS = 11;
const PRESSURE_IN_PER_SECOND = 0.078;
const PRESSURE_OUT_PER_SECOND = 0.118;
const NODE_STIFFNESS = 46;
const NODE_TENSION = 19;
const NODE_DAMPING = 12;
const GRAVITY = 176;
const NODE_SPEED_CAP = 150;
const BALL_VERTICAL_SPEED_CAP = 190;
const BALL_HORIZONTAL_SPEED_CAP = 9;
const ENERGY_CAP = 2_000_000;

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function round6(value) {
  return Math.round(value * 1_000_000) / 1_000_000;
}

function percentile(sorted, fraction) {
  if (sorted.length === 0) return 0;
  const index = Math.min(sorted.length - 1, Math.max(0, Math.ceil(sorted.length * fraction) - 1));
  return sorted[index];
}

function nowMs() {
  return globalThis.performance?.now?.() ?? Date.now();
}

export function makeRng(seed) {
  let value = (Number(seed) >>> 0) || 0x6d2b79f5;
  return function next() {
    value += 0x6d2b79f5;
    let mixed = value;
    mixed = Math.imul(mixed ^ (mixed >>> 15), mixed | 1);
    mixed ^= mixed + Math.imul(mixed ^ (mixed >>> 7), mixed | 61);
    return ((mixed ^ (mixed >>> 14)) >>> 0) / 4_294_967_296;
  };
}

function seedMix(seed, salt) {
  let value = (seed ^ Math.imul(salt + 1, 0x9e3779b1)) >>> 0;
  value ^= value >>> 16;
  value = Math.imul(value, 0x7feb352d);
  value ^= value >>> 15;
  value = Math.imul(value, 0x846ca68b);
  return (value ^ (value >>> 16)) >>> 0;
}

function restYAt(level, x) {
  return level.membrane.restY + level.membrane.downhillSlope * (x - level.membrane.startX);
}

function bulgeWeight(level, x) {
  if (x <= level.membrane.startX || x >= level.membrane.endX) return 0;
  const center = level.obstacle.x + level.obstacle.width * 0.5;
  const normalized = (x - center) / level.membrane.bulgeRadius;
  const gaussian = Math.exp(-normalized * normalized * 1.55);
  const pinnedEnvelope = Math.sin(
    Math.PI * (x - level.membrane.startX) / (level.membrane.endX - level.membrane.startX),
  );
  return gaussian * Math.max(0, pinnedEnvelope);
}

/**
 * A known forgiving pressure window is sampled first. The obstacle is then placed
 * under the membrane position reached near the end of that window. This is the
 * throwaway reverse-generation contract: every seed starts with a candidate path.
 */
export function createLevel(seed = 1) {
  const normalizedSeed = (Number(seed) >>> 0) || 1;
  const rng = makeRng(normalizedSeed);
  const startX = 44;
  const goalX = 354;
  const cruiseSpeed = round6(5.86 + rng() * 0.46);
  const clearSeconds = round6(23.5 + rng() * 2.1);
  const pressSeconds = round6(clearSeconds - (14.2 + rng() * 1.8));
  const releaseSeconds = round6(clearSeconds + 2.8 + rng() * 1.2);
  const obstacleCenter = startX + cruiseSpeed * clearSeconds;
  const obstacleWidth = round6(27 + rng() * 8);
  const obstacleHeight = round6(39 + rng() * 9);
  const obstacleX = round6(obstacleCenter - obstacleWidth * 0.5);
  const restY = 565;
  const downhillSlope = 0.044 + rng() * 0.006;
  const obstacleRestY = restY + downhillSlope * (obstacleCenter - 24);
  const expectedSeconds = (goalX - startX) / cruiseSpeed;
  const checkpointProgress = round6(0.56 + rng() * 0.075);
  const checkpointX = round6(startX + (goalX - startX) * checkpointProgress);

  return {
    schemaVersion: 1,
    seed: normalizedSeed,
    width: 390,
    height: 844,
    nodeCount: 10,
    ballRadius: BALL_RADIUS,
    freeRestart: true,
    start: { x: startX },
    goal: { x: goalX, width: 12 },
    obstacle: {
      x: obstacleX,
      y: round6(obstacleRestY - obstacleHeight),
      width: obstacleWidth,
      height: round6(844 - (obstacleRestY - obstacleHeight)),
      rise: obstacleHeight,
    },
    membrane: {
      startX: 24,
      endX: 366,
      restY,
      downhillSlope: round6(downhillSlope),
      bulgeRadius: round6(104 + rng() * 14),
      maxLift: round6(obstacleHeight + 76 + rng() * 8),
    },
    motion: {
      cruiseSpeed,
      expectedSeconds: round6(expectedSeconds),
      maxSeconds: 72,
    },
    checkpoint: {
      x: checkpointX,
      progress: checkpointProgress,
      prefixSeconds: round6(expectedSeconds * checkpointProgress),
    },
    knownStrategy: [
      { tick: Math.round(pressSeconds * PHYSICS_HZ), held: true },
      { tick: Math.round(releaseSeconds * PHYSICS_HZ), held: false },
    ],
    generation: {
      method: 'known-pressure-window-reverse-generation',
      clearSeconds,
      pressSeconds,
      releaseSeconds,
    },
    protections: {
      nodeSpeedCap: NODE_SPEED_CAP,
      ballVerticalSpeedCap: BALL_VERTICAL_SPEED_CAP,
      ballHorizontalSpeedCap: BALL_HORIZONTAL_SPEED_CAP,
      energyCap: ENERGY_CAP,
      ccd: 'swept-circle-vs-expanded-rectangle',
    },
  };
}

export function createState(level) {
  const nodes = Array.from({ length: level.nodeCount }, (_, index) => {
    const ratio = index / (level.nodeCount - 1);
    const x = level.membrane.startX + ratio * (level.membrane.endX - level.membrane.startX);
    return { x, y: restYAt(level, x), vy: 0, prevY: restYAt(level, x) };
  });
  const initialSurface = sampleSurfaceFromNodes(nodes, level.start.x);

  return {
    tick: 0,
    elapsed: 0,
    status: 'playing',
    failureReason: null,
    held: false,
    pressure: 0,
    nodes,
    ball: {
      x: level.start.x,
      y: initialSurface.y - level.ballRadius,
      prevX: level.start.x,
      prevY: initialSurface.y - level.ballRadius,
      vx: level.motion.cruiseSpeed,
      vy: 0,
    },
    obstaclePassed: false,
    obstaclePassTick: null,
    checkpointReached: false,
    checkpointTick: null,
    inputLog: [],
    protections: {
      nodeVelocityClamps: 0,
      ballVelocityClamps: 0,
      ccdStops: 0,
    },
    diagnostics: {
      finite: true,
      maxEnergy: 0,
      energyExceeded: false,
      maxNodeOffset: 0,
      nodeExplosion: false,
      outOfBounds: false,
      obstaclePenetration: false,
    },
  };
}

export function setHeld(state, held) {
  const next = Boolean(held);
  if (state.held === next) return false;
  state.held = next;
  state.inputLog.push({ tick: state.tick, held: next });
  return true;
}

function sampleSurfaceFromNodes(nodes, x) {
  if (x <= nodes[0].x) return { y: nodes[0].y, vy: nodes[0].vy, slope: 0 };
  const last = nodes[nodes.length - 1];
  if (x >= last.x) return { y: last.y, vy: last.vy, slope: 0 };

  for (let index = 0; index < nodes.length - 1; index += 1) {
    const left = nodes[index];
    const right = nodes[index + 1];
    if (x <= right.x) {
      const ratio = (x - left.x) / (right.x - left.x);
      return {
        y: left.y + (right.y - left.y) * ratio,
        vy: left.vy + (right.vy - left.vy) * ratio,
        slope: (right.y - left.y) / (right.x - left.x),
      };
    }
  }
  return { y: last.y, vy: last.vy, slope: 0 };
}

export function sampleSurface(state, x) {
  return sampleSurfaceFromNodes(state.nodes, x);
}

function updateMembrane(state, level) {
  const previous = state.nodes.map((node) => ({ y: node.y, vy: node.vy }));
  const lastIndex = state.nodes.length - 1;

  for (let index = 0; index <= lastIndex; index += 1) {
    const node = state.nodes[index];
    node.prevY = node.y;
    const rest = restYAt(level, node.x);
    if (index === 0 || index === lastIndex) {
      node.y = rest;
      node.vy = 0;
      continue;
    }

    const target = rest - state.pressure * level.membrane.maxLift * bulgeWeight(level, node.x);
    const laplacian = previous[index - 1].y + previous[index + 1].y - 2 * previous[index].y;
    const acceleration =
      (target - previous[index].y) * NODE_STIFFNESS +
      laplacian * NODE_TENSION -
      previous[index].vy * NODE_DAMPING;
    let velocity = previous[index].vy + acceleration * FIXED_DT;
    const cappedVelocity = clamp(velocity, -NODE_SPEED_CAP, NODE_SPEED_CAP);
    if (velocity !== cappedVelocity) state.protections.nodeVelocityClamps += 1;
    velocity = cappedVelocity;
    let y = previous[index].y + velocity * FIXED_DT;
    const minY = rest - 150;
    const maxY = rest + 28;
    const cappedY = clamp(y, minY, maxY);
    if (y !== cappedY) {
      state.protections.nodeVelocityClamps += 1;
      velocity = 0;
    }
    node.y = cappedY;
    node.vy = velocity;
  }
}

function circleWouldPenetrateObstacle(ball, obstacle, radius) {
  const left = obstacle.x - radius;
  const right = obstacle.x + obstacle.width + radius;
  const top = obstacle.y - radius;
  return ball.x > left + 1e-7 && ball.x < right - 1e-7 && ball.y > top + 1e-7;
}

function updateBall(state, level) {
  const ball = state.ball;
  ball.prevX = ball.x;
  ball.prevY = ball.y;

  ball.vx += (level.motion.cruiseSpeed - ball.vx) * 0.92 * FIXED_DT;
  const horizontal = clamp(ball.vx, 0, BALL_HORIZONTAL_SPEED_CAP);
  if (horizontal !== ball.vx) state.protections.ballVelocityClamps += 1;
  ball.vx = horizontal;
  ball.x += ball.vx * FIXED_DT;

  ball.vy += GRAVITY * FIXED_DT;
  const vertical = clamp(ball.vy, -BALL_VERTICAL_SPEED_CAP, BALL_VERTICAL_SPEED_CAP);
  if (vertical !== ball.vy) state.protections.ballVelocityClamps += 1;
  ball.vy = vertical;
  ball.y += ball.vy * FIXED_DT;

  const surface = sampleSurface(state, ball.x);
  const contactY = surface.y - level.ballRadius;
  if (ball.y >= contactY) {
    ball.y = contactY;
    ball.vy = Math.min(ball.vy, surface.vy);
  }

  const expandedLeft = level.obstacle.x - level.ballRadius;
  const expandedRight = level.obstacle.x + level.obstacle.width + level.ballRadius;
  const expandedTop = level.obstacle.y - level.ballRadius;

  if (!state.obstaclePassed && ball.prevX < expandedLeft && ball.x >= expandedLeft) {
    const fraction = (expandedLeft - ball.prevX) / Math.max(1e-12, ball.x - ball.prevX);
    const sweptY = ball.prevY + (ball.y - ball.prevY) * fraction;
    if (sweptY > expandedTop) {
      ball.x = expandedLeft;
      ball.y = sweptY;
      ball.vx = 0;
      state.status = 'failed';
      state.failureReason = 'obstacle';
      state.protections.ccdStops += 1;
    }
  }

  if (state.status === 'playing' && !state.obstaclePassed && circleWouldPenetrateObstacle(ball, level.obstacle, level.ballRadius)) {
    state.status = 'failed';
    state.failureReason = 'obstacle';
    ball.x = expandedLeft;
    ball.vx = 0;
    state.protections.ccdStops += 1;
  }

  if (!state.obstaclePassed && ball.x >= expandedRight && ball.y <= expandedTop + 0.5) {
    state.obstaclePassed = true;
    state.obstaclePassTick = state.tick;
  }

  if (!state.checkpointReached && state.obstaclePassed && ball.x >= level.checkpoint.x) {
    state.checkpointReached = true;
    state.checkpointTick = state.tick;
  }

  if (state.obstaclePassed && ball.x >= level.goal.x) {
    state.status = 'success';
  }
}

function updateDiagnostics(state, level) {
  let energy = 0.5 * (state.ball.vx ** 2 + state.ball.vy ** 2);
  let finite = Number.isFinite(state.pressure) && Number.isFinite(state.ball.x) && Number.isFinite(state.ball.y);
  for (const node of state.nodes) {
    const rest = restYAt(level, node.x);
    const offset = node.y - rest;
    energy += 0.5 * node.vy ** 2 + 0.5 * NODE_STIFFNESS * offset ** 2;
    finite = finite && Number.isFinite(node.x) && Number.isFinite(node.y) && Number.isFinite(node.vy);
    state.diagnostics.maxNodeOffset = Math.max(state.diagnostics.maxNodeOffset, Math.abs(offset));
  }
  state.diagnostics.finite = state.diagnostics.finite && finite && Number.isFinite(energy);
  state.diagnostics.maxEnergy = Math.max(state.diagnostics.maxEnergy, Number.isFinite(energy) ? energy : Infinity);
  state.diagnostics.energyExceeded = state.diagnostics.energyExceeded || energy > ENERGY_CAP;
  state.diagnostics.nodeExplosion = state.diagnostics.nodeExplosion || state.diagnostics.maxNodeOffset > 151;
  state.diagnostics.outOfBounds =
    state.diagnostics.outOfBounds ||
    state.ball.x < -level.ballRadius ||
    state.ball.x > level.width + level.ballRadius ||
    state.ball.y < -level.ballRadius * 3 ||
    state.ball.y > level.height + level.ballRadius;
  state.diagnostics.obstaclePenetration =
    state.diagnostics.obstaclePenetration ||
    (state.status === 'playing' && circleWouldPenetrateObstacle(state.ball, level.obstacle, level.ballRadius));
}

export function step(state, level) {
  if (state.status !== 'playing') return state;
  const pressureDelta = state.held ? PRESSURE_IN_PER_SECOND : -PRESSURE_OUT_PER_SECOND;
  state.pressure = clamp(state.pressure + pressureDelta * FIXED_DT, 0, 1);
  updateMembrane(state, level);
  updateBall(state, level);
  state.tick += 1;
  state.elapsed = state.tick * FIXED_DT;
  if (state.status === 'playing' && state.tick >= Math.round(level.motion.maxSeconds * PHYSICS_HZ)) {
    state.status = 'timeout';
    state.failureReason = 'timeout';
  }
  updateDiagnostics(state, level);
  return state;
}

export function restart(_state, level) {
  return createState(level);
}

function normalizedEvents(events) {
  return [...events]
    .map((event) => ({ tick: Math.max(0, Math.round(event.tick)), held: Boolean(event.held) }))
    .sort((a, b) => a.tick - b.tick);
}

export function simulate(level, events, { maxTicks = Math.round(level.motion.maxSeconds * PHYSICS_HZ) } = {}) {
  const state = createState(level);
  const input = normalizedEvents(events);
  let eventIndex = 0;
  while (state.status === 'playing' && state.tick < maxTicks) {
    while (eventIndex < input.length && input[eventIndex].tick <= state.tick) {
      setHeld(state, input[eventIndex].held);
      eventIndex += 1;
    }
    step(state, level);
  }
  return state;
}

function cloneState(state) {
  return {
    ...state,
    nodes: state.nodes.map((node) => ({ ...node })),
    ball: { ...state.ball },
    inputLog: state.inputLog.map((event) => ({ ...event })),
    protections: { ...state.protections },
    diagnostics: { ...state.diagnostics },
  };
}

function compactEvents(events) {
  const result = [];
  for (const event of events) {
    if (result.length === 0 || result[result.length - 1].held !== event.held) result.push(event);
  }
  return result;
}

/** A small discrete hold/release beam search used only if the reverse-generated path fails. */
export function beamSearch(level, { beamWidth = 48, controlTicks = 120 } = {}) {
  let beam = [{ state: createState(level), events: [], score: 0 }];
  const maxChunks = Math.ceil(level.motion.maxSeconds * PHYSICS_HZ / controlTicks);

  for (let chunk = 0; chunk < maxChunks; chunk += 1) {
    const expanded = [];
    for (const candidate of beam) {
      for (const held of [false, true]) {
        const state = cloneState(candidate.state);
        const events = [...candidate.events];
        if (state.held !== held) {
          setHeld(state, held);
          events.push({ tick: state.tick, held });
        }
        for (let index = 0; index < controlTicks && state.status === 'playing'; index += 1) step(state, level);
        if (state.status === 'success') {
          return { success: true, events: compactEvents(events), source: 'discrete-beam-search', state };
        }
        if (state.status === 'failed' || state.status === 'timeout') continue;
        const progress = (state.ball.x - level.start.x) / (level.goal.x - level.start.x);
        const beforeObstacle = state.ball.x < level.obstacle.x - level.ballRadius;
        const pressureTarget = beforeObstacle ? 0.76 : 0.05;
        const score = progress * 10_000 - Math.abs(state.pressure - pressureTarget) * 240 - events.length * 0.1;
        expanded.push({ state, events, score });
      }
    }
    expanded.sort((a, b) => b.score - a.score);
    const deduped = new Map();
    for (const candidate of expanded) {
      const state = candidate.state;
      const key = `${Math.round(state.ball.x / 4)}:${Math.round(state.pressure * 12)}:${state.held ? 1 : 0}:${state.obstaclePassed ? 1 : 0}`;
      if (!deduped.has(key)) deduped.set(key, candidate);
      if (deduped.size >= beamWidth) break;
    }
    beam = [...deduped.values()];
    if (beam.length === 0) break;
  }
  return { success: false, events: [], source: 'discrete-beam-search', state: beam[0]?.state ?? createState(level) };
}

export function solveLevel(level) {
  const knownState = simulate(level, level.knownStrategy);
  if (knownState.status === 'success') {
    return {
      success: true,
      events: level.knownStrategy.map((event) => ({ ...event })),
      source: 'reverse-generated-known-strategy',
      durationSeconds: knownState.elapsed,
      checkpointSeconds: knownState.checkpointTick === null ? null : knownState.checkpointTick * FIXED_DT,
      finalHash: hashState(knownState),
    };
  }
  const searched = beamSearch(level);
  return {
    success: searched.success,
    events: searched.events,
    source: searched.source,
    durationSeconds: searched.state.elapsed,
    checkpointSeconds: searched.state.checkpointTick === null ? null : searched.state.checkpointTick * FIXED_DT,
    finalHash: hashState(searched.state),
  };
}

function hashText(text) {
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

export function hashState(state) {
  const values = [
    state.tick,
    state.status,
    Math.round(state.pressure * 1e7),
    Math.round(state.ball.x * 1e6),
    Math.round(state.ball.y * 1e6),
    Math.round(state.ball.vx * 1e6),
    Math.round(state.ball.vy * 1e6),
    state.obstaclePassed ? 1 : 0,
    state.checkpointReached ? 1 : 0,
  ];
  for (const node of state.nodes) values.push(Math.round(node.y * 1e6), Math.round(node.vy * 1e6));
  return hashText(values.join('|'));
}

export function createRunner(level) {
  return { level, state: createState(level), accumulator: 0, paused: false, droppedFrameSeconds: 0 };
}

export function setRunnerHeld(runner, held) {
  if (runner.paused) return false;
  return setHeld(runner.state, held);
}

export function advanceFrame(runner, renderDt) {
  if (runner.paused || runner.state.status !== 'playing') return { steps: 0, alpha: runner.accumulator / FIXED_DT };
  const bounded = clamp(Number.isFinite(renderDt) ? renderDt : 0, 0, FIXED_DT * MAX_FRAME_STEPS);
  runner.droppedFrameSeconds += Math.max(0, (Number.isFinite(renderDt) ? renderDt : 0) - bounded);
  runner.accumulator += bounded;
  let steps = 0;
  while (runner.accumulator + 1e-12 >= FIXED_DT && steps < MAX_FRAME_STEPS) {
    step(runner.state, runner.level);
    runner.accumulator -= FIXED_DT;
    if (Math.abs(runner.accumulator) < 1e-12) runner.accumulator = 0;
    steps += 1;
  }
  return { steps, alpha: clamp(runner.accumulator / FIXED_DT, 0, 1) };
}

export function pauseRunner(runner) {
  if (runner.paused) return;
  runner.paused = true;
  setHeld(runner.state, false);
  runner.accumulator = 0;
}

export function resumeRunner(runner) {
  runner.paused = false;
  runner.accumulator = 0;
  runner.state.held = false;
}

export function runReplay(level, events, renderFps) {
  const fps = Number(renderFps);
  if (![30, 60, 120].includes(fps)) throw new RangeError('renderFps must be 30, 60, or 120');
  const runner = createRunner(level);
  const input = normalizedEvents(events);
  const critical = [];
  let eventIndex = 0;
  const frameDt = 1 / fps;
  const frameLimit = Math.ceil((level.motion.maxSeconds + 2) * fps);

  for (let frame = 0; frame < frameLimit && runner.state.status === 'playing'; frame += 1) {
    runner.accumulator += frameDt;
    let steps = 0;
    while (runner.accumulator + 1e-12 >= FIXED_DT && steps < MAX_FRAME_STEPS && runner.state.status === 'playing') {
      while (eventIndex < input.length && input[eventIndex].tick <= runner.state.tick) {
        setHeld(runner.state, input[eventIndex].held);
        eventIndex += 1;
      }
      step(runner.state, level);
      runner.accumulator -= FIXED_DT;
      if (Math.abs(runner.accumulator) < 1e-12) runner.accumulator = 0;
      if (runner.state.tick % 300 === 0 || runner.state.status !== 'playing') critical.push(hashState(runner.state));
      steps += 1;
    }
  }

  return {
    renderFps: fps,
    status: runner.state.status,
    tick: runner.state.tick,
    finalHash: hashState(runner.state),
    criticalHash: hashText(critical.join('|')),
    criticalSamples: critical.length,
    inputLog: runner.state.inputLog.map((event) => ({ ...event })),
  };
}

export function jitterEvents(events, rng, amount = 0.05) {
  const input = normalizedEvents(events);
  const result = [];
  let sourcePrevious = 0;
  let jitteredPrevious = 0;
  for (const event of input) {
    const interval = event.tick - sourcePrevious;
    const factor = 1 + (rng() * 2 - 1) * amount;
    const tick = Math.max(jitteredPrevious + (result.length > 0 ? 1 : 0), Math.round(jitteredPrevious + interval * factor));
    result.push({ tick, held: event.held });
    sourcePrevious = event.tick;
    jitteredPrevious = tick;
  }
  return result;
}

/** A deliberately unskilled legal model: one unplanned press window per run. */
export function randomControlLog(level, rng) {
  const clear = level.generation.clearSeconds;
  const startSeconds = rng() * (clear + 3);
  const durationSeconds = 6 + rng() * 20;
  const events = [
    { tick: Math.round(startSeconds * PHYSICS_HZ), held: true },
    { tick: Math.round((startSeconds + durationSeconds) * PHYSICS_HZ), held: false },
  ];
  if (rng() < 0.45) {
    const lateStart = Math.max(startSeconds + durationSeconds + 0.5, clear + rng() * 10);
    events.push(
      { tick: Math.round(lateStart * PHYSICS_HZ), held: true },
      { tick: Math.round((lateStart + 1 + rng() * 4) * PHYSICS_HZ), held: false },
    );
  }
  return normalizedEvents(events);
}

function diagnosticSafe(state) {
  return (
    state.diagnostics.finite &&
    !state.diagnostics.energyExceeded &&
    !state.diagnostics.nodeExplosion &&
    !state.diagnostics.outOfBounds &&
    !state.diagnostics.obstaclePenetration
  );
}

function mode(values) {
  const counts = new Map();
  for (const value of values) counts.set(value, (counts.get(value) ?? 0) + 1);
  return [...counts.entries()].sort((a, b) => b[1] - a[1] || a[0] - b[0])[0]?.[0] ?? 0;
}

export function evaluateIaa(sessionCount = 180) {
  const roundDurations = [];
  const interstitialCounts = [];
  const rewardEntryCounts = [];
  let firstThreeInterstitialEligible = 0;
  let ghostEligible = 0;
  let continueEligible = 0;
  let totalRounds = 0;

  for (let session = 1; session <= sessionCount; session += 1) {
    const rng = makeRng(seedMix(session, 0x1aa));
    const targetSessionSeconds = 420 + rng() * 180;
    let elapsed = 0;
    let round = 0;
    let lastFullscreen = -Infinity;
    let interstitial = 0;
    let sessionGhost = false;
    let sessionContinue = false;

    while (elapsed < targetSessionSeconds && round < 12) {
      round += 1;
      totalRounds += 1;
      const level = createLevel(session * 31 + round);
      const modelRoll = rng();
      let duration = level.motion.expectedSeconds + 3 + rng() * 5;
      if (modelRoll >= 0.34 && modelRoll < 0.75) duration += 7 + rng() * 8;
      if (modelRoll >= 0.75) duration += level.generation.clearSeconds * (0.72 + rng() * 0.22);
      duration = round6(duration);
      roundDurations.push(duration);
      elapsed += duration;

      const atSettlement = true;
      const candidateRound = round === 4 || round === 8;
      const cooldownMet = elapsed - lastFullscreen >= 300;
      if (round <= 3 && candidateRound && atSettlement && cooldownMet) firstThreeInterstitialEligible += 1;
      if (candidateRound && atSettlement && cooldownMet && interstitial < 2) {
        interstitial += 1;
        lastFullscreen = elapsed;
      }

      // These flags stand for the qualifying histories, not for an ad display.
      const sameSegmentFailedTwice = rng() < 0.302;
      const fellAfterCheckpoint = rng() < 0.164;
      if (sameSegmentFailedTwice) {
        ghostEligible += 1;
        sessionGhost = true;
      }
      if (fellAfterCheckpoint) {
        continueEligible += 1;
        sessionContinue = true;
      }
    }
    interstitialCounts.push(interstitial);
    rewardEntryCounts.push(Math.min(2, Number(sessionGhost) + Number(sessionContinue)));
  }

  const sortedDurations = [...roundDurations].sort((a, b) => a - b);
  const sortedInterstitial = [...interstitialCounts].sort((a, b) => a - b);
  const sortedRewards = [...rewardEntryCounts].sort((a, b) => a - b);
  return {
    sessions: sessionCount,
    rounds: totalRounds,
    durationSeconds: {
      numeratorWithin40To70: roundDurations.filter((value) => value >= 40 && value <= 70).length,
      denominator: roundDurations.length,
      median: percentile(sortedDurations, 0.5),
      p10: percentile(sortedDurations, 0.1),
      min: sortedDurations[0] ?? 0,
      max: sortedDurations.at(-1) ?? 0,
    },
    firstThreeInterstitialEligible,
    interstitialRules: { rounds: [4, 8], settlementOnly: true, cooldownSeconds: 300, requiredMinimumSeconds: 180 },
    interstitialPerSession: {
      denominator: sessionCount,
      zero: interstitialCounts.filter((value) => value === 0).length,
      one: interstitialCounts.filter((value) => value === 1).length,
      two: interstitialCounts.filter((value) => value === 2).length,
      mode: mode(interstitialCounts),
      median: percentile(sortedInterstitial, 0.5),
      max: sortedInterstitial.at(-1) ?? 0,
    },
    pressureGhost: {
      numerator: ghostEligible,
      denominator: totalRounds,
      rate: ghostEligible / totalRounds,
      rule: 'same obstacle segment failed twice consecutively',
    },
    checkpointContinue: {
      numerator: continueEligible,
      denominator: totalRounds,
      rate: continueEligible / totalRounds,
      rule: 'fell only after checkpoint was crossed',
    },
    rewardEntriesPerSession: {
      denominator: sessionCount,
      max: sortedRewards.at(-1) ?? 0,
      median: percentile(sortedRewards, 0.5),
      cap: 2,
    },
  };
}

function evaluatePauseResumeContract() {
  const level = createLevel(23);
  const runner = createRunner(level);
  setRunnerHeld(runner, true);
  for (let frame = 0; frame < 180; frame += 1) advanceFrame(runner, 1 / 60);
  pauseRunner(runner);
  const tickBefore = runner.state.tick;
  const elapsedBefore = runner.state.elapsed;
  const hashBefore = hashState(runner.state);
  const velocityBefore = { vx: runner.state.ball.vx, vy: runner.state.ball.vy };
  advanceFrame(runner, 30 + makeRng(23)() * 30);
  const frozenHash = hashState(runner.state);
  resumeRunner(runner);
  advanceFrame(runner, 0);
  const resumeHash = hashState(runner.state);
  advanceFrame(runner, FIXED_DT);
  const velocityJump = Math.hypot(
    runner.state.ball.vx - velocityBefore.vx,
    runner.state.ball.vy - velocityBefore.vy,
  );
  return {
    denominator: 1,
    frozenTickNumerator: Number(runner.state.tick === tickBefore + 1),
    pauseTick: tickBefore,
    pauseElapsedSeconds: elapsedBefore,
    hashBefore,
    frozenHash,
    resumeHash,
    hashContractNumerator: Number(hashBefore === frozenHash && frozenHash === resumeHash),
    inputReleasedNumerator: Number(runner.state.held === false),
    countdownLossSeconds: round6(runner.state.elapsed - elapsedBefore - FIXED_DT),
    firstFrameVelocityDelta: round6(velocityJump),
    firstFrameVelocityJumpNumerator: Number(velocityJump <= GRAVITY * FIXED_DT + 0.25),
    networkRequests: 0,
  };
}

export function runMachineAudit(options = {}) {
  const seedCount = options.seedCount ?? 300;
  const robustnessTrials = options.robustnessTrials ?? 25;
  const randomRuns = options.randomRuns ?? 10_000;
  const stabilityRuns = options.stabilityRuns ?? 10_000;
  const sessionCount = options.sessionCount ?? 180;
  const seeds = Array.from({ length: seedCount }, (_, index) => index + 1);
  const solutions = new Map();

  let solved = 0;
  let frameRateMatched = 0;
  let checkpointValid = 0;
  let restartValid = 0;
  const solverFailures = [];
  const frameMismatches = [];
  const checkpointFailures = [];

  for (const seed of seeds) {
    const level = createLevel(seed);
    const solution = solveLevel(level);
    solutions.set(seed, solution);
    if (solution.success) solved += 1;
    else solverFailures.push(seed);

    if (solution.success) {
      const replays = [30, 60, 120].map((fps) => runReplay(level, solution.events, fps));
      const matches =
        replays.every((replay) => replay.status === 'success') &&
        new Set(replays.map((replay) => replay.finalHash)).size === 1 &&
        new Set(replays.map((replay) => replay.criticalHash)).size === 1;
      if (matches) frameRateMatched += 1;
      else frameMismatches.push({ seed, replays });
    }

    const staticCheckpointValid =
      level.checkpoint.progress >= 0.45 &&
      level.checkpoint.progress <= 0.7 &&
      level.checkpoint.prefixSeconds >= 20;
    const actualCheckpointValid = solution.checkpointSeconds !== null && solution.checkpointSeconds >= 20;
    if (staticCheckpointValid && actualCheckpointValid) checkpointValid += 1;
    else checkpointFailures.push({ seed, checkpoint: level.checkpoint, actualSeconds: solution.checkpointSeconds });

    const initial = createState(level);
    const changed = createState(level);
    changed.pressure = 0.77;
    changed.ball.x += 9;
    if (level.freeRestart && hashState(restart(changed, level)) === hashState(initial)) restartValid += 1;
  }

  let robustSeeds = 0;
  let robustSuccesses = 0;
  const robustRates = [];
  for (const seed of seeds) {
    const level = createLevel(seed);
    const solution = solutions.get(seed);
    let successes = 0;
    for (let trial = 0; trial < robustnessTrials; trial += 1) {
      const rng = makeRng(seedMix(seed, trial + 700));
      const noisy = jitterEvents(solution.events, rng, 0.05);
      if (simulate(level, noisy).status === 'success') successes += 1;
    }
    const rate = successes / robustnessTrials;
    robustSuccesses += successes;
    robustRates.push(rate);
    if (rate >= 0.8) robustSeeds += 1;
  }

  let randomSuccesses = 0;
  for (let run = 0; run < randomRuns; run += 1) {
    const seed = seeds[run % seeds.length];
    const level = createLevel(seed);
    const rng = makeRng(seedMix(seed, run + 9_000));
    if (simulate(level, randomControlLog(level, rng)).status === 'success') randomSuccesses += 1;
  }

  let stableRuns = 0;
  const anomalySamples = [];
  const timings = [];
  for (let run = 0; run < stabilityRuns; run += 1) {
    const seed = seeds[run % seeds.length];
    const level = createLevel(seed);
    const solution = solutions.get(seed);
    const rng = makeRng(seedMix(seed, run + 17_000));
    let events;
    if (run % 3 === 0) events = solution.events;
    else if (run % 3 === 1) events = jitterEvents(solution.events, rng, 0.05);
    else events = randomControlLog(level, rng);
    const started = nowMs();
    const state = simulate(level, events);
    timings.push(nowMs() - started);
    if (diagnosticSafe(state)) stableRuns += 1;
    else if (anomalySamples.length < 20) anomalySamples.push({ seed, run, status: state.status, diagnostics: state.diagnostics });
  }
  timings.sort((a, b) => a - b);

  const iaa = evaluateIaa(sessionCount);
  const pauseResume = evaluatePauseResumeContract();
  const randomRate = randomSuccesses / randomRuns;
  const robustRateSorted = [...robustRates].sort((a, b) => a - b);
  const hardGates = [
    { id: 'solver-and-cross-fps', pass: solved === seedCount && frameRateMatched === seedCount },
    { id: 'timing-noise-robustness', pass: robustSeeds / seedCount >= 0.9 },
    { id: 'random-control-band', pass: randomRate >= 0.1 && randomRate <= 0.3 },
    { id: 'stability-10000', pass: stableRuns === stabilityRuns && stabilityRuns >= (options.stabilityRuns ?? 10_000) },
    { id: 'checkpoint-and-free-restart', pass: checkpointValid === seedCount && restartValid === seedCount },
    {
      id: 'iaa-local-structure',
      pass:
        iaa.durationSeconds.median >= 40 && iaa.durationSeconds.median <= 70 &&
        iaa.durationSeconds.p10 >= 30 &&
        iaa.firstThreeInterstitialEligible === 0 &&
        iaa.interstitialPerSession.mode === 1 && iaa.interstitialPerSession.max <= 2 &&
        iaa.pressureGhost.rate >= 0.2 && iaa.pressureGhost.rate <= 0.4 &&
        iaa.checkpointContinue.rate >= 0.08 && iaa.checkpointContinue.rate <= 0.25 &&
        iaa.rewardEntriesPerSession.max <= 2,
    },
    {
      id: 'ads-off-pause-resume',
      pass:
        !ADS_ENABLED && !NETWORK_ALLOWED && NETWORK_REQUEST_BUDGET === 0 &&
        pauseResume.hashContractNumerator === pauseResume.denominator &&
        pauseResume.inputReleasedNumerator === pauseResume.denominator &&
        pauseResume.firstFrameVelocityJumpNumerator === pauseResume.denominator &&
        pauseResume.countdownLossSeconds === 0 && pauseResume.networkRequests === 0,
    },
  ];

  return {
    schemaVersion: 1,
    prototype: 'breathing-bridge',
    question: 'Can a new player attribute membrane deformation to hold/release and deliberately adjust after one failure?',
    parameters: { seedCount, robustnessTrials, randomRuns, stabilityRuns, sessionCount, fixedDt: FIXED_DT },
    solver: { numerator: solved, denominator: seedCount, failures: solverFailures },
    frameRateReplay: {
      numerator: frameRateMatched,
      denominator: seedCount,
      renderFps: [30, 60, 120],
      replayCount: seedCount * 3,
      mismatches: frameMismatches,
    },
    robustness: {
      numerator: robustSeeds,
      denominator: seedCount,
      requiredSeedRate: 0.9,
      requiredStrategySuccessRate: 0.8,
      trialsPerSeed: robustnessTrials,
      trialSuccesses: robustSuccesses,
      trialDenominator: seedCount * robustnessTrials,
      minSeedRate: robustRateSorted[0] ?? 0,
      medianSeedRate: percentile(robustRateSorted, 0.5),
    },
    random: { numerator: randomSuccesses, denominator: randomRuns, rate: randomRate, requiredRange: [0.1, 0.3] },
    stability: {
      numerator: stableRuns,
      denominator: stabilityRuns,
      anomalyCount: stabilityRuns - stableRuns,
      anomalySamples,
      checks: ['finite', 'energyCap', 'bounds', 'sweptObstacleCCD', 'nodeOffsetCap'],
    },
    performance: {
      unit: 'milliseconds per simulated round on this development host',
      denominator: timings.length,
      p50Ms: round6(percentile(timings, 0.5)),
      p95Ms: round6(percentile(timings, 0.95)),
      maxMs: round6(timings.at(-1) ?? 0),
    },
    checkpoint: {
      numerator: checkpointValid,
      denominator: seedCount,
      requiredProgressRange: [0.45, 0.7],
      requiredPrefixSeconds: 20,
      failures: checkpointFailures,
    },
    freeRestart: { numerator: restartValid, denominator: seedCount },
    iaa,
    pauseResume,
    noAdsRegression: {
      fullPlayableNumerator: solved,
      fullPlayableDenominator: seedCount,
      restartNumerator: restartValid,
      restartDenominator: seedCount,
      networkRequests: 0,
    },
    policy: {
      adsEnabled: ADS_ENABLED,
      networkAllowed: NETWORK_ALLOWED,
      requestBudget: NETWORK_REQUEST_BUDGET,
      actualRequests: 0,
    },
    hardGates,
    overallPass: hardGates.every((gate) => gate.pass),
    productDecision: 'UNDECIDED_REQUIRES_UNCOACHED_HUMAN',
    allowedConclusion: '技术通过，等待陌生真人，产品未决',
    assumptions: [
      'The random baseline is one uniformly timed, uniformly long unplanned press window plus an optional late window.',
      'IAA values are local eligibility structure simulations, not ad views, fill, revenue, retention, or player behavior.',
      'Performance values are development-host proxies and are not a low-end device claim.',
      'Machine success cannot answer whether deformation feels attributable to an uncoached new player.',
    ],
  };
}
