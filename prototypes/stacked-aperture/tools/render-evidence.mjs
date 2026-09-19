import { createRequire } from 'node:module';
import { mkdir, writeFile } from 'node:fs/promises';
import { resolve } from 'node:path';
import { createEvidenceFrame } from '../src/evidence-frame.js';
import { EVIDENCE_LABELS } from '../src/evidence-labels.js';
import { computeLayout } from '../src/layout.js';

const require = createRequire(import.meta.url);
const { createCanvas } = require('/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');
const width = 390;
const height = 844;
const canvas = createCanvas(width, height);
const ctx = canvas.getContext('2d');
const frame = createEvidenceFrame(1_001);
const { level, state, solution, appliedMoves } = frame;
const layout = computeLayout(width, height, level.layerCount);

function roundedRect(target, x, y, w, h, radius) {
  const r = Math.min(radius, w / 2, h / 2);
  target.beginPath();
  target.moveTo(x + r, y);
  target.lineTo(x + w - r, y);
  target.quadraticCurveTo(x + w, y, x + w, y + r);
  target.lineTo(x + w, y + h - r);
  target.quadraticCurveTo(x + w, y + h, x + w - r, y + h);
  target.lineTo(x + r, y + h);
  target.quadraticCurveTo(x, y + h, x, y + h - r);
  target.lineTo(x, y + r);
  target.quadraticCurveTo(x, y, x + r, y);
  target.closePath();
}

function text(value, x, y, size, color = '#e8edf6', align = 'left', weight = 500) {
  ctx.fillStyle = color;
  ctx.font = `${weight >= 700 ? 'bold' : 'normal'} ${size}px sans-serif`;
  ctx.textAlign = align;
  ctx.textBaseline = 'middle';
  ctx.fillText(value, x, y);
}

function button(rect, label, emphasis = false) {
  roundedRect(ctx, rect.x, rect.y, rect.width, rect.height, 12);
  ctx.fillStyle = emphasis ? '#e8edf6' : '#222c3b';
  ctx.fill();
  ctx.strokeStyle = emphasis ? '#ffffff' : '#52627a';
  ctx.lineWidth = 1.5;
  ctx.stroke();
  text(label, rect.x + rect.width / 2, rect.y + rect.height / 2, 15, emphasis ? '#10151f' : '#e8edf6', 'center', 700);
}

const gradient = ctx.createLinearGradient(0, 0, 0, height);
gradient.addColorStop(0, '#182131');
gradient.addColorStop(1, '#0d121a');
ctx.fillStyle = gradient;
ctx.fillRect(0, 0, width, height);

text(EVIDENCE_LABELS.title, layout.header.x, layout.header.y + 16, 22, '#f4f7fb', 'left', 800);
text(EVIDENCE_LABELS.subtitle, layout.header.x, layout.header.y + 45, 11, '#8fa1ba', 'left', 700);
text(`SEED ${level.seed} / ${level.layerCount} LAYERS`, layout.header.x, layout.header.y + 76, 13, '#c9d3e1', 'left', 650);
text(`SHORTEST ${solution.distance} / MOVED ${state.moveCount} / DROPS ${state.dropCount}`, layout.header.x, layout.header.y + 100, 12, '#8fa1ba');
text(EVIDENCE_LABELS.modelFrame, layout.header.x, layout.header.y + 123, 11, '#708096');

const centerX = width / 2;
ctx.strokeStyle = '#344155';
ctx.lineWidth = 2;
ctx.setLineDash([5, 8]);
ctx.beginPath();
ctx.moveTo(centerX, layout.board.y - 25);
ctx.lineTo(centerX, layout.board.y + layout.board.height + 34);
ctx.stroke();
ctx.setLineDash([]);

for (const sheet of layout.layers) {
  for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
    const x = centerX + slot * level.slotSpacing;
    ctx.beginPath();
    ctx.moveTo(x, sheet.centerY - 4);
    ctx.lineTo(x, sheet.centerY + 4);
    ctx.strokeStyle = slot === 0 ? '#708096' : '#364357';
    ctx.lineWidth = 1;
    ctx.stroke();
  }
}

