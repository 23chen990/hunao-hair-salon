import {
  attemptDrop,
  createGameState,
  hashGameState,
  moveGameState,
  retryLevel,
} from './model.js';

function applyAction(level, state, action) {
  if (action.type === 'move') {
    return moveGameState(level, state, { layer: action.layer, delta: action.delta });
  }
  if (action.type === 'drop') return attemptDrop(level, state);
  if (action.type === 'retry') return retryLevel(state);
  throw new TypeError(`unsupported replay action: ${action.type}`);
}

function payloadFor(action) {
  return action.type === 'move' ? { layer: action.layer, delta: action.delta } : {};
}

export function recordRun(level, actions) {
  let state = createGameState(level);
  let clockMs = 0;
  const events = [];
  for (let index = 0; index < actions.length; index += 1) {
    const action = actions[index];
    clockMs += action.advanceMs ?? 0;
    state = applyAction(level, state, action);
    events.push({
      seq: index + 1,
      clockMs,
      type: action.type,
      payload: payloadFor(action),
      stateHash: hashGameState(state),
    });
  }
  return { events, state, hash: hashGameState(state) };
}

export function replayRun(level, events) {
  let state = createGameState(level);
  let priorClock = 0;
  for (let index = 0; index < events.length; index += 1) {
    const event = events[index];
    if (event.seq !== index + 1) throw new Error(`event sequence mismatch at ${index + 1}`);
    if (event.clockMs < priorClock) throw new Error(`event clock moved backward at ${event.seq}`);
    priorClock = event.clockMs;
    state = applyAction(level, state, { type: event.type, ...event.payload });
    const actualHash = hashGameState(state);
    if (actualHash !== event.stateHash) {
      throw new Error(`replay hash mismatch at event ${event.seq}: ${actualHash} != ${event.stateHash}`);
    }
  }
  return { state, hash: hashGameState(state) };
}
