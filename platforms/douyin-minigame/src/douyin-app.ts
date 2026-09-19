import { LocalEventLog } from '../../../prototypes/phase-ripple-skinned/src/events.ts';
import { ACCEPTANCE_SEEDS, TUTORIAL_LEVELS } from '../../../prototypes/phase-ripple-skinned/src/levels.ts';
import { computeLayout } from '../../../prototypes/phase-ripple-skinned/src/layout.ts';
import { TutorialController } from '../../../prototypes/phase-ripple-skinned/src/onboarding.ts';
import { canvasPointToModel, renderGame } from '../../../prototypes/phase-ripple-skinned/src/render.ts';
import { DEFAULT_RULES } from '../../../prototypes/phase-ripple-skinned/src/rules.ts';
import { PhaseRippleSession } from '../../../prototypes/phase-ripple-skinned/src/session.ts';
import { actionAtPoint, resultActions } from '../../../prototypes/phase-ripple-skinned/src/ui-actions.ts';
import { launchedFromSidebar } from './launch.ts';
import {
  checkSidebarSupport,
  configureCanvas,
  createPlatformStorage,
  installRoundRect,
  loadPlatformImage,
  navigateToSidebar,
  normalizeTouchPoint,
  readViewport,
  scheduleFrame,
} from './platform.ts';

declare const tt: Readonly<Record<string, any>>;

const EVENT_KEY = 'phase-ripple-douyin-events-v1';
const SIDEBAR_CLAIM_KEY = 'phase-ripple-sidebar-claim-date-v1';

