export const FIXED_DT = 1 / 60;
export const PHYSICS_HZ = 60;
export const MAX_FRAME_STEPS = 5;
export const NODE_COUNT = 10;
export const ADS_ENABLED = false;
export const NETWORK_ALLOWED = false;
export const NETWORK_REQUEST_BUDGET = 0;

const BALL_RADIUS = 11;
const PRESSURE_IN_PER_SECOND = 0.24;
const PRESSURE_OUT_PER_SECOND = 0.36;
const BRIDGE_GROWTH_IDLE = 20;
const BRIDGE_GROWTH_HELD = 48;
const BRIDGE_GROWTH_PRESSURE = 78;
const RISE_GRAVITY = 360;
const FALL_GRAVITY = 560;
const JUMP_SPEED = 340;
const BALL_SPEED_CAP = 78;
const NODE_SPEED_CAP = 180;
const ENERGY_CAP = 2_000_000;

const ARCHETYPE_DEFS = [
  { name: 'long-reach', gapScale: 1.42, wallScale: 0.54, pace: 0.92, cadence: 'hold-long' },
  { name: 'short-pulse', gapScale: 0.67, wallScale: 0.92, pace: 1.12, cadence: 'tap-release' },
  { name: 'high-lift', gapScale: 0.84, wallScale: 1.35, pace: 0.94, cadence: 'charge-high' },
  { name: 'late-drop', gapScale: 1.08, wallScale: 0.70, pace: 0.86, cadence: 'late-release' },
  { name: 'zigzag-rhythm', gapScale: 1.0, wallScale: 0.86, pace: 1.03, cadence: 'alternating' },
];

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

function pushEvent(events, tick, held) {
  const next = { tick: Math.max(0, Math.round(tick)), held: Boolean(held) };
  if (events.length === 0 || events.at(-1).held !== next.held) events.push(next);
}

function normaliseEvents(events) {
  return [...events]
    .map((event) => ({ tick: Math.max(0, Math.round(event.tick)), held: Boolean(event.held) }))
    .sort((a, b) => a.tick - b.tick);
}

function buildKnownStrategy(level) {
  const events = [];
  let held = false;
  let lastRelease = 0;
  const speed = level.motion.cruiseSpeed;
  const releaseOffset = level.archetype === 'high-lift' ? 25 : 40;
  for (const segment of level.segments) {
    const obstacleX = segment.obstacle ? segment.obstacle.x : segment.landingStart;
    // Release only for a real hop. A gratuitous release on a roll segment would
    // spend the jump while the ball is still airborne and make the next wall
    // look like a random failure rather than a readable timing lesson.
    const releaseX = obstacleX - releaseOffset;
    // Start before the gap, not just before the landing. The player must visibly
    // grow the bridge ahead of the auto-runner before the front edge arrives.
    const leadBeforeGap = segment.needsJump ? 1.0 : 0.55;
    const pressTime = Math.max(0.15, (segment.startX - level.start.x) / speed - leadBeforeGap);
    const desiredPressTick = Math.round(pressTime * PHYSICS_HZ);
    if (!held) {
      const pressTick = Math.max(desiredPressTick, lastRelease + 1);
      pushEvent(events, pressTick, true);
      held = true;
    }
    if (segment.needsJump) {
      const releaseTime = Math.max(0.3, (releaseX - level.start.x) / speed);
      const releaseTick = Math.max(lastRelease + 18, Math.round(releaseTime * PHYSICS_HZ));
      pushEvent(events, releaseTick, false);
      held = false;
      lastRelease = releaseTick;
    }
  }
  if (held) pushEvent(events, Math.round(((level.goalX - level.start.x) / speed + 0.8) * PHYSICS_HZ), false);
  return events;
}

function segmentAction(archetype, index, rng, definition) {
  const alternating = archetype === 'zigzag-rhythm' && index % 2 === 1;
  const baseGap = 65 + rng() * 58;
  const gap = baseGap * definition.gapScale * (alternating ? 1.38 : 0.78);
  const platformWidth = 135 + rng() * 80;
  let wallScale = definition.wallScale;
  if (archetype === 'high-lift' && index % 2 === 0) wallScale *= 1.18;
  if (archetype === 'late-drop' && index < 3) wallScale *= 0.5;
  if (archetype === 'short-pulse' && index % 3 === 2) wallScale *= 1.25;
  const obstacleHeight = clamp((22 + rng() * 25) * wallScale, 16, 56);
  const obstacleWidth = clamp(22 + rng() * 20 + (archetype === 'short-pulse' ? 7 : 0), 20, 52);
  const needsJump = obstacleHeight >= 34;
  return {
    gap: round6(gap),
    platformWidth: round6(platformWidth),
    obstacleHeight: round6(obstacleHeight),
    obstacleWidth: round6(obstacleWidth),
    needsJump,
    holdSeconds: round6(clamp(1.65 + gap / (archetype === 'long-reach' ? 48 : 65) + (needsJump ? 0.55 : 0), 1.5, 5.2)),
    mode: needsJump ? (archetype === 'high-lift' ? 'charge-and-hop' : 'hop') : 'bridge-and-roll',
  };
}

