import {
  advanceFrame,
  createLevel,
  createRunner,
  hashState,
  pauseRunner,
  restart,
  resumeRunner,
  sampleBridgeSurface,
  setRunnerHeld,
} from './growing-bridge.mjs';

const canvas = document.querySelector('#bridge');
const context = canvas.getContext('2d', { alpha: false });
const restartButton = document.querySelector('#restart');
const nextButton = document.querySelector('#next-level');
const seedOutput = document.querySelector('#seed');
const archetypeOutput = document.querySelector('#archetype');
const stateOutput = document.querySelector('#state');
const clockOutput = document.querySelector('#clock');
const inputOutput = document.querySelector('#input-state');
const pressureFill = document.querySelector('#pressure-fill');
const query = new URLSearchParams(location.search);
let levelSeed = Math.max(1, Number.parseInt(query.get('level') ?? query.get('seed') ?? '1', 10) || 1);
let level = createLevel(levelSeed);
let runner = createRunner(level);
let previousFrame = performance.now();
let activePointer = null;

function updateLevelReadout() {
  seedOutput.textContent = `LEVEL ${level.levelNumber}`;
  archetypeOutput.textContent = level.archetype.toUpperCase();
  nextButton.disabled = runner.state.status !== 'success';
}

function resize() {
  const rect = canvas.getBoundingClientRect();
  const pixelRatio = Math.min(2, window.devicePixelRatio || 1);
  const width = Math.max(1, Math.round(rect.width * pixelRatio));
  const height = Math.max(1, Math.round(rect.height * pixelRatio));
  if (canvas.width !== width || canvas.height !== height) {
    canvas.width = width;
    canvas.height = height;
  }
}

function drawDashedLine(x, top, bottom, cameraX) {
  context.save();
  context.setLineDash([7, 8]);
  context.strokeStyle = '#63756f';
  context.lineWidth = 1.5;
  context.beginPath();
  context.moveTo(x - cameraX, top);
  context.lineTo(x - cameraX, bottom);
  context.stroke();
  context.restore();
}

function draw() {
  resize();
  const sx = canvas.width / level.width;
  const sy = canvas.height / level.height;
  const maxCamera = Math.max(0, level.worldLength - level.width);
  const cameraX = Math.max(0, Math.min(maxCamera, runner.state.ball.x - 112));
  context.setTransform(sx, 0, 0, sy, -cameraX * sx, 0);
  context.fillStyle = runner.state.held ? '#1a2a25' : '#182220';
  context.fillRect(cameraX, 0, level.width, level.height);
  context.fillStyle = '#202c2a';
  context.fillRect(cameraX, 640, level.width, level.height - 640);

  for (const platform of level.platforms) {
    context.fillStyle = platform.kind === 'start' ? '#40514b' : '#31423e';
    context.fillRect(platform.startX, platform.y, platform.endX - platform.startX, level.height - platform.y);
    context.strokeStyle = '#79918a';
    context.lineWidth = 2;
    context.strokeRect(platform.startX, platform.y, platform.endX - platform.startX, 6);
  }

  for (const obstacle of level.obstacles) {
    context.fillStyle = obstacle.active ? '#775b4d' : '#465a54';
    context.strokeStyle = obstacle.active ? '#e5b27e' : '#77918a';
    context.lineWidth = 2;
    context.fillRect(obstacle.x, obstacle.y, obstacle.width, obstacle.height);
    context.strokeRect(obstacle.x, obstacle.y, obstacle.width, 6);
  }

  drawDashedLine(level.checkpoint.x, 250, 690, cameraX);
  context.fillStyle = runner.state.checkpointReached ? '#91f2c4' : '#6b7d78';
  context.fillRect(level.goal.x, level.goal.y, level.goal.width, 280);

  context.strokeStyle = runner.state.held ? '#91f2c4' : '#7fa096';
  context.lineWidth = 5;
  context.lineCap = 'round';
  context.lineJoin = 'round';
  context.beginPath();
  runner.state.nodes.forEach((node, index) => {
    if (index === 0) context.moveTo(node.x, node.y);
    else context.lineTo(node.x, node.y);
  });
  context.stroke();
  for (const node of runner.state.nodes) {
    context.fillStyle = '#d4e4de';
    context.beginPath();
    context.arc(node.x, node.y, 3.8, 0, Math.PI * 2);
    context.fill();
  }

  const ball = runner.state.ball;
  context.fillStyle = '#f2d08f';
  context.strokeStyle = '#fff3cf';
  context.lineWidth = 2;
  context.beginPath();
  context.arc(ball.x, ball.y, level.ballRadius, 0, Math.PI * 2);
  context.fill();
  context.stroke();

  if (runner.state.status !== 'playing') {
    context.fillStyle = 'rgba(10, 14, 13, 0.70)';
    context.fillRect(cameraX + 42, 292, 306, 122);
    context.fillStyle = runner.state.status === 'success' ? '#91f2c4' : '#f2d08f';
    context.font = '700 22px ui-monospace, monospace';
    context.textAlign = 'center';
    context.fillText(runner.state.status === 'success' ? 'BRIDGE COMPLETE' : 'TRY AGAIN', cameraX + 195, 344);
    context.font = '12px ui-monospace, monospace';
    context.fillText(runner.state.status === 'success' ? 'NEXT LEVEL UNLOCKED' : 'FREE RESTART', cameraX + 195, 375);
  }

  const probe = sampleBridgeSurface(runner.state, level, runner.state.ball.x);
  window.__bridgeLastSurface = { y: probe.y, exists: probe.exists, kind: probe.kind };
}