function localDateKey(now = new Date()): string {
  const month = String(now.getMonth() + 1).padStart(2, '0');
  const day = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${month}-${day}`;
}

function sidebarButton(width: number): Readonly<{ left: number; top: number; width: number; height: number }> {
  return { left: width - 87, top: 70, width: 74, height: 34 };
}

function inside(
  point: Readonly<{ x: number; y: number }>,
  rectangle: Readonly<{ left: number; top: number; width: number; height: number }>,
): boolean {
  return point.x >= rectangle.left && point.x <= rectangle.left + rectangle.width &&
    point.y >= rectangle.top && point.y <= rectangle.top + rectangle.height;
}

function drawSidebarButton(
  context: Readonly<Record<string, any>>,
  width: number,
  claimReady: boolean,
  claimedToday: boolean,
): void {
  const button = sidebarButton(width);
  context.beginPath();
  context.roundRect(button.left, button.top, button.width, button.height, 17);
  context.fillStyle = claimReady ? '#d6a84e' : '#fff7e7';
  context.fill();
  context.strokeStyle = '#078e91';
  context.lineWidth = 2;
  context.stroke();
  context.fillStyle = '#174f4c';
  context.font = '700 12px sans-serif';
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  const label = claimReady ? '领取提示' : (claimedToday ? '明日再来' : '侧边栏礼');
  context.fillText(label, button.left + button.width / 2, button.top + button.height / 2);
}

export async function boot(ttApi: Readonly<Record<string, any>> = tt): Promise<void> {
  if (ttApi === undefined || typeof ttApi.createCanvas !== 'function') {
    throw new Error('Douyin mini-game runtime is unavailable');
  }
  const canvas = ttApi.createCanvas();
  const context = canvas.getContext('2d');
  if (context === null) throw new Error('Canvas 2D is unavailable');
  installRoundRect(context);
  const viewport = readViewport(ttApi);
  configureCanvas(canvas, context, viewport);

  context.fillStyle = '#fff7e7';
  context.fillRect(0, 0, viewport.width, viewport.height);
  context.fillStyle = '#174f4c';
  context.font = '700 20px sans-serif';
  context.textAlign = 'center';
  context.fillText('相位涟漪 · 加载中', viewport.width / 2, viewport.height / 2);

  const assets = {
    board: await loadPlatformImage(canvas, ttApi, 'assets/tabletop-board.png'),
    outboundPiece: await loadPlatformImage(canvas, ttApi, 'assets/piece-outbound.png'),
    returnedPiece: await loadPlatformImage(canvas, ttApi, 'assets/piece-returned.png'),
  };

  const storage = createPlatformStorage(ttApi);
  const eventLog = new LocalEventLog(storage, EVENT_KEY);
  const session = new PhaseRippleSession(ACCEPTANCE_SEEDS, eventLog);
  const tutorialSession = new PhaseRippleSession(TUTORIAL_LEVELS, eventLog);
  const tutorial = new TutorialController(TUTORIAL_LEVELS.length);
  let lastFrame = Date.now();
  let wasHidden = false;
  let hintActive = false;
  let sidebarSupported = false;

  void checkSidebarSupport(ttApi).then((supported) => { sidebarSupported = supported; });

  function tutorialActive(): boolean {
    return tutorial.phase !== 'complete';
  }

  function activeSession(): PhaseRippleSession {
    return tutorialActive() ? tutorialSession : session;
  }

  function advanceTutorialLesson(): void {
    const transition = tutorial.advanceLesson();
    if (transition === 'next') tutorialSession.nextLevel();
  }

  function claimedToday(): boolean {
    return storage.getItem(SIDEBAR_CLAIM_KEY) === localDateKey();
  }

  function claimSidebarHint(): void {
    storage.setItem(SIDEBAR_CLAIM_KEY, localDateKey());
    hintActive = true;
    const current = activeSession();
    if (!tutorialActive() && (current.state.click !== null || current.state.outcome !== 'playing')) {
      current.restart('tap');
    }
    eventLog.record('hint_requested', {
      seed: current.level.seed,
      attempt: current.progress.attempts,
      source: 'sidebar_return',
    });
  }

  function handleTouch(event: Readonly<Record<string, any>>): void {
    const point = normalizeTouchPoint(event);
    if (point === null) return;

    if (sidebarSupported && inside(point, sidebarButton(viewport.width))) {
      if (launchedFromSidebar() && !claimedToday()) claimSidebarHint();
      else if (!claimedToday()) navigateToSidebar(ttApi);
      return;
    }

    if (tutorial.continueAfterImpact()) return;
    const current = activeSession();
    const state = current.state;
    if (tutorialActive() && tutorial.phase === 'result') {
      const action = actionAtPoint(resultActions(viewport.width, viewport.height, state.outcome), point);
      if (action === 'next') advanceTutorialLesson();
      return;
    }
    if (state.outcome !== 'playing') {
      const action = actionAtPoint(resultActions(viewport.width, viewport.height, state.outcome), point);
      if (action === 'next') {
        hintActive = false;
        current.nextLevel();
      } else if (action === 'retry') {
        hintActive = false;
        current.restart('tap');
      } else if (action === 'hint') {
        hintActive = true;
        eventLog.record('hint_requested', { seed: current.level.seed, attempt: current.progress.attempts });
        current.restart('tap');
      }
      return;
    }
    if (state.click !== null) return;

    const layout = computeLayout(viewport.width, viewport.height);
    const model = canvasPointToModel(layout, point.x, point.y);
    if (Math.hypot(model.x, model.y) > DEFAULT_RULES.arenaRadius) return;
    if (tutorialActive() && tutorial.isWaitingForTarget(state.time, current.level.referenceInput.time)) {
      if (!tutorial.completeTargetTap(model, current.level.referenceInput)) return;
      current.click(current.level.referenceInput);
      return;
    }
    current.click(model);
  }

  if (typeof ttApi.onTouchStart === 'function') ttApi.onTouchStart(handleTouch);
  if (typeof ttApi.onHide === 'function') ttApi.onHide(() => { wasHidden = true; });
  if (typeof ttApi.onShow === 'function') {
    ttApi.onShow(() => {
      if (!wasHidden) return;
      activeSession().recoverFromBackground();
      wasHidden = false;
      lastFrame = Date.now();
    });
  }

  function frame(timestamp: number): void {
    const now = Number.isFinite(timestamp) ? timestamp : Date.now();
    const requestedSeconds = Math.max(0, (now - lastFrame) / 1000);
    const current = activeSession();
    const allowedSeconds = tutorialActive()
      ? tutorial.tickAllowance(current.state.time, requestedSeconds, current.level.referenceInput.time)
      : requestedSeconds;
    current.tick(allowedSeconds);
    if (tutorialActive()) {
      tutorial.observeReversals(current.state.triggerOrder.length);
      tutorial.observeOutcome(current.state.outcome);
    }
    lastFrame = now;
    renderGame(context, current.state, viewport.width, viewport.height, assets, {
      progress: session.progress,
      tutorial: {
        phase: tutorial.phase,
        waitingForTarget: tutorialActive() && tutorial.isWaitingForTarget(
          current.state.time,
          current.level.referenceInput.time,
        ),
        lesson: tutorial.lesson,
        lessonTotal: tutorial.lessonTotal,
        impactIndex: tutorial.impactIndex,
      },
      hintActive,
      animationTime: now / 1000,
    });
    if (sidebarSupported) {
      drawSidebarButton(context, viewport.width, launchedFromSidebar() && !claimedToday(), claimedToday());
    }
    scheduleFrame(canvas, frame);
  }

  frame(Date.now());
}

void boot().catch((error) => {
  console.error('Phase Ripple failed to start', error);
  if (typeof tt !== 'undefined' && typeof tt.showToast === 'function') {
    tt.showToast({ title: '启动失败，请重试', icon: 'none' });
  }
});