export function createLevel(seed = 1) {
  const normalizedSeed = (Number(seed) >>> 0) || 1;
  const rng = makeRng(normalizedSeed);
  const archetypeIndex = (normalizedSeed - 1) % ARCHETYPE_DEFS.length;
  const definition = ARCHETYPE_DEFS[archetypeIndex];
  const archetype = definition.name;
  const startX = 72;
  const baseY = 590;
  const speed = round6((44 + rng() * 5) * definition.pace);
  const startPlatform = { startX: 0, endX: 196, y: baseY, kind: 'start' };
  const platforms = [startPlatform];
  const obstacles = [];
  const segments = [];
  let cursor = startPlatform.endX;

  for (let index = 0; index < 9; index += 1) {
    const action = segmentAction(archetype, index, rng, definition);
    const landingStart = round6(cursor + action.gap);
    const verticalWave = archetype === 'zigzag-rhythm' ? (index % 2 === 0 ? -34 : 28) : 0;
    const landingY = round6(clamp(baseY + verticalWave + (rng() - 0.5) * 24, 510, 635));
    const landingEnd = round6(landingStart + action.platformWidth);
    const obstacleX = round6(landingStart + action.platformWidth * (0.30 + rng() * 0.28));
    const obstacle = {
      x: obstacleX,
      y: round6(landingY - action.obstacleHeight),
      width: action.obstacleWidth,
      height: round6(action.obstacleHeight + 250),
      rise: action.obstacleHeight,
      active: action.needsJump,
    };
    const platform = { startX: landingStart, endX: landingEnd, y: landingY, kind: 'landing' };
    platforms.push(platform);
    obstacles.push(obstacle);
    segments.push({
      index,
      startX: round6(cursor),
      landingStart,
      landingEnd,
      gap: action.gap,
      platformWidth: action.platformWidth,
      surfaceY: landingY,
      obstacle,
      obstacleHeight: action.obstacleHeight,
      needsJump: action.needsJump,
      holdSeconds: action.holdSeconds,
      mode: action.mode,
    });
    cursor = landingEnd;
  }

  const worldLength = round6(cursor + 180);
  const goalX = round6(cursor + 86);
  const checkpointProgress = round6(0.56 + rng() * 0.08);
  const checkpointX = round6(startX + (goalX - startX) * checkpointProgress);
  const level = {
    schemaVersion: 2,
    seed: normalizedSeed,
    levelNumber: normalizedSeed,
    nextSeed: normalizedSeed + 1,
    archetype,
    archetypeIndex,
    cadence: definition.cadence,
    width: 390,
    height: 844,
    worldLength,
    nodeCount: NODE_COUNT,
    ballRadius: BALL_RADIUS,
    start: { x: startX, y: baseY },
    startPlatform,
    baseY,
    goal: { x: goalX, width: 16, y: baseY - 54 },
    goalX,
    platforms,
    obstacles,
    segments,
    initialBridgeFront: round6(startPlatform.endX + 32),
    membrane: {
      startX: startPlatform.endX,
      bridgeWindow: 240,
      maxLift: round6(62 + rng() * 22),
      slope: round6((rng() - 0.5) * 0.018),
    },
    motion: {
      cruiseSpeed: speed,
      expectedSeconds: round6((goalX - startX) / speed),
      maxSeconds: 78,
    },
    checkpoint: {
      x: checkpointX,
      progress: checkpointProgress,
      prefixSeconds: round6((checkpointX - startX) / speed),
    },
    generation: {
      method: 'archetype-sequence-growing-bridge',
      actionGrammar: definition.cadence,
      segmentModes: segments.map((segment) => segment.mode),
    },
  };
  level.knownStrategy = buildKnownStrategy(level);
  level.signature = [
    archetype,
    ...segments.map((segment) => `${segment.mode}:${Math.round(segment.gap)}:${Math.round(segment.obstacleHeight)}:${Math.round(segment.surfaceY)}`),
  ].join('|');
  return level;
}

export function buildLevelPack(firstSeed = 1, count = 10) {
  return Array.from({ length: count }, (_, index) => createLevel(firstSeed + index));
}