function releaseInput(pointerId = activePointer) {
  if (pointerId !== null && activePointer !== null && pointerId !== activePointer) return;
  activePointer = null;
  setRunnerHeld(runner, false);
}

canvas.addEventListener('pointerdown', (event) => {
  if (runner.state.status !== 'playing') return;
  event.preventDefault();
  activePointer = event.pointerId;
  canvas.setPointerCapture?.(event.pointerId);
  setRunnerHeld(runner, true);
});
canvas.addEventListener('pointerup', (event) => releaseInput(event.pointerId));
canvas.addEventListener('pointercancel', (event) => releaseInput(event.pointerId));
window.addEventListener('pointerup', (event) => releaseInput(event.pointerId));
window.addEventListener('blur', () => releaseInput());

function reset() {
  releaseInput();
  runner.state = restart(runner.state, level);
  runner.accumulator = 0;
  runner.paused = false;
  updateLevelReadout();
  previousFrame = performance.now();
}

function nextLevel() {
  if (runner.state.status !== 'success') return;
  levelSeed = level.nextSeed;
  level = createLevel(levelSeed);
  runner = createRunner(level);
  history.replaceState(null, '', `?level=${levelSeed}`);
  updateLevelReadout();
  previousFrame = performance.now();
}

restartButton.addEventListener('click', reset);
nextButton.addEventListener('click', nextLevel);

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    releaseInput();
    pauseRunner(runner);
  } else {
    resumeRunner(runner);
    previousFrame = performance.now();
  }
});

function frame(time) {
  const frameSeconds = Math.max(0, (time - previousFrame) / 1000);
  previousFrame = time;
  advanceFrame(runner, frameSeconds);
  pressureFill.style.width = `${Math.round(runner.state.pressure * 100)}%`;
  clockOutput.textContent = `${runner.state.elapsed.toFixed(1)}s`;
  inputOutput.textContent = runner.state.held ? 'GROW' : runner.state.grounded ? 'ROLL' : 'AIR';
  stateOutput.textContent = runner.state.status === 'playing' ? `${runner.state.completedSegments}/${level.segments.length}` : runner.state.status.toUpperCase();
  updateLevelReadout();
  draw();
  requestAnimationFrame(frame);
}

window.__bridgeDebug = {
  ready: true,
  adsEnabled: false,
  networkAllowed: false,
  networkRequests: 0,
  seed: level.seed,
  levelNumber: level.levelNumber,
  archetype: level.archetype,
  hold(value) {
    setRunnerHeld(runner, Boolean(value));
  },
  pause() {
    releaseInput();
    pauseRunner(runner);
  },
  resume() {
    resumeRunner(runner);
    previousFrame = performance.now();
  },
  restart: reset,
  nextLevel,
  snapshot() {
    return {
      tick: runner.state.tick,
      elapsed: runner.state.elapsed,
      status: runner.state.status,
      held: runner.state.held,
      pressure: runner.state.pressure,
      bridgeFront: runner.state.bridgeFront,
      ballX: runner.state.ball.x,
      completedSegments: runner.state.completedSegments,
      jumps: runner.state.jumps,
      levelNumber: level.levelNumber,
      archetype: level.archetype,
      hash: hashState(runner.state),
      inputLog: runner.state.inputLog.map((event) => ({ ...event })),
      protections: { ...runner.state.protections },
      diagnostics: { ...runner.state.diagnostics },
    };
  },
};

updateLevelReadout();
requestAnimationFrame(frame);

