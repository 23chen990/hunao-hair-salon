export const TAPE_COLUMNS = 12;
export const TAPE_ROWS = 8;
export const ROLLER_FACE_BITS = Object.freeze([
  0b00011000,
  0b00011000,
  0b00100100,
  0b00100100,
  0b01000010,
  0b01000010,
  0b10000001,
  0b10000001,
]);

const clamp = (value, low, high) => Math.max(low, Math.min(high, value));
const mod = (value, divisor) => ((value % divisor) + divisor) % divisor;

function normalizeBits(bits) {
  return Number(bits) & 0xff;
}

function normalizeTarget(target) {
  if (!Array.isArray(target) || target.length !== TAPE_COLUMNS) {
    throw new RangeError(`target must contain ${TAPE_COLUMNS} columns`);
  }
  return target.map(normalizeBits);
}

function normalizeAction(action) {
  if (!action || ![-1, 1].includes(action.direction)) {
    throw new RangeError('direction must be -1 or 1');
  }
  return {
    direction: action.direction,
    distance: clamp(Math.round(Number(action.distance) || 0), 1, TAPE_COLUMNS),
    face: mod(Math.round(Number(action.face) || 0), ROLLER_FACE_BITS.length),
  };
}

export function faceBits(face) {
  return ROLLER_FACE_BITS[mod(face, ROLLER_FACE_BITS.length)];
}

export function actionFromPointerSamples(samples, cellPx, face) {
  if (!Array.isArray(samples) || samples.length < 2 || !(cellPx > 0)) {
    throw new RangeError('at least two samples and a positive cell size are required');
  }
  const startX = Number(samples[0].x);
  const endX = Number(samples.at(-1).x);
  const delta = endX - startX;
  return normalizeAction({
    direction: delta >= 0 ? 1 : -1,
    distance: Math.round(Math.abs(delta) / cellPx),
    face,
  });
}

export function traceRoll(action) {
  const normalized = normalizeAction(action);
  return Array.from({ length: normalized.distance }, (_, step) => ({
    column: normalized.direction === 1 ? step : TAPE_COLUMNS - 1 - step,
    face: mod(normalized.face + normalized.direction * step, ROLLER_FACE_BITS.length),
  }));
}

export function rollMask(action) {
  const mask = Array(TAPE_COLUMNS).fill(0);
  for (const contact of traceRoll(action)) {
    mask[contact.column] |= faceBits(contact.face);
  }
  return mask;
}

function bitCount(byte) {
  let value = byte & 0xff;
  let count = 0;
  while (value) {
    value &= value - 1;
    count += 1;
  }
  return count;
}

export function compareBoards(ink, target) {
  const normalizedInk = normalizeTarget(ink);
  const normalizedTarget = normalizeTarget(target);
  let missing = 0;
  let wrong = 0;
  for (let column = 0; column < TAPE_COLUMNS; column += 1) {
    missing += bitCount(normalizedTarget[column] & ~normalizedInk[column]);
    wrong += bitCount(normalizedInk[column] & ~normalizedTarget[column]);
  }
  return { missing, wrong, exact: missing === 0 && wrong === 0 };
}

export function createGame({ seed, target }) {
  const normalizedTarget = normalizeTarget(target);
  return {
    version: 1,
    seed: Number(seed) >>> 0,
    target: normalizedTarget,
    ink: Array(TAPE_COLUMNS).fill(0),
    moves: 0,
    clockMs: 0,
    paused: false,
    history: [],
  };
}

function playableSnapshot(state) {
  return {
    ink: [...state.ink],
    moves: state.moves,
    clockMs: state.clockMs,
    paused: state.paused,
  };
}

export function applyRoll(state, action) {
  if (state.paused) return state;
  const stamp = rollMask(action);
  const ink = state.ink.map((bits, column) => normalizeBits(bits | stamp[column]));
  return {
    ...state,
    ink,
    moves: state.moves + 1,
    history: [...state.history, playableSnapshot(state)],
  };
}

export function undoRoll(state) {
  if (state.paused || state.history.length === 0) return state;
  const restored = state.history.at(-1);
  return {
    ...state,
    ...restored,
    ink: [...restored.ink],
    history: state.history.slice(0, -1),
  };
}

export function restartGame(state) {
  return createGame({ seed: state.seed, target: state.target });
}

function hashText(text) {
  let hash = 0xcbf29ce484222325n;
  for (let index = 0; index < text.length; index += 1) {
    hash ^= BigInt(text.charCodeAt(index));
    hash = BigInt.asUintN(64, hash * 0x100000001b3n);
  }
  return hash.toString(16).padStart(16, '0');
}

export function stateHash(state) {
  return hashText(JSON.stringify({
    seed: state.seed,
    target: state.target,
    ink: state.ink,
    moves: state.moves,
    clockMs: state.clockMs,
    paused: state.paused,
  }));
}

export function serializeState(state) {
  return JSON.stringify(state);
}

export function deserializeState(serialized) {
  const parsed = JSON.parse(serialized);
  const restored = createGame({ seed: parsed.seed, target: parsed.target });
  return {
    ...restored,
    ink: normalizeTarget(parsed.ink),
    moves: Number(parsed.moves) || 0,
    clockMs: Number(parsed.clockMs) || 0,
    paused: Boolean(parsed.paused),
    history: Array.isArray(parsed.history)
      ? parsed.history.map((snapshot) => ({
        ink: normalizeTarget(snapshot.ink),
        moves: Number(snapshot.moves) || 0,
        clockMs: Number(snapshot.clockMs) || 0,
        paused: Boolean(snapshot.paused),
      }))
      : [],
  };
}

function applyEvent(state, event) {
  switch (event.type) {
    case 'tick':
      return state.paused
        ? state
        : { ...state, clockMs: state.clockMs + Math.max(0, Number(event.ms) || 0) };
    case 'roll':
      return applyRoll(state, event.action);
    case 'undo':
      return undoRoll(state);
    case 'restart':
      return restartGame(state);
    default:
      throw new RangeError(`unknown event type: ${event.type}`);
  }
}

export function replayEvents(initialState, events) {
  let state = deserializeState(serializeState(initialState));
  const hashes = [];
  for (const event of events) {
    state = applyEvent(state, event);
    hashes.push(stateHash(state));
  }
  return { state, hashes, finalHash: stateHash(state) };
}
