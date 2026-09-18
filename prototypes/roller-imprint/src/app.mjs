import {
  actionFromPointerSamples,
  applyRoll,
  compareBoards,
  createGame,
  deserializeState,
  faceBits,
  replayEvents,
  restartGame,
  rollMask,
  serializeState,
  stateHash,
  undoRoll,
} from './model.mjs';
import { allLegalActions, generateLevel } from './solver.mjs';
import { beginAdSimulation, endAdSimulation } from './iaa.mjs';
import { computeLayout, hitTestRollStart } from './ui-contract.mjs';

const canvas = document.querySelector('#game');
const context = canvas.getContext('2d');
const faceLabel = document.querySelector('#face-label');
const seedLabel = document.querySelector('#seed-label');
const eventLogView = document.querySelector('#event-log');
const buttons = [...document.querySelectorAll('button')];

let seed = Math.max(1, Number(new URL(location.href).searchParams.get('seed')) || 1);
let level;
let state;
let initialSerialized;
let selectedFace = 0;
let layout;
let activeDrag = null;
let contactArcVisible = false;
let modelEvents = [];
let eventLog = [];

function loadSeed(nextSeed) {
  seed = Math.max(1, Number(nextSeed) >>> 0);
  level = generateLevel(seed);
  state = createGame({ seed, target: level.target });
  initialSerialized = serializeState(state);
  selectedFace = 0;
  activeDrag = null;
  contactArcVisible = false;
  modelEvents = [];
  eventLog = [{ type: 'seed', seed, shortest: level.shortest, at: 0 }];
  syncView();
}

function canvasPoint(event) {
  const rect = canvas.getBoundingClientRect();
  return {
    x: ((event.clientX - rect.left) / rect.width) * layout.viewportWidth,
    y: ((event.clientY - rect.top) / rect.height) * layout.canvasHeight,
  };
}

function record(entry) {
  eventLog.push({ ...entry, index: eventLog.length });
  eventLogView.textContent = JSON.stringify(eventLog.slice(-8), null, 2);
}

function boardBitTotal(board) {
  let total = 0;
  for (let byte of board) {
    while (byte) {
      byte &= byte - 1;
      total += 1;
    }
  }
  return total;
}

function drawGrid(board, geometry, mode) {
  const { x, y, cell } = geometry;
  context.save();
  context.lineWidth = 1;
  for (let column = 0; column < 12; column += 1) {
    for (let row = 0; row < 8; row += 1) {
      const px = x + column * cell;
      const py = y + row * cell;
      const bit = 1 << row;
      const active = Boolean(board[column] & bit);
      const target = Boolean(level.target[column] & bit);
      context.fillStyle = '#f7f8f4';
      context.fillRect(px, py, cell, cell);
      context.strokeStyle = '#b9c0c2';
      context.strokeRect(px + 0.5, py + 0.5, cell - 1, cell - 1);
      if (mode === 'target' && active) {
        context.fillStyle = '#343d42';
        context.fillRect(px + 4, py + 4, cell - 8, cell - 8);
      } else if (mode === 'ink') {
        if (active) {
          context.fillStyle = target ? '#283d43' : '#b94937';
          context.beginPath();
          context.arc(px + cell / 2, py + cell / 2, Math.max(3, cell * 0.29), 0, Math.PI * 2);
          context.fill();
        } else if (target) {
          context.strokeStyle = '#78858a';
          context.setLineDash([2, 3]);
          context.strokeRect(px + 5, py + 5, cell - 10, cell - 10);
          context.setLineDash([]);
        }
      }
    }
  }
  context.restore();
}

function drawRoller() {
  const left = layout.leftHandle;
  const right = layout.rightHandle;
  const bodyX = left.x + 28;
  const bodyWidth = right.x - left.x - 56;
  context.save();
  context.strokeStyle = state.paused ? '#8e9699' : '#38464c';
  context.fillStyle = state.paused ? '#c8cdcf' : '#d9deda';
  context.lineWidth = 2;
  context.fillRect(bodyX, layout.rollerY - 18, bodyWidth, 36);
  context.strokeRect(bodyX, layout.rollerY - 18, bodyWidth, 36);
  for (let face = 0; face < 8; face += 1) {
    const segmentX = bodyX + (bodyWidth * face) / 8;
    context.fillStyle = face === selectedFace ? '#5d7d84' : '#aeb7b8';
    context.fillRect(segmentX + 2, layout.rollerY - 14, bodyWidth / 8 - 4, 28);
  }
  for (const [handle, arrow] of [[left, '→'], [right, '←']]) {
    context.beginPath();
    context.arc(handle.x, handle.y, handle.radius, 0, Math.PI * 2);
    context.fillStyle = state.paused ? '#aeb5b7' : '#fafbf8';
    context.fill();
    context.strokeStyle = '#3e4a50';
    context.stroke();
    context.fillStyle = '#263137';
    context.font = '700 18px system-ui, sans-serif';
    context.textAlign = 'center';
    context.textBaseline = 'middle';
    context.fillText(arrow, handle.x, handle.y - 1);
  }
  if (contactArcVisible) {
    context.beginPath();
    context.arc((left.x + right.x) / 2, layout.rollerY, 30, 0.15 * Math.PI, 0.85 * Math.PI);
    context.strokeStyle = '#277d70';
    context.lineWidth = 5;
    context.stroke();
  }
  context.restore();
}

