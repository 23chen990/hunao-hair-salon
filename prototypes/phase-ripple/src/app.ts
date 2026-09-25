import { LocalEventLog } from './events.ts';
import { ACCEPTANCE_SEEDS } from './levels.ts';
import { computeLayout } from './layout.ts';
import { canvasPointToModel, renderGame } from './render.ts';
import { PhaseRippleSession } from './session.ts';

const canvas = document.querySelector<HTMLCanvasElement>('#game');
if (canvas === null) throw new Error('Missing #game canvas');
const context = canvas.getContext('2d');
if (context === null) throw new Error('Canvas 2D is unavailable');

const eventLog = new LocalEventLog(window.localStorage);
const session = new PhaseRippleSession(ACCEPTANCE_SEEDS, eventLog);
let logicalWidth = 390;
let logicalHeight = 844;
let lastFrame = performance.now();
let wasHidden = false;

function resizeCanvas(): void {
  const bounds = canvas.getBoundingClientRect();
  const pixelRatio = Math.min(window.devicePixelRatio || 1, 2);
  logicalWidth = Math.max(1, Math.round(bounds.width));
  logicalHeight = Math.max(1, Math.round(bounds.height));
  const pixelWidth = Math.round(logicalWidth * pixelRatio);
  const pixelHeight = Math.round(logicalHeight * pixelRatio);
  if (canvas.width !== pixelWidth || canvas.height !== pixelHeight) {
    canvas.width = pixelWidth;
    canvas.height = pixelHeight;
  }
  context.setTransform(pixelRatio, 0, 0, pixelRatio, 0, 0);
  context.imageSmoothingEnabled = true;
}

function pointerPosition(event: PointerEvent): Readonly<{ x: number; y: number }> {
  const bounds = canvas.getBoundingClientRect();
  return {
    x: ((event.clientX - bounds.left) / bounds.width) * logicalWidth,
    y: ((event.clientY - bounds.top) / bounds.height) * logicalHeight,
  };
}

canvas.addEventListener('pointerdown', (event) => {
  event.preventDefault();
  const state = session.state;
  if (state.outcome === 'success') {
    session.nextLevel();
    return;
  }
  if (state.outcome === 'boundary' || state.outcome === 'timeout') {
    session.restart('tap');
    return;
  }
  if (state.click !== null) return;

  const pointer = pointerPosition(event);
  const layout = computeLayout(logicalWidth, logicalHeight);
  const insideArena =
    pointer.x >= layout.arena.left && pointer.x <= layout.arena.right &&
    pointer.y >= layout.arena.top && pointer.y <= layout.arena.bottom;
  if (!insideArena) return;
  session.click(canvasPointToModel(layout, pointer.x, pointer.y));
});

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    wasHidden = true;
    return;
  }
  if (wasHidden) {
    session.recoverFromBackground();
    wasHidden = false;
    lastFrame = performance.now();
  }
});

window.addEventListener('resize', resizeCanvas);

function frame(now: number): void {
  resizeCanvas();
  session.tick((now - lastFrame) / 1000);
  lastFrame = now;
  renderGame(context, session.state, logicalWidth, logicalHeight);
  requestAnimationFrame(frame);
}

resizeCanvas();
renderGame(context, session.state, logicalWidth, logicalHeight);
requestAnimationFrame(frame);

Object.assign(window, {
  phaseRippleDebug: {
    getState: () => session.state,
    getEvents: () => eventLog.read(),
    restart: () => session.restart('manual'),
  },
});