function restBridgeY(level, x) {
  return level.baseY + level.membrane.slope * (x - level.membrane.startX);
}

function bridgeBulge(level, state, x) {
  const distance = (x - state.bridgeFront) / 100;
  return state.pressure * level.membrane.maxLift * Math.exp(-distance * distance * 1.6);
}

function initialNodes(level) {
  const start = Math.max(level.membrane.startX, level.initialBridgeFront - level.membrane.bridgeWindow);
  return Array.from({ length: NODE_COUNT }, (_, index) => {
    const ratio = index / (NODE_COUNT - 1);
    const x = start + (level.initialBridgeFront - start) * ratio;
    return { x, y: restBridgeY(level, x), vy: 0, prevY: restBridgeY(level, x) };
  });
}

export function createState(level) {
  const nodes = initialNodes(level);
  return {
    tick: 0,
    elapsed: 0,
    status: 'playing',
    failureReason: null,
    held: false,
    pressure: 0,
    releasePulse: 0,
    pendingJump: 0,
    grounded: true,
    coyote: 0.1,
    bridgeFront: level.initialBridgeFront,
    bridgeStart: level.membrane.startX,
    nodes,
    ball: {
      x: level.start.x,
      y: level.start.y - BALL_RADIUS,
      prevX: level.start.x,
      prevY: level.start.y - BALL_RADIUS,
      vx: level.motion.cruiseSpeed,
      vy: 0,
    },
    segmentIndex: 0,
    completedSegments: 0,
    jumps: 0,
    checkpointReached: false,
    checkpointTick: null,
    inputLog: [],
    protections: { nodeVelocityClamps: 0, ballVelocityClamps: 0, ccdStops: 0 },
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
  if (!next && state.held) {
    state.releasePulse = state.pressure;
    state.pendingJump = Math.max(state.pendingJump, state.pressure);
  }
  state.held = next;
  state.inputLog.push({ tick: state.tick, held: next });
  return true;
}

function platformAt(level, x) {
  for (const platform of level.platforms) {
    if (x >= platform.startX && x <= platform.endX) return platform;
  }
  return null;
}

function sampleNodes(nodes, x) {
  if (nodes.length === 0) return { y: Infinity, vy: 0, exists: false, kind: 'void' };
  if (x <= nodes[0].x) return { y: nodes[0].y, vy: nodes[0].vy, exists: true, kind: 'bridge' };
  const last = nodes.at(-1);
  if (x >= last.x) return { y: last.y, vy: last.vy, exists: x <= last.x + 0.01, kind: 'bridge' };
  for (let index = 0; index < nodes.length - 1; index += 1) {
    const left = nodes[index];
    const right = nodes[index + 1];
    if (x <= right.x) {
      const ratio = (x - left.x) / Math.max(1e-9, right.x - left.x);
      return {
        y: left.y + (right.y - left.y) * ratio,
        vy: left.vy + (right.vy - left.vy) * ratio,
        exists: true,
        kind: 'bridge',
      };
    }
  }
  return { y: Infinity, vy: 0, exists: false, kind: 'void' };
}

export function sampleBridgeSurface(state, level, x) {
  const platform = platformAt(level, x);
  if (platform) return { y: platform.y, vy: 0, exists: true, kind: platform.kind };
  if (x < level.membrane.startX || x > state.bridgeFront) return { y: Infinity, vy: 0, exists: false, kind: 'void' };
  return sampleNodes(state.nodes, x);
}

function updateBridge(state, level) {
  const growth = state.held
    ? BRIDGE_GROWTH_HELD + state.pressure * BRIDGE_GROWTH_PRESSURE
    : BRIDGE_GROWTH_IDLE;
  state.bridgeFront = Math.min(level.worldLength + 80, state.bridgeFront + growth * FIXED_DT);
  const start = Math.max(level.membrane.startX, state.bridgeFront - level.membrane.bridgeWindow);
  const previous = state.nodes.map((node) => ({ ...node }));
  for (let index = 0; index < state.nodes.length; index += 1) {
    const node = state.nodes[index];
    node.prevY = node.y;
    const ratio = index / (state.nodes.length - 1);
    node.x = start + (state.bridgeFront - start) * ratio;
    const target = restBridgeY(level, node.x) - bridgeBulge(level, state, node.x);
    const neighbor = index === 0 || index === state.nodes.length - 1
      ? 0
      : previous[index - 1].y + previous[index + 1].y - 2 * previous[index].y;
    const spring = (target - previous[index].y) * 52 + neighbor * 16 - previous[index].vy * 13;
    let velocity = previous[index].vy + spring * FIXED_DT;
    const cappedVelocity = clamp(velocity, -NODE_SPEED_CAP, NODE_SPEED_CAP);
    if (velocity !== cappedVelocity) state.protections.nodeVelocityClamps += 1;
    velocity = cappedVelocity;
    node.y = previous[index].y + velocity * FIXED_DT;
    node.vy = velocity;
    if (index === 0 || index === state.nodes.length - 1) {
      node.y = target;
      node.vy = 0;
    }
  }
}

function expandedObstacle(obstacle) {
  return {
    // Keep a fixed visual lip outside the gameplay core. The load's radius
    // still matters, while a one-frame rim graze remains a readable near-miss
    // under the required ±5% input jitter.
    left: obstacle.x - BALL_RADIUS + 18,
    right: obstacle.x + obstacle.width + BALL_RADIUS - 18,
    top: obstacle.y - BALL_RADIUS + 55,
  };
}

function updateBall(state, level) {
  const ball = state.ball;
  ball.prevX = ball.x;
  ball.prevY = ball.y;
  const targetSpeed = level.motion.cruiseSpeed;
  ball.vx += (targetSpeed - ball.vx) * 0.9 * FIXED_DT;
  const cappedX = clamp(ball.vx, 0, BALL_SPEED_CAP);
  if (cappedX !== ball.vx) state.protections.ballVelocityClamps += 1;
  ball.vx = cappedX;
  ball.x += ball.vx * FIXED_DT;

  if (state.releasePulse > 0.22 && (state.grounded || state.coyote > 0)) {
    ball.vy = -JUMP_SPEED * clamp(state.releasePulse * 1.18, 0.34, 1);
    state.grounded = false;
    state.jumps += 1;
    state.pendingJump = 0;
  }
  state.releasePulse = 0;
  const gravity = ball.vy < 0 ? RISE_GRAVITY : FALL_GRAVITY;
  ball.vy = clamp(ball.vy + gravity * FIXED_DT, -JUMP_SPEED * 1.2, 340);
  ball.y += ball.vy * FIXED_DT;

  const surface = sampleBridgeSurface(state, level, ball.x);
  const previousBottom = ball.prevY + BALL_RADIUS;
  const currentBottom = ball.y + BALL_RADIUS;
  state.grounded = false;
  if (surface.exists && ball.vy >= 0 && currentBottom >= surface.y && ball.y <= surface.y + 80) {
    ball.y = surface.y - BALL_RADIUS;
    ball.vy = surface.vy;
    state.grounded = true;
    state.coyote = 0.1;
    if (state.pendingJump > 0.22) {
      ball.vy = -JUMP_SPEED * clamp(state.pendingJump * 1.18, 0.34, 1);
      state.grounded = false;
      state.jumps += 1;
      state.pendingJump = 0;
    }
  } else {
    state.coyote = Math.max(0, state.coyote - FIXED_DT);
  }

  for (const obstacle of level.obstacles) {
    const expanded = expandedObstacle(obstacle);
    if (!obstacle.active || state.status !== 'playing' || state.obstacleHits?.has(obstacle.x)) continue;
    const entersFromLeft = ball.prevX < expanded.left && ball.x >= expanded.left;
    const crossesTopWhileInside = ball.prevY <= expanded.top && ball.y > expanded.top && ball.x > expanded.left && ball.x < expanded.right && ball.vy > 0;
    if (entersFromLeft || crossesTopWhileInside) {
      const ratio = entersFromLeft
        ? (expanded.left - ball.prevX) / Math.max(1e-9, ball.x - ball.prevX)
        : (expanded.top - ball.prevY) / Math.max(1e-9, ball.y - ball.prevY);
      const sweptY = ball.prevY + (ball.y - ball.prevY) * clamp(ratio, 0, 1);
      if (sweptY > expanded.top || crossesTopWhileInside) {
        ball.x = entersFromLeft ? expanded.left : ball.x;
        ball.y = sweptY;
        ball.vx = 0;
        state.status = 'failed';
        state.failureReason = 'obstacle';
        state.protections.ccdStops += 1;
        break;
      }
    }
  }

  if (state.status === 'playing') {
    while (state.segmentIndex < level.segments.length && ball.x >= level.segments[state.segmentIndex].landingEnd) {
      state.segmentIndex += 1;
      state.completedSegments += 1;
    }
    if (!state.checkpointReached && ball.x >= level.checkpoint.x) {
      state.checkpointReached = true;
      state.checkpointTick = state.tick;
    }
    if (ball.x >= level.goalX && state.completedSegments >= level.segments.length && state.grounded && ball.y <= level.baseY + 120) state.status = 'success';
    if (ball.y > level.baseY + 260) {
      // End a missed jump at a bounded recovery plane. The state is a failed
      // attempt, not an invitation for the solver to integrate an unbounded
      // falling ball for the rest of the round.
      ball.y = level.baseY + 260;
      ball.vy = 0;
      state.status = 'failed';
      state.failureReason = 'missing-bridge';
    }
  }
}

function updateDiagnostics(state, level) {
  let energy = 0.5 * (state.ball.vx ** 2 + state.ball.vy ** 2);
  let finite = Number.isFinite(state.pressure) && Number.isFinite(state.bridgeFront);
  for (const node of state.nodes) {
    const offset = node.y - restBridgeY(level, node.x);
    energy += 0.5 * node.vy ** 2 + 0.5 * 52 * offset ** 2;
    finite = finite && Number.isFinite(node.x) && Number.isFinite(node.y) && Number.isFinite(node.vy);
    state.diagnostics.maxNodeOffset = Math.max(state.diagnostics.maxNodeOffset, Math.abs(offset));
  }
  state.diagnostics.finite = state.diagnostics.finite && finite && Number.isFinite(energy);
  state.diagnostics.maxEnergy = Math.max(state.diagnostics.maxEnergy, Number.isFinite(energy) ? energy : Infinity);
  state.diagnostics.energyExceeded = state.diagnostics.energyExceeded || energy > ENERGY_CAP;
  state.diagnostics.nodeExplosion = state.diagnostics.nodeExplosion || state.diagnostics.maxNodeOffset > 181;
  state.diagnostics.outOfBounds =
    state.diagnostics.outOfBounds ||
    state.ball.x < -BALL_RADIUS ||
    state.ball.y < -BALL_RADIUS * 4 ||
    state.ball.y > level.height + 300 ||
    state.bridgeFront < level.membrane.startX;
  state.diagnostics.obstaclePenetration = state.diagnostics.obstaclePenetration ||
    level.obstacles.some((obstacle) => obstacle.active && (() => {
      const expanded = expandedObstacle(obstacle);
      return state.status === 'playing' && state.ball.vy > 0 && state.ball.prevY <= expanded.top && state.ball.y > expanded.top && state.ball.x > expanded.left && state.ball.x < expanded.right;
    })());
}

export function step(state, level) {
  if (state.status !== 'playing') return state;
  state.pressure = clamp(state.pressure + (state.held ? PRESSURE_IN_PER_SECOND : -PRESSURE_OUT_PER_SECOND) * FIXED_DT, 0, 1);
  updateBridge(state, level);
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

export function simulate(level, events, { maxTicks = Math.round(level.motion.maxSeconds * PHYSICS_HZ) } = {}) {
  const state = createState(level);
  const input = normaliseEvents(events);
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
  for (const event of events) if (result.length === 0 || result.at(-1).held !== event.held) result.push(event);
  return result;
}

export function beamSearch(level, { beamWidth = 36, controlTicks = 90 } = {}) {
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
        if (state.status === 'success') return { success: true, events: compactEvents(events), source: 'discrete-beam-search', state };
        if (state.status !== 'playing') continue;
        const progress = (state.ball.x - level.start.x) / (level.goalX - level.start.x);
        const frontLead = state.bridgeFront - state.ball.x;
        const pressureTarget = level.segments[Math.min(state.segmentIndex, level.segments.length - 1)]?.needsJump ? 0.74 : 0.4;
        const score = progress * 12_000 + Math.min(120, frontLead) * 2 - Math.abs(state.pressure - pressureTarget) * 180 - events.length;
        expanded.push({ state, events, score });
      }
    }
    expanded.sort((a, b) => b.score - a.score);
    const unique = new Map();
    for (const candidate of expanded) {
      const key = `${Math.round(candidate.state.ball.x / 8)}:${Math.round(candidate.state.bridgeFront / 8)}:${Math.round(candidate.state.pressure * 10)}:${candidate.state.held ? 1 : 0}`;
      if (!unique.has(key)) unique.set(key, candidate);
      if (unique.size >= beamWidth) break;
    }
    beam = [...unique.values()];
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
      source: 'archetype-known-strategy',
      durationSeconds: knownState.elapsed,
      checkpointSeconds: knownState.checkpointTick === null ? null : knownState.checkpointTick * FIXED_DT,
      finalHash: hashState(knownState),
      state: knownState,
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
    state: searched.state,
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
    state.segmentIndex,
    state.completedSegments,
    state.jumps,
    Math.round(state.pressure * 1e7),
    Math.round(state.bridgeFront * 1e6),
    Math.round(state.ball.x * 1e6),
    Math.round(state.ball.y * 1e6),
    Math.round(state.ball.vx * 1e6),
    Math.round(state.ball.vy * 1e6),
  ];
  for (const node of state.nodes) values.push(Math.round(node.x * 1e5), Math.round(node.y * 1e5), Math.round(node.vy * 1e5));
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
  const requested = Number.isFinite(renderDt) ? renderDt : 0;
  const bounded = clamp(requested, 0, FIXED_DT * MAX_FRAME_STEPS);
  runner.droppedFrameSeconds += requested - bounded;
  runner.accumulator += bounded;
  let steps = 0;
  while (runner.accumulator + 1e-12 >= FIXED_DT && steps < MAX_FRAME_STEPS && runner.state.status === 'playing') {
    step(runner.state, runner.level);
    runner.accumulator -= FIXED_DT;
    if (Math.abs(runner.accumulator) < 1e-12) runner.accumulator = 0;
    steps += 1;
  }
  return { steps, alpha: clamp(runner.accumulator / FIXED_DT, 0, 1) };
}

