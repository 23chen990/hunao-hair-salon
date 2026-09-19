import { LocalEventLog } from './events.js';
import { ACCEPTANCE_SEEDS, TUTORIAL_LEVELS } from './levels.js';
import { computeLayout } from './layout.js';
import { TutorialController } from './onboarding.js';
import { canvasPointToModel, modelPointToCanvas, renderGame } from './render.js';
                                              
import { DEFAULT_RULES } from './rules.js';
import { PhaseRippleSession } from './session.js';
import { actionAtPoint, resultActions } from './ui-actions.js';
                                                        

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
const tutorialSession = new PhaseRippleSession(TUTORIAL_LEVELS, eventLog);
const tutorial = new TutorialController(TUTORIAL_LEVELS.length);
let logicalWidth = 390;
let logicalHeight = 844;
let lastFrame = performance.now();
let wasHidden = false;
let hintActive = false;

function tutorialActive()          {
  return tutorial.phase !== 'complete';
}

function activeSession()                     {
  return tutorialActive() ? tutorialSession : session;
}

function advanceTutorialLesson()       {
  const transition = tutorial.advanceLesson();
  if (transition === 'next') tutorialSession.nextLevel();
}

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
  const pointer = pointerPosition(event);
  if (tutorial.continueAfterImpact()) return;

  const currentSession = activeSession();
  const state = currentSession.state;
  if (tutorialActive() && tutorial.phase === 'result') {
    const action = actionAtPoint(
      resultActions(logicalWidth, logicalHeight, state.outcome),
      pointer,
    );
    if (action === 'next') advanceTutorialLesson();
    return;
  }
  if (state.outcome !== 'playing') {
    const action = actionAtPoint(
      resultActions(logicalWidth, logicalHeight, state.outcome),
      pointer,
    );
    if (action === 'next') {
      hintActive = false;
      currentSession.nextLevel();
    } else if (action === 'retry') {
      hintActive = false;
      currentSession.restart('tap');
    } else if (action === 'hint') {
      hintActive = true;
      eventLog.record('hint_requested', { seed: currentSession.level.seed, attempt: currentSession.progress.attempts });
      currentSession.restart('tap');
    }
    return;
  }
  if (state.click !== null) return;

  const layout = computeLayout(logicalWidth, logicalHeight);
  const model = canvasPointToModel(layout, pointer.x, pointer.y);
  if (Math.hypot(model.x, model.y) > DEFAULT_RULES.arenaRadius) return;

  if (tutorialActive() && tutorial.isWaitingForTarget(state.time, currentSession.level.referenceInput.time)) {
    if (!tutorial.completeTargetTap(model, currentSession.level.referenceInput)) return;
    currentSession.click(currentSession.level.referenceInput);
    return;
  }
  currentSession.click(model);
});

canvas.addEventListener('keydown', (event) => {
  if (event.key !== 'Enter' && event.key !== ' ') return;
  event.preventDefault();
  if (tutorial.continueAfterImpact()) return;
  const currentSession = activeSession();
  const state = currentSession.state;
  if (tutorialActive() && tutorial.phase === 'result') {
    advanceTutorialLesson();
    return;
  }
  if (state.outcome === 'success') {
    hintActive = false;
    currentSession.nextLevel();
    return;
  }
  if (state.outcome === 'boundary' || state.outcome === 'timeout') {
    hintActive = false;
    currentSession.restart('tap');
    return;
  }
  if (tutorialActive() && tutorial.isWaitingForTarget(state.time, currentSession.level.referenceInput.time)) {
    tutorial.completeTargetTap(currentSession.level.referenceInput, currentSession.level.referenceInput);
    currentSession.click(currentSession.level.referenceInput);
  }
});

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    wasHidden = true;
    return;
  }
  if (wasHidden) {
    activeSession().recoverFromBackground();
    wasHidden = false;
    lastFrame = performance.now();
  }
});

window.addEventListener('resize', resizeCanvas);

function frame(now        )       {
  resizeCanvas();
  const currentSession = activeSession();
  const requestedSeconds = (now - lastFrame) / 1000;
  const allowedSeconds = tutorialActive()
    ? tutorial.tickAllowance(
      currentSession.state.time,
      requestedSeconds,
      currentSession.level.referenceInput.time,
    )
    : requestedSeconds;
  currentSession.tick(allowedSeconds);
  if (tutorialActive()) {
    tutorial.observeReversals(currentSession.state.triggerOrder.length);
    tutorial.observeOutcome(currentSession.state.outcome);
  }
  lastFrame = now;
  renderGame(context, currentSession.state, logicalWidth, logicalHeight, assets, {
    progress: session.progress,
    tutorial: {
      phase: tutorial.phase,
      waitingForTarget: tutorialActive() && tutorial.isWaitingForTarget(
        currentSession.state.time,
        currentSession.level.referenceInput.time,
      ),
      lesson: tutorial.lesson,
      lessonTotal: tutorial.lessonTotal,
      impactIndex: tutorial.impactIndex,
    },
    hintActive,
    animationTime: now / 1000,
  });
  requestAnimationFrame(frame);
}

resizeCanvas();
renderGame(context, tutorialSession.state, logicalWidth, logicalHeight, assets, {
  progress: session.progress,
  tutorial: {
    phase: tutorial.phase,
    waitingForTarget: false,
    lesson: tutorial.lesson,
    lessonTotal: tutorial.lessonTotal,
    impactIndex: tutorial.impactIndex,
  },
  hintActive,
  animationTime: performance.now() / 1000,
});
loading.hidden = true;
canvas.classList.add('is-ready');
requestAnimationFrame(frame);

Object.assign(window, {
  phaseRippleDebug: {
    getState: () => activeSession().state,
    getEvents: () => eventLog.read(),
    getReferenceCanvasPoint: () => modelPointToCanvas(
      computeLayout(logicalWidth, logicalHeight),
      activeSession().level.referenceInput,
    ),
    getCanvasPoint: (point                                    ) => modelPointToCanvas(
      computeLayout(logicalWidth, logicalHeight),
      point,
    ),
    restart: () => activeSession().restart('manual'),
    getResultActionPoint: (action                  ) => {
      const entry = resultActions(logicalWidth, logicalHeight, activeSession().state.outcome)
        .find((candidate) => candidate.action === action);
      return entry === undefined ? null : {
        x: entry.left + entry.width / 2,
        y: entry.top + entry.height / 2,
      };
    },
    completeTutorial: () => {
      let guard = 20;
      while (tutorial.phase !== 'complete' && guard > 0) {
        if (tutorial.phase === 'guided') {
          tutorial.completeTargetTap(tutorialSession.level.referenceInput, tutorialSession.level.referenceInput);
        } else if (tutorial.phase === 'ripple') {
          if (!tutorial.observeReversals(tutorialSession.level.pieces.length)) {
            tutorial.observeOutcome('success');
          }
        } else if (tutorial.phase === 'impact') {
          tutorial.continueAfterImpact();
        } else if (tutorial.phase === 'result') {
          advanceTutorialLesson();
        }
        guard -= 1;
      }
    },
  },
});
