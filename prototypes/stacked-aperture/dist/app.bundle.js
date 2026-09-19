(() => {
'use strict';
// prng.js
function hashSeed(seed) {
  const text = String(seed);
  let hash = 0x811c9dc5;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= text.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return hash >>> 0;
}

function createRng(seed) {
  let state = hashSeed(seed) || 0x6d2b79f5;
  const next = () => {
    state = (state + 0x6d2b79f5) >>> 0;
    let value = state;
    value = Math.imul(value ^ (value >>> 15), value | 1);
    value ^= value + Math.imul(value ^ (value >>> 7), value | 61);
    return ((value ^ (value >>> 14)) >>> 0) / 4_294_967_296;
  };
  return {
    next,
    int(min, max) {
      if (!Number.isInteger(min) || !Number.isInteger(max) || max < min) {
        throw new RangeError('integer bounds must be ordered integers');
      }
      return min + Math.floor(next() * (max - min + 1));
    },
  };
}

// model.js
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

function generateLevel(seed) {
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

function applyOffsetMove(level, offsets, move) {
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

function dropBead(level, offsets) {
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

function createGameState(level) {
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

function moveGameState(level, state, move) {
  if (state.status !== 'ready') throw new Error('cannot move while result is unresolved');
  return {
    ...state,
    offsets: applyOffsetMove(level, state.offsets, move),
    moveCount: state.moveCount + 1,
    lastDrop: null,
  };
}

function attemptDrop(level, state) {
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

function retryLevel(state) {
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

function serializeGameState(state) {
  return JSON.stringify(canonicalize(state));
}

function deserializeGameState(serialized) {
  const state = JSON.parse(serialized);
  if (state?.version !== 1 || !Array.isArray(state.offsets)) {
    throw new TypeError('unsupported game state');
  }
  return state;
}

function hashGameState(state) {
  const serialized = serializeGameState(state);
  let hash = 0x811c9dc5;
  for (let index = 0; index < serialized.length; index += 1) {
    hash ^= serialized.charCodeAt(index);
    hash = Math.imul(hash, 0x01000193);
  }
  return (hash >>> 0).toString(16).padStart(8, '0');
}

// input.js
function snapDragToSteps(deltaPx, spacingPx) {
  if (!Number.isFinite(deltaPx) || !Number.isFinite(spacingPx) || spacingPx <= 0) {
    throw new TypeError('drag and spacing must be finite, with positive spacing');
  }
  return Math.sign(deltaPx) * Math.floor(Math.abs(deltaPx) / spacingPx + 0.5);
}

function snapCatchWidth(spacingPx) {
  if (!Number.isFinite(spacingPx) || spacingPx <= 0) throw new TypeError('spacing must be positive');
  return spacingPx;
}

// layout.js
function computeLayout(width, height, layerCount) {
  if (width <= 0 || height <= 0 || ![3, 4].includes(layerCount)) {
    throw new RangeError('layout requires a positive viewport and 3–4 layers');
  }
  const safeTop = 18;
  const safeBottom = 16;
  const side = Math.max(18, Math.round(width * 0.055));
  const controls = {
    x: side,
    y: height - safeBottom - 116,
    width: width - side * 2,
    height: 104,
  };
  const board = {
    x: side,
    y: 172,
    width: width - side * 2,
    height: Math.min(430, controls.y - 192),
  };
  const pitch = board.height / layerCount;
  const hitHeight = Math.max(54, Math.min(72, pitch - 12));
  const sheetHeight = Math.min(48, hitHeight - 8);
  const layers = Array.from({ length: layerCount }, (_, index) => {
    const hitTop = board.y + index * pitch + (pitch - hitHeight) / 2;
    return {
      index,
      hitTop,
      hitHeight,
      centerY: hitTop + hitHeight / 2,
      sheetHeight,
    };
  });
  return {
    width,
    height,
    safeTop,
    safeBottom,
    header: { x: side, y: safeTop, width: width - side * 2, height: 130 },
    board,
    layers,
    controls,
  };
}

// eligibility.js
function createEligibilitySession({ placeholdersEnabled = false } = {}) {
  return {
    placeholdersEnabled,
    wallClockMs: 0,
    gameClockMs: 0,
    roundNumber: 0,
    page: 'play',
    lastFullscreenEligibilityMs: null,
    lastRewardResumeMs: null,
    interstitialEligibilityCount: 0,
    rewardEligibilityCount: 0,
    adSimulation: null,
  };
}

function advanceClock(session, durationMs) {
  if (!Number.isFinite(durationMs) || durationMs < 0) throw new RangeError('duration must be non-negative');
  return {
    ...session,
    wallClockMs: session.wallClockMs + durationMs,
    gameClockMs: session.gameClockMs + (session.adSimulation ? 0 : durationMs),
  };
}

function completeRound(session, durationMs) {
  let next = advanceClock({ ...session, page: 'play' }, durationMs);
  next = { ...next, roundNumber: next.roundNumber + 1, page: 'settlement' };
  const sinceFullscreen = next.gameClockMs - (next.lastFullscreenEligibilityMs ?? 0);
  const sinceReward = next.lastRewardResumeMs === null
    ? Number.POSITIVE_INFINITY
    : next.gameClockMs - next.lastRewardResumeMs;
  const eligible = next.placeholdersEnabled
    && next.roundNumber >= 4
    && sinceFullscreen >= 180_000
    && sinceReward >= 60_000
    && next.interstitialEligibilityCount < 2;
  if (!eligible) return { session: next, events: [] };
  const event = {
    type: 'interstitial_eligible',
    page: 'settlement',
    roundNumber: next.roundNumber,
    gameClockMs: next.gameClockMs,
  };
  next = {
    ...next,
    lastFullscreenEligibilityMs: next.gameClockMs,
    interstitialEligibilityCount: next.interstitialEligibilityCount + 1,
  };
  return { session: next, events: [event] };
}

function rewardEligibility(level, gameState, { viewDetails = false } = {}) {
  const blockerIndex = gameState.lastDrop?.blockerIndex;
  return {
    hint: gameState.status === 'blocked' && gameState.consecutiveFailures >= 2,
    diagnostic: gameState.status === 'blocked'
      && viewDetails
      && Number.isInteger(blockerIndex)
      && blockerIndex >= Math.ceil(level.layerCount / 2),
  };
}

function registerRewardEntry(session, type) {
  if (type !== 'hint' && type !== 'diagnostic') throw new TypeError('unsupported reward entry type');
  if (!session.placeholdersEnabled || session.rewardEligibilityCount >= 2) {
    return { session, events: [] };
  }
  const next = { ...session, rewardEligibilityCount: session.rewardEligibilityCount + 1 };
  return {
    session: next,
    events: [{
      type: `${type}_eligible`,
      page: session.page,
      gameClockMs: session.gameClockMs,
    }],
  };
}

function beginAdSimulation(session, type) {
  if (!session.placeholdersEnabled) throw new Error('ad placeholders are disabled');
  if (session.adSimulation) throw new Error('an ad simulation is already active');
  if (type !== 'rewarded' && type !== 'interstitial') throw new TypeError('unsupported ad simulation type');
  return { ...session, adSimulation: { type, startedAtWallClockMs: session.wallClockMs } };
}

function endAdSimulation(session) {
  if (!session.adSimulation) throw new Error('no ad simulation is active');
  return {
    ...session,
    lastRewardResumeMs: session.adSimulation.type === 'rewarded'
      ? session.gameClockMs
      : session.lastRewardResumeMs,
    adSimulation: null,
  };
}

// solver.js
function keyOf(offsets) {
  return offsets.join(',');
}

function solveLevel(level, startOffsets = level.initialOffsets) {
  const start = [...startOffsets];
  if (dropBead(level, start).passed) return { distance: 0, moves: [], offsets: start };
  const queue = [start];
  let cursor = 0;
  const parent = new Map([[keyOf(start), null]]);
  let goalKey = null;

  while (cursor < queue.length && goalKey === null) {
    const offsets = queue[cursor];
    cursor += 1;
    for (let layer = 0; layer < level.layerCount; layer += 1) {
      for (const delta of [-1, 1]) {
        const candidate = offsets[layer] + delta;
        if (candidate < level.slotMin || candidate > level.slotMax) continue;
        const move = { layer, delta };
        const next = applyOffsetMove(level, offsets, move);
        const key = keyOf(next);
        if (parent.has(key)) continue;
        parent.set(key, { previous: keyOf(offsets), move });
        if (dropBead(level, next).passed) {
          goalKey = key;
          break;
        }
        queue.push(next);
      }
      if (goalKey !== null) break;
    }
  }

  if (goalKey === null) return null;
  const moves = [];
  let key = goalKey;
  while (parent.get(key) !== null) {
    const entry = parent.get(key);
    moves.push(entry.move);
    key = entry.previous;
  }
  moves.reverse();
  return {
    distance: moves.length,
    moves,
    offsets: goalKey.split(',').map(Number),
  };
}

function applyMoves(level, offsets, moves) {
  return moves.reduce((current, move) => applyOffsetMove(level, current, move), [...offsets]);
}

function* enumerateOffsets(level) {
  const offsets = Array(level.layerCount).fill(level.slotMin);
  function* visit(index) {
    if (index === offsets.length) {
      yield [...offsets];
      return;
    }
    for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
      offsets[index] = slot;
      yield* visit(index + 1);
    }
  }
  yield* visit(0);
}

// app.js
const canvas = document.querySelector('canvas');
const context = canvas.getContext('2d');
const maskCanvas = document.createElement('canvas');
const maskContext = maskCanvas.getContext('2d');
const placeholdersEnabled = new URLSearchParams(window.location.search).get('eligibility') === '1';

let seed = 1_001;
let level = generateLevel(seed);
let state = createGameState(level);
let layout = null;
let drag = null;
let dropAnimation = null;
let logicalClockMs = 0;
let events = [];
let lastHintEligibilityDrop = -1;
let detailWasChosen = false;
let eligibilitySession = createEligibilitySession({ placeholdersEnabled });

function clone(value) {
  return JSON.parse(JSON.stringify(value));
}

function logEvent(type, payload = {}) {
  logicalClockMs += type === 'move' ? 1_000 : type === 'drop' ? 2_000 : 0;
  events.push({
    seq: events.length + 1,
    clockMs: logicalClockMs,
    seed: level.seed,
    type,
    payload,
    stateHash: hashGameState(state),
  });
}

function logRewardEntry(type) {
  const result = registerRewardEntry(eligibilitySession, type);
  eligibilitySession = result.session;
  for (const event of result.events) logEvent(event.type, { page: event.page });
}

function resetCurrent() {
  state = createGameState(level);
  dropAnimation = null;
  detailWasChosen = false;
  lastHintEligibilityDrop = -1;
  logEvent('reset');
}

function loadSeed(nextSeed) {
  seed = Number(nextSeed);
  level = generateLevel(seed);
  state = createGameState(level);
  if (layout) layout = computeLayout(layout.width, layout.height, level.layerCount);
  dropAnimation = null;
  detailWasChosen = false;
  lastHintEligibilityDrop = -1;
  logEvent('level_start', { seed: level.seed });
}

function controlRects() {
  const { controls } = layout;
  const reset = { x: controls.x, y: controls.y, width: 82, height: 52 };
  const primary = {
    x: controls.x + 92,
    y: controls.y,
    width: controls.width - 92,
    height: 52,
  };
  const optionalY = controls.y + 62;
  const rects = { reset };
  if (state.status === 'ready') rects.drop = primary;
  if (state.status === 'blocked') rects.retry = primary;
  if (state.status === 'passed') rects.next = primary;
  if (placeholdersEnabled && state.status === 'blocked') {
    rects.details = { x: controls.x, y: optionalY, width: controls.width * 0.48, height: 36 };
    if (state.consecutiveFailures >= 2) {
      rects.hint = {
        x: controls.x + controls.width * 0.52,
        y: optionalY,
        width: controls.width * 0.48,
        height: 36,
      };
    }
  }
  return rects;
}

function contains(rect, point) {
  return point.x >= rect.x && point.x <= rect.x + rect.width
    && point.y >= rect.y && point.y <= rect.y + rect.height;
}

function controlAt(point) {
  return Object.entries(controlRects()).find(([, rect]) => contains(rect, point))?.[0] ?? null;
}

function handleControl(name) {
  if (name === 'reset') {
    resetCurrent();
    return;
  }
  if (name === 'drop' && state.status === 'ready') {
    state = attemptDrop(level, state);
    dropAnimation = { startedAt: performance.now(), result: clone(state.lastDrop) };
    detailWasChosen = false;
    logEvent('drop', clone(state.lastDrop));
    const reward = rewardEligibility(level, state, { viewDetails: false });
    if (placeholdersEnabled && reward.hint && lastHintEligibilityDrop !== state.dropCount) {
      lastHintEligibilityDrop = state.dropCount;
      logRewardEntry('hint');
    }
    return;
  }
  if (name === 'retry' && state.status === 'blocked') {
    state = retryLevel(state);
    dropAnimation = null;
    detailWasChosen = false;
    logEvent('retry', { retainedOffsets: [...state.offsets] });
    return;
  }
  if (name === 'next' && state.status === 'passed') {
    loadSeed(seed + 1);
    return;
  }
  if (name === 'details' && state.status === 'blocked') {
    detailWasChosen = true;
    const reward = rewardEligibility(level, state, { viewDetails: true });
    if (reward.diagnostic) logRewardEntry('diagnostic');
    else logEvent('details_viewed_no_eligibility');
    return;
  }
  if (name === 'hint' && state.status === 'blocked' && state.consecutiveFailures >= 2) {
    logEvent('hint_button_chosen');
  }
}

function canvasPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: (event.clientX - rect.left) * (layout.width / rect.width),
    y: (event.clientY - rect.top) * (layout.height / rect.height),
  };
}

canvas.addEventListener('pointerdown', (event) => {
  event.preventDefault();
  const point = canvasPoint(event);
  const control = controlAt(point);
  if (control) {
    handleControl(control);
    return;
  }
  if (state.status !== 'ready') return;
  const layerHit = layout.layers.find((entry) => (
    point.y >= entry.hitTop && point.y <= entry.hitTop + entry.hitHeight
  ));
  if (!layerHit) return;
  drag = { pointerId: event.pointerId, layer: layerHit.index, startX: point.x, deltaPx: 0 };
  try {
    canvas.setPointerCapture(event.pointerId);
  } catch {
    // Synthetic touch smoke events need no capture.
  }
});

canvas.addEventListener('pointermove', (event) => {
  if (!drag || drag.pointerId !== event.pointerId) return;
  event.preventDefault();
  drag.deltaPx = canvasPoint(event).x - drag.startX;
});

canvas.addEventListener('pointerup', (event) => {
  if (!drag || drag.pointerId !== event.pointerId) return;
  event.preventDefault();
  const activeDrag = drag;
  drag = null;
  const requestedSteps = snapDragToSteps(canvasPoint(event).x - activeDrag.startX, level.slotSpacing);
  const current = state.offsets[activeDrag.layer];
  const target = Math.max(level.slotMin, Math.min(level.slotMax, current + requestedSteps));
  let remaining = target - current;
  while (remaining !== 0) {
    const delta = Math.sign(remaining);
    state = moveGameState(level, state, { layer: activeDrag.layer, delta });
    logEvent('move', { layer: activeDrag.layer, delta });
    remaining -= delta;
  }
});

canvas.addEventListener('pointercancel', () => {
  drag = null;
});

function resize() {
  const width = Math.max(320, Math.round(window.innerWidth));
  const height = Math.max(568, Math.round(window.innerHeight));
  const dpr = Math.min(2, window.devicePixelRatio || 1);
  canvas.width = Math.round(width * dpr);
  canvas.height = Math.round(height * dpr);
  maskCanvas.width = width;
  maskCanvas.height = height;
  canvas.style.width = `${width}px`;
  canvas.style.height = `${height}px`;
  context.setTransform(dpr, 0, 0, dpr, 0, 0);
  layout = computeLayout(width, height, level.layerCount);
}

window.addEventListener('resize', resize);
resize();

function roundedRect(ctx, x, y, width, height, radius) {
  ctx.beginPath();
  ctx.roundRect(x, y, width, height, radius);
}

function drawText(text, x, y, size, color = '#e8edf6', align = 'left', weight = 500) {
  context.fillStyle = color;
  context.font = `${weight} ${size}px system-ui, -apple-system, sans-serif`;
  context.textAlign = align;
  context.textBaseline = 'middle';
  context.fillText(text, x, y);
}

function drawButton(rect, label, emphasis = false) {
  roundedRect(context, rect.x, rect.y, rect.width, rect.height, 12);
  context.fillStyle = emphasis ? '#e8edf6' : '#222c3b';
  context.fill();
  context.strokeStyle = emphasis ? '#ffffff' : '#52627a';
  context.lineWidth = 1.5;
  context.stroke();
  drawText(label, rect.x + rect.width / 2, rect.y + rect.height / 2, 15, emphasis ? '#10151f' : '#e8edf6', 'center', 700);
}

function drawSheet(index) {
  const sheet = layout.layers[index];
  const layer = level.layers[index];
  const preview = drag?.layer === index ? drag.deltaPx : 0;
  const centerX = layout.width / 2 + state.offsets[index] * level.slotSpacing + preview;
  const sheetWidth = Math.min(342, layout.board.width - 8);
  const x = centerX - sheetWidth / 2;
  const y = sheet.centerY - sheet.sheetHeight / 2;

  maskContext.clearRect(0, 0, maskCanvas.width, maskCanvas.height);
  roundedRect(maskContext, x, y, sheetWidth, sheet.sheetHeight, 10);
  maskContext.fillStyle = layer.color;
  maskContext.globalAlpha = layer.alpha;
  maskContext.fill();
  maskContext.globalAlpha = 1;
  maskContext.globalCompositeOperation = 'destination-out';
  for (const hole of layer.holes) {
    const holeX = centerX + hole.localSlot * level.slotSpacing;
    maskContext.beginPath();
    maskContext.arc(holeX, sheet.centerY, hole.radius, 0, Math.PI * 2);
    maskContext.fill();
  }
  maskContext.globalCompositeOperation = 'source-over';
  context.drawImage(maskCanvas, 0, 0);

  roundedRect(context, x, y, sheetWidth, sheet.sheetHeight, 10);
  context.strokeStyle = layer.color;
  context.lineWidth = state.lastDrop?.blockerIndex === index ? 4 : 2;
  context.stroke();
  for (const hole of layer.holes) {
    const holeX = centerX + hole.localSlot * level.slotSpacing;
    context.beginPath();
    context.arc(holeX, sheet.centerY, hole.radius, 0, Math.PI * 2);
    context.strokeStyle = '#dce4f0';
    context.lineWidth = 1.5;
    context.stroke();
  }
  drawText(`L${index + 1}`, Math.max(18, x + 20), sheet.centerY, 13, layer.color, 'center', 800);
}

function drawBoard() {
  const centerX = layout.width / 2;
  context.strokeStyle = '#344155';
  context.lineWidth = 2;
  context.setLineDash([5, 8]);
  context.beginPath();
  context.moveTo(centerX, layout.board.y - 25);
  context.lineTo(centerX, layout.board.y + layout.board.height + 34);
  context.stroke();
  context.setLineDash([]);

  for (const sheet of layout.layers) {
    for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
      const x = centerX + slot * level.slotSpacing;
      context.beginPath();
      context.moveTo(x, sheet.centerY - 4);
      context.lineTo(x, sheet.centerY + 4);
      context.strokeStyle = slot === 0 ? '#708096' : '#364357';
      context.lineWidth = 1;
      context.stroke();
    }
  }

  for (let index = 0; index < level.layerCount; index += 1) drawSheet(index);

  let beadY = layout.board.y - 30;
  if (dropAnimation) {
    const targetY = dropAnimation.result.passed
      ? layout.board.y + layout.board.height + 24
      : layout.layers[dropAnimation.result.blockerIndex].centerY - level.beadRadius;
    const progress = Math.min(1, (performance.now() - dropAnimation.startedAt) / 750);
    const eased = 1 - (1 - progress) ** 3;
    beadY += (targetY - beadY) * eased;
  }
  context.beginPath();
  context.arc(centerX, beadY, level.beadRadius, 0, Math.PI * 2);
  context.fillStyle = state.status === 'blocked' ? '#ff6f78' : state.status === 'passed' ? '#8cf29a' : '#f4f7fb';
  context.shadowColor = context.fillStyle;
  context.shadowBlur = 12;
  context.fill();
  context.shadowBlur = 0;
}

function drawHud() {
  const shortest = solveLevel(level, state.offsets);
  drawText('叠孔通路', layout.header.x, layout.header.y + 16, 24, '#f4f7fb', 'left', 800);
  drawText('SHIFT / THROWAWAY GREYBOX', layout.header.x, layout.header.y + 45, 11, '#8fa1ba', 'left', 700);
  drawText(`SEED ${level.seed}  ·  ${level.layerCount} 层`, layout.header.x, layout.header.y + 76, 13, '#c9d3e1', 'left', 650);
  drawText(`当前最短 ${shortest?.distance ?? 0} 格  ·  移动 ${state.moveCount}  ·  放珠 ${state.dropCount}`, layout.header.x, layout.header.y + 100, 12, '#8fa1ba');
  drawText(`IAA 资格占位 ${placeholdersEnabled ? 'ON（仅本地事件）' : 'OFF'}`, layout.header.x, layout.header.y + 123, 11, placeholdersEnabled ? '#f0b84b' : '#708096');

  if (state.status === 'blocked') {
    drawText(`首次阻挡：L${state.lastDrop.blockerIndex + 1}`, layout.width / 2, layout.board.y + layout.board.height + 47, 15, '#ff7b83', 'center', 800);
  } else if (state.status === 'passed') {
    drawText('通路贯通', layout.width / 2, layout.board.y + layout.board.height + 47, 16, '#8cf29a', 'center', 800);
  } else {
    drawText('↔  拖层吸附   ·   ●  放珠验证', layout.width / 2, layout.board.y + layout.board.height + 47, 13, '#aebacd', 'center', 650);
  }

  const rects = controlRects();
  drawButton(rects.reset, '重置');
  if (rects.drop) drawButton(rects.drop, '放珠', true);
  if (rects.retry) drawButton(rects.retry, '免费重试 · 保留布局', true);
  if (rects.next) drawButton(rects.next, '下一基础 seed', true);
  if (rects.details) drawButton(rects.details, detailWasChosen ? '详情已查看' : '失败详情');
  if (rects.hint) drawButton(rects.hint, '分层提示资格');
}

function render() {
  context.clearRect(0, 0, layout.width, layout.height);
  context.fillStyle = '#10151f';
  context.fillRect(0, 0, layout.width, layout.height);
  const gradient = context.createLinearGradient(0, 0, 0, layout.height);
  gradient.addColorStop(0, '#182131');
  gradient.addColorStop(1, '#0d121a');
  context.fillStyle = gradient;
  context.fillRect(0, 0, layout.width, layout.height);
  drawBoard();
  drawHud();
  requestAnimationFrame(render);
}

function canvasToClient(point) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: rect.left + point.x * (rect.width / layout.width),
    y: rect.top + point.y * (rect.height / layout.height),
  };
}

window.stackedApertureDebug = {
  getState() {
    return clone({
      ...state,
      seed: level.seed,
      initialOffsets: level.initialOffsets,
      solutionOffsets: level.layers.map((layer) => layer.solutionSlot),
      placeholdersEnabled,
      rewardEligibilityCount: eligibilitySession.rewardEligibilityCount,
      safeLayout: {
        viewport: `${layout.width}x${layout.height}`,
        boardWithinHorizontalSafeArea: layout.board.x >= 16
          && layout.board.x + layout.board.width <= layout.width - 16,
        controlsWithinBottomSafeArea: layout.controls.y + layout.controls.height <= layout.height - layout.safeBottom,
        minimumLayerHitHeight: Math.min(...layout.layers.map((entry) => entry.hitHeight)),
      },
    });
  },
  getEvents() {
    return clone(events);
  },
  getSolution() {
    return clone(solveLevel(level, state.offsets));
  },
  getLayerPoint(layerIndex) {
    const sheet = layout.layers[layerIndex];
    return canvasToClient({ x: layout.width / 2, y: sheet.centerY });
  },
  getControlPoint(name) {
    const rect = controlRects()[name];
    if (!rect) throw new Error(`control ${name} is not available`);
    return canvasToClient({ x: rect.x + rect.width / 2, y: rect.y + rect.height / 2 });
  },
};

logEvent('level_start', { seed: level.seed });
requestAnimationFrame(render);
})();
