import { LocalEventLog } from './events.js';
import { ACCEPTANCE_SEEDS } from './levels.js';
import { computeLayout } from './layout.js';
import { canvasPointToModel, modelPointToCanvas, renderGame } from './render.js';
                                              
import { DEFAULT_RULES } from './rules.js';
import { PhaseRippleSession } from './session.js';

const canvas = document.querySelector                   ('#game');
const loading = document.querySelector             ('#loading');
if (canvas === null) throw new Error('Missing #game canvas');
if (loading === null) throw new Error('Missing #loading indicator');
const context = canvas.getContext('2d');
if (context === null) throw new Error('Canvas 2D is unavailable');

function loadImage(source        )                            {
  return new Promise((resolve, reject) => {
    const image = new Image();
    image.decoding = 'async';
    image.addEventListener('load', () => resolve(image), { once: true });
    image.addEventListener('error', () => reject(new Error(`Failed to load ${source}`)), { once: true });
    image.src = source;
  });
}

const assets             = {
  board: await loadImage('./assets/tabletop-board.png'),
  outboundPiece: await loadImage('./assets/piece-outbound.png'),
  returnedPiece: await loadImage('./assets/piece-returned.png'),
};

const eventLog = new LocalEventLog(window.localStorage, 'phase-ripple-skinned-events-v1');
const session = new PhaseRippleSession(ACCEPTANCE_SEEDS, eventLog);
let logicalWidth = 390;
let logicalHeight = 844;
let lastFrame = performance.now();
let wasHidden = false;

function resizeCanvas()       {
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
  context.imageSmoothingQuality = 'high';
}

function pointerPosition(event              )                                     {
  const bounds = canvas.getBoundingClientRect();
  return {
    x: ((event.clientX - bounds.left) / bounds.width) * logicalWidth,
    y: ((event.clientY - bounds.top) / bounds.height) * logicalHeight,
  };
}

canvas.addEventListener('pointerdown', (event) => {
  event.preventDefault();
  canvas.focus({ preventScroll: true });
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
  const model = canvasPointToModel(layout, pointer.x, pointer.y);
  if (Math.hypot(model.x, model.y) > DEFAULT_RULES.arenaRadius) return;
  session.click(model);
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

function frame(now        )       {
  resizeCanvas();
  session.tick((now - lastFrame) / 1000);
  lastFrame = now;
  renderGame(context, session.state, logicalWidth, logicalHeight, assets);
  requestAnimationFrame(frame);
}

resizeCanvas();
renderGame(context, session.state, logicalWidth, logicalHeight, assets);
loading.hidden = true;
canvas.classList.add('is-ready');
requestAnimationFrame(frame);

Object.assign(window, {
  phaseRippleDebug: {
    getState: () => session.state,
    getEvents: () => eventLog.read(),
    getReferenceCanvasPoint: () => modelPointToCanvas(
      computeLayout(logicalWidth, logicalHeight),
      session.level.referenceInput,
    ),
    getCanvasPoint: (point                                    ) => modelPointToCanvas(
      computeLayout(logicalWidth, logicalHeight),
      point,
    ),
    restart: () => session.restart('manual'),
  },
});