function drawPreview() {
  if (!activeDrag || activeDrag.samples.length < 2) return;
  const delta = activeDrag.samples.at(-1).x - activeDrag.samples[0].x;
  if (Math.abs(delta) < layout.targetBoard.cell / 2) return;
  const action = actionFromPointerSamples(activeDrag.samples, layout.targetBoard.cell, selectedFace);
  if (action.direction !== activeDrag.direction) return;
  const mask = rollMask(action);
  const board = layout.inkBoard;
  context.save();
  context.globalAlpha = 0.35;
  context.fillStyle = '#277d70';
  for (let column = 0; column < 12; column += 1) {
    for (let row = 0; row < 8; row += 1) {
      if (mask[column] & (1 << row)) {
        context.fillRect(
          board.x + column * board.cell + 6,
          board.y + row * board.cell + 6,
          board.cell - 12,
          board.cell - 12,
        );
      }
    }
  }
  context.restore();
}

function render() {
  const cssWidth = canvas.clientWidth || 390;
  layout = computeLayout(cssWidth, innerHeight);
  const dpr = Math.min(2, devicePixelRatio || 1);
  canvas.style.height = `${layout.canvasHeight}px`;
  if (canvas.width !== Math.round(cssWidth * dpr) || canvas.height !== Math.round(layout.canvasHeight * dpr)) {
    canvas.width = Math.round(cssWidth * dpr);
    canvas.height = Math.round(layout.canvasHeight * dpr);
  }
  context.setTransform(dpr, 0, 0, dpr, 0, 0);
  context.clearRect(0, 0, cssWidth, layout.canvasHeight);
  context.fillStyle = '#eceeea';
  context.fillRect(0, 0, cssWidth, layout.canvasHeight);
  context.fillStyle = '#4b575d';
  context.font = '650 12px system-ui, sans-serif';
  context.textAlign = 'left';
  context.fillText('目标（凸纹要覆盖的格）', layout.targetBoard.x, 18);
  drawGrid(level.target, layout.targetBoard, 'target');
  drawRoller();
  context.fillText('当前落印｜实心=已印，红色=错印，虚线=漏印', layout.inkBoard.x, layout.inkBoard.y - 9);
  drawGrid(state.ink, layout.inkBoard, 'ink');
  drawPreview();
  const error = compareBoards(state.ink, state.target);
  context.textAlign = 'right';
  context.fillStyle = error.exact ? '#18705e' : '#566168';
  context.fillText(
    error.exact ? `完成 · ${state.moves} 趟` : `漏 ${error.missing} · 错 ${error.wrong} · ${state.moves} 趟`,
    layout.inkBoard.right,
    layout.canvasHeight - 7,
  );
}

function syncView() {
  faceLabel.textContent = `起始面 ${selectedFace + 1} / 8 · ${faceBits(selectedFace).toString(2).padStart(8, '0')}`;
  seedLabel.textContent = `#${seed}`;
  buttons.forEach((button) => {
    button.setAttribute('aria-disabled', String(state.paused));
  });
  eventLogView.textContent = JSON.stringify(eventLog.slice(-8), null, 2);
  render();
}

canvas.addEventListener('pointerdown', (event) => {
  const point = canvasPoint(event);
  const direction = hitTestRollStart(layout, point.x, point.y);
  if (!direction || state.paused) return;
  activeDrag = {
    pointerId: event.pointerId,
    pointerType: event.pointerType || 'mouse',
    direction,
    samples: [{ x: point.x }],
  };
  canvas.setPointerCapture(event.pointerId);
  record({ type: 'pointerdown', pointerType: activeDrag.pointerType, direction });
  render();
});

canvas.addEventListener('pointermove', (event) => {
  if (!activeDrag || activeDrag.pointerId !== event.pointerId) return;
  const point = canvasPoint(event);
  activeDrag.samples.push({ x: point.x });
  render();
});