for (let index = 0; index < level.layerCount; index += 1) {
  const sheet = layout.layers[index];
  const layer = level.layers[index];
  const plateCenterX = centerX + state.offsets[index] * level.slotSpacing;
  const sheetWidth = Math.min(342, layout.board.width - 8);
  const x = plateCenterX - sheetWidth / 2;
  const y = sheet.centerY - sheet.sheetHeight / 2;
  roundedRect(ctx, x, y, sheetWidth, sheet.sheetHeight, 10);
  ctx.fillStyle = layer.color;
  ctx.globalAlpha = layer.alpha;
  ctx.fill();
  ctx.globalAlpha = 1;
  for (const hole of layer.holes) {
    const holeX = plateCenterX + hole.localSlot * level.slotSpacing;
    ctx.beginPath();
    ctx.arc(holeX, sheet.centerY, hole.radius, 0, Math.PI * 2);
    ctx.fillStyle = '#111923';
    ctx.fill();
  }
  roundedRect(ctx, x, y, sheetWidth, sheet.sheetHeight, 10);
  ctx.strokeStyle = layer.color;
  ctx.lineWidth = state.lastDrop.blockerIndex === index ? 4 : 2;
  ctx.stroke();
  for (const hole of layer.holes) {
    const holeX = plateCenterX + hole.localSlot * level.slotSpacing;
    ctx.beginPath();
    ctx.arc(holeX, sheet.centerY, hole.radius, 0, Math.PI * 2);
    ctx.strokeStyle = '#dce4f0';
    ctx.lineWidth = 1.5;
    ctx.stroke();
  }
  text(`L${index + 1}`, Math.max(18, x + 20), sheet.centerY, 13, layer.color, 'center', 800);
}

const blockerY = layout.layers[state.lastDrop.blockerIndex].centerY - level.beadRadius;
ctx.beginPath();
ctx.arc(centerX, blockerY, level.beadRadius, 0, Math.PI * 2);
ctx.fillStyle = '#ff6f78';
ctx.shadowColor = '#ff6f78';
ctx.shadowBlur = 12;
ctx.fill();
ctx.shadowBlur = 0;
text(`${EVIDENCE_LABELS.firstBlocker}: L${state.lastDrop.blockerIndex + 1}`, centerX, layout.board.y + layout.board.height + 47, 15, '#ff7b83', 'center', 800);

const reset = { x: layout.controls.x, y: layout.controls.y, width: 82, height: 52 };
const retry = {
  x: layout.controls.x + 92,
  y: layout.controls.y,
  width: layout.controls.width - 92,
  height: 52,
};
button(reset, EVIDENCE_LABELS.reset);
button(retry, EVIDENCE_LABELS.retry, true);
text(EVIDENCE_LABELS.disclaimer, centerX, height - 24, 9, '#66758a', 'center', 500);

const evidenceRoot = resolve(import.meta.dirname, '..', 'evidence');
await mkdir(evidenceRoot, { recursive: true });
const imagePath = resolve(evidenceRoot, 'stacked-aperture-model-frame-390x844.png');
await writeFile(imagePath, canvas.toBuffer('image/png'));
await writeFile(resolve(evidenceRoot, 'model-frame.json'), `${JSON.stringify({
  generatedAt: new Date().toISOString(),
  image: 'stacked-aperture-model-frame-390x844.png',
  source: 'actual generated level + BFS near-solution + real dropBead result',
  seed: level.seed,
  layerCount: level.layerCount,
  shortestDistance: solution.distance,
  appliedMoves,
  offsets: state.offsets,
  firstBlockerIndex: state.lastDrop.blockerIndex,
  browserClaim: false,
}, null, 2)}\n`);
process.stdout.write(`${imagePath}\n`);