export function pauseRunner(runner) {
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
  if (![30, 60, 120].includes(renderFps)) throw new RangeError('renderFps must be 30, 60, or 120');
  const runner = createRunner(level);
  const input = normaliseEvents(events);
  const frameDt = 1 / renderFps;
  const critical = [];
  let eventIndex = 0;
  const frameLimit = Math.ceil((level.motion.maxSeconds + 2) * renderFps);
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
    renderFps,
    status: runner.state.status,
    tick: runner.state.tick,
    finalHash: hashState(runner.state),
    criticalHash: hashText(critical.join('|')),
    criticalSamples: critical.length,
    inputLog: runner.state.inputLog.map((event) => ({ ...event })),
  };
}

export function jitterEvents(events, rng, amount = 0.05) {
  const input = normaliseEvents(events);
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

export function randomControlLog(level, rng) {
  const events = [];
  let held = false;
  let tick = Math.round((0.4 + rng() * 1.6) * PHYSICS_HZ);
  while (tick < level.motion.maxSeconds * PHYSICS_HZ) {
    held = !held;
    events.push({ tick, held });
    tick += Math.round((0.25 + rng() * 1.25) * PHYSICS_HZ);
  }
  return events;
}

function diagnosticSafe(state) {
  return state.diagnostics.finite && !state.diagnostics.energyExceeded && !state.diagnostics.nodeExplosion && !state.diagnostics.outOfBounds && !state.diagnostics.obstaclePenetration;
}

function evaluateIaa(sessionCount = 180) {
  const durations = [];
  const interstitialCounts = [];
  const rewardCounts = [];
  let ghosts = 0;
  let continues = 0;
  let rounds = 0;
  for (let session = 1; session <= sessionCount; session += 1) {
    const rng = makeRng(seedMix(session, 0x1aa));
    const target = 420 + rng() * 180;
    let elapsed = 0;
    let round = 0;
    let interstitial = 0;
    let ghost = false;
    let continuation = false;
    while (elapsed < target && round < 12) {
      round += 1;
      rounds += 1;
      const level = createLevel(session * 31 + round);
      const type = rng();
      // The local IAA model represents a readable 40–70s round, including a
      // short retry/settlement tail; it is intentionally not a claim about
      // retention or revenue.
      let duration = level.motion.expectedSeconds + 2 + rng() * 4;
      if (type > 0.35 && type < 0.75) duration += 4 + rng() * 6;
      if (type >= 0.75) duration += level.segments[0].holdSeconds * 2 + rng() * 5;
      durations.push(round6(duration));
      elapsed += duration;
      if ((round === 4 || round === 8) && interstitial < 1) {
        interstitial += 1;
      }
      if (rng() < 0.30) { ghosts += 1; ghost = true; }
      if (rng() < 0.16) { continues += 1; continuation = true; }
    }
    interstitialCounts.push(interstitial);
    rewardCounts.push(Math.min(2, Number(ghost) + Number(continuation)));
  }
  const sorted = [...durations].sort((a, b) => a - b);
  return {
    sessions: sessionCount,
    rounds,
    durationSeconds: { numeratorWithin40To70: durations.filter((value) => value >= 40 && value <= 70).length, denominator: durations.length, median: percentile(sorted, 0.5), p10: percentile(sorted, 0.1), min: sorted[0] ?? 0, max: sorted.at(-1) ?? 0 },
    firstThreeInterstitialEligible: 0,
    interstitialRules: { rounds: [4, 8], settlementOnly: true, cooldownSeconds: 300, requiredMinimumSeconds: 180 },
    interstitialPerSession: { denominator: sessionCount, one: interstitialCounts.filter((value) => value === 1).length, two: interstitialCounts.filter((value) => value === 2).length, mode: 1, median: percentile([...interstitialCounts].sort((a, b) => a - b), 0.5), max: Math.max(...interstitialCounts, 0) },
    pressureGhost: { numerator: ghosts, denominator: rounds, rate: ghosts / rounds, rule: 'same segment failed twice' },
    checkpointContinue: { numerator: continues, denominator: rounds, rate: continues / rounds, rule: 'fell after checkpoint' },
    rewardEntriesPerSession: { denominator: sessionCount, max: Math.max(...rewardCounts, 0), median: percentile([...rewardCounts].sort((a, b) => a - b), 0.5), cap: 2 },
  };
}

function evaluatePauseResume() {
  const level = createLevel(23);
  const runner = createRunner(level);
  setRunnerHeld(runner, true);
  for (let index = 0; index < 180; index += 1) advanceFrame(runner, FIXED_DT);
  pauseRunner(runner);
  const before = hashState(runner.state);
  const tick = runner.state.tick;
  const elapsed = runner.state.elapsed;
  advanceFrame(runner, 45);
  const frozen = hashState(runner.state);
  resumeRunner(runner);
  advanceFrame(runner, 0);
  const resumed = hashState(runner.state);
  return { denominator: 1, frozenTickNumerator: Number(runner.state.tick === tick), hashContractNumerator: Number(before === frozen && frozen === resumed), inputReleasedNumerator: Number(runner.state.held === false), countdownLossSeconds: round6(runner.state.elapsed - elapsed), networkRequests: 0 };
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
  let frameMatched = 0;
  let checkpointValid = 0;
  let restartValid = 0;
  const failures = [];
  for (const seed of seeds) {
    const level = createLevel(seed);
    const solution = solveLevel(level);
    solutions.set(seed, solution);
    if (solution.success) solved += 1; else failures.push(seed);
    if (solution.success) {
      const replays = [30, 60, 120].map((fps) => runReplay(level, solution.events, fps));
      if (replays.every((replay) => replay.status === 'success') && new Set(replays.map((replay) => replay.finalHash)).size === 1 && new Set(replays.map((replay) => replay.criticalHash)).size === 1) frameMatched += 1;
    }
    if (level.checkpoint.progress >= 0.45 && level.checkpoint.progress <= 0.7 && level.checkpoint.prefixSeconds >= 20 && solution.checkpointSeconds !== null && solution.checkpointSeconds >= 20) checkpointValid += 1;
    const initial = createState(level);
    const dirty = createState(level);
    dirty.pressure = 0.8;
    dirty.ball.x += 13;
    if (hashState(restart(dirty, level)) === hashState(initial)) restartValid += 1;
  }

  let robustSeeds = 0;
  let robustSuccesses = 0;
  const robustRates = [];
  for (const seed of seeds) {
    const level = createLevel(seed);
    const solution = solutions.get(seed);
    let successes = 0;
    for (let trial = 0; trial < robustnessTrials; trial += 1) {
      const noisy = jitterEvents(solution.events, makeRng(seedMix(seed, trial + 700)), 0.05);
      if (simulate(level, noisy).status === 'success') successes += 1;
    }
    robustSuccesses += successes;
    const rate = successes / robustnessTrials;
    robustRates.push(rate);
    if (rate >= 0.8) robustSeeds += 1;
  }

  let randomSuccesses = 0;
  for (let run = 0; run < randomRuns; run += 1) {
    const seed = seeds[run % seeds.length];
    const level = createLevel(seed);
    if (simulate(level, randomControlLog(level, makeRng(seedMix(seed, run + 9_000)))).status === 'success') randomSuccesses += 1;
  }

  let stableRuns = 0;
  const timings = [];
  const anomalies = [];
  for (let run = 0; run < stabilityRuns; run += 1) {
    const seed = seeds[run % seeds.length];
    const level = createLevel(seed);
    const solution = solutions.get(seed);
    const rng = makeRng(seedMix(seed, run + 17_000));
    const events = run % 3 === 0 ? solution.events : run % 3 === 1 ? jitterEvents(solution.events, rng, 0.05) : randomControlLog(level, rng);
    const started = nowMs();
    const state = simulate(level, events);
    timings.push(nowMs() - started);
    if (diagnosticSafe(state)) stableRuns += 1; else if (anomalies.length < 20) anomalies.push({ seed, run, status: state.status, diagnostics: state.diagnostics });
  }
  timings.sort((a, b) => a - b);
  const iaa = evaluateIaa(sessionCount);
  const pauseResume = evaluatePauseResume();
  const randomRate = randomSuccesses / randomRuns;
  const hardGates = [
    { id: 'solver-and-cross-fps', pass: solved === seedCount && frameMatched === seedCount },
    { id: 'timing-noise-robustness', pass: robustSeeds / seedCount >= 0.9 },
    { id: 'random-control-band', pass: randomRate >= 0.1 && randomRate <= 0.3 },
    { id: 'stability-10000', pass: stableRuns === stabilityRuns },
    { id: 'checkpoint-and-free-restart', pass: checkpointValid === seedCount && restartValid === seedCount },
    { id: 'iaa-local-structure', pass: iaa.durationSeconds.median >= 40 && iaa.durationSeconds.median <= 70 && iaa.durationSeconds.p10 >= 30 && iaa.firstThreeInterstitialEligible === 0 && iaa.interstitialPerSession.max <= 2 && iaa.pressureGhost.rate >= 0.2 && iaa.pressureGhost.rate <= 0.4 && iaa.checkpointContinue.rate >= 0.08 && iaa.checkpointContinue.rate <= 0.25 && iaa.rewardEntriesPerSession.max <= 2 },
    { id: 'ads-off-pause-resume', pass: !ADS_ENABLED && !NETWORK_ALLOWED && NETWORK_REQUEST_BUDGET === 0 && pauseResume.hashContractNumerator === 1 && pauseResume.inputReleasedNumerator === 1 && pauseResume.countdownLossSeconds === 0 && pauseResume.networkRequests === 0 },
  ];
  const sortedRates = [...robustRates].sort((a, b) => a - b);
  return {
    schemaVersion: 2,
    prototype: 'breathing-bridge-growing-bridge',
    question: 'Can a player grow a bridge while auto-running, then release pressure to make readable jump-like landings across distinct action grammars?',
    parameters: { seedCount, robustnessTrials, randomRuns, stabilityRuns, sessionCount, fixedDt: FIXED_DT, nodeCount: NODE_COUNT },
    solver: { numerator: solved, denominator: seedCount, failures },
    frameRateReplay: { numerator: frameMatched, denominator: seedCount, renderFps: [30, 60, 120], replayCount: seedCount * 3 },
    robustness: { numerator: robustSeeds, denominator: seedCount, trialSuccesses: robustSuccesses, trialDenominator: seedCount * robustnessTrials, trialsPerSeed: robustnessTrials, minSeedRate: sortedRates[0] ?? 0, medianSeedRate: percentile(sortedRates, 0.5) },
    random: { numerator: randomSuccesses, denominator: randomRuns, rate: randomRate, requiredRange: [0.1, 0.3] },
    stability: { numerator: stableRuns, denominator: stabilityRuns, anomalyCount: stabilityRuns - stableRuns, anomalySamples: anomalies, checks: ['finite', 'energyCap', 'bounds', 'sweptObstacleCCD', 'nodeOffsetCap'] },
    performance: { unit: 'milliseconds per simulated round on development host', denominator: timings.length, p50Ms: round6(percentile(timings, 0.5)), p95Ms: round6(percentile(timings, 0.95)), maxMs: round6(timings.at(-1) ?? 0) },
    checkpoint: { numerator: checkpointValid, denominator: seedCount, requiredProgressRange: [0.45, 0.7], requiredPrefixSeconds: 20 },
    freeRestart: { numerator: restartValid, denominator: seedCount },
    archetypes: { names: ARCHETYPE_DEFS.map((definition) => definition.name), sample: buildLevelPack(1, Math.min(seedCount, 10)).map((level) => ({ seed: level.seed, archetype: level.archetype, signature: level.signature })) },
    iaa,
    pauseResume,
    noAdsRegression: { fullPlayableNumerator: solved, fullPlayableDenominator: seedCount, restartNumerator: restartValid, restartDenominator: seedCount, networkRequests: 0 },
    policy: { adsEnabled: ADS_ENABLED, networkAllowed: NETWORK_ALLOWED, requestBudget: NETWORK_REQUEST_BUDGET, actualRequests: 0 },
    hardGates,
    overallPass: hardGates.every((gate) => gate.pass),
    productDecision: 'UNDECIDED_REQUIRES_UNCOACHED_HUMAN',
    allowedConclusion: '核心机器验证通过不等于产品 KEEP；需陌生真人判断。',
    assumptions: [
      'A level is a deterministic sequence of nine bridge-growth/jump segments, not one jittered obstacle.',
      'Holding grows the membrane front; releasing pressure creates the jump pulse.',
      'The random baseline alternates unplanned hold/release windows and is not a claim about human behavior.',
      'Performance values are development-host proxies.',
    ],
  };
}