function finishDrag(event) {
  if (!activeDrag || activeDrag.pointerId !== event.pointerId) return;
  const point = canvasPoint(event);
  activeDrag.samples.push({ x: point.x });
  const drag = activeDrag;
  activeDrag = null;
  const delta = drag.samples.at(-1).x - drag.samples[0].x;
  if (Math.abs(delta) < layout.targetBoard.cell / 2 || state.paused) {
    render();
    return;
  }
  const action = actionFromPointerSamples(drag.samples, layout.targetBoard.cell, selectedFace);
  if (action.direction !== drag.direction) {
    render();
    return;
  }
  const before = compareBoards(state.ink, state.target);
  state = applyRoll(state, action);
  modelEvents.push({ type: 'roll', action });
  const after = compareBoards(state.ink, state.target);
  record({
    type: 'roll',
    pointerType: drag.pointerType,
    action,
    before,
    after,
    hash: stateHash(state),
  });
  syncView();
}

canvas.addEventListener('pointerup', finishDrag);
canvas.addEventListener('pointercancel', (event) => {
  if (activeDrag?.pointerId === event.pointerId) activeDrag = null;
  render();
});

document.querySelector('#face-prev').addEventListener('click', () => {
  if (state.paused) return;
  selectedFace = (selectedFace + 7) % 8;
  record({ type: 'face', face: selectedFace });
  syncView();
});

document.querySelector('#face-next').addEventListener('click', () => {
  if (state.paused) return;
  selectedFace = (selectedFace + 1) % 8;
  record({ type: 'face', face: selectedFace });
  syncView();
});

document.querySelector('#undo').addEventListener('click', () => {
  if (state.paused || state.history.length === 0) return;
  state = undoRoll(state);
  modelEvents.push({ type: 'undo' });
  record({ type: 'undo', hash: stateHash(state) });
  syncView();
});

document.querySelector('#restart').addEventListener('click', () => {
  if (state.paused) return;
  state = restartGame(state);
  modelEvents.push({ type: 'restart' });
  record({ type: 'restart', free: true, hash: stateHash(state) });
  syncView();
});

document.querySelector('#next-seed').addEventListener('click', () => {
  if (state.paused) return;
  loadSeed(seed + 1);
});

document.querySelector('#hint').addEventListener('click', () => {
  if (state.paused) return;
  contactArcVisible = true;
  record({ type: 'contact-arc', localEligibilityOnly: true });
  syncView();
});

document.querySelector('#simulate-ad').addEventListener('click', () => {
  if (state.paused) return;
  const before = stateHash(state);
  state = beginAdSimulation(state);
  record({ type: 'ad-simulation-start', localOnly: true, hashBefore: before });
  syncView();
  setTimeout(() => {
    state = endAdSimulation(state);
    record({ type: 'ad-simulation-end', hashAfter: stateHash(state), unchanged: stateHash(state) === before });
    syncView();
  }, 300);
});

addEventListener('resize', render);

function findWrongAction() {
  return allLegalActions().find((action) => compareBoards(rollMask(action), level.target).wrong > 0);
}

window.__ROLLER_AUDIT__ = {
  ready: false,
  snapshot() {
    const replay = replayEvents(deserializeState(initialSerialized), modelEvents);
    return {
      seed,
      moves: state.moves,
      inkBits: boardBitTotal(state.ink),
      error: compareBoards(state.ink, state.target),
      stateHash: stateHash(state),
      selectedFace,
      paused: state.paused,
      contactArcVisible,
      pointerTypes: [...new Set(eventLog.map((entry) => entry.pointerType).filter(Boolean))],
      replayMatches: replay.finalHash === stateHash(state),
      eventCount: eventLog.length,
    };
  },
  regions() {
    const rect = canvas.getBoundingClientRect();
    const scaleX = rect.width / layout.viewportWidth;
    const scaleY = rect.height / layout.canvasHeight;
    return {
      leftHandle: {
        x: rect.left + layout.leftHandle.x * scaleX,
        y: rect.top + layout.leftHandle.y * scaleY,
      },
      rightHandle: {
        x: rect.left + layout.rightHandle.x * scaleX,
        y: rect.top + layout.rightHandle.y * scaleY,
      },
      cell: layout.targetBoard.cell * scaleX,
    };
  },
  controls() {
    return buttons.map((button) => {
      const rect = button.getBoundingClientRect();
      return { id: button.id, width: rect.width, height: rect.height };
    });
  },
  smokePlan() {
    return {
      wrong: { ...findWrongAction() },
      solution: level.solution.map((action) => ({ ...action })),
    };
  },
  events() {
    return JSON.parse(JSON.stringify(eventLog));
  },
};

loadSeed(seed);
window.__ROLLER_AUDIT__.ready = true;
