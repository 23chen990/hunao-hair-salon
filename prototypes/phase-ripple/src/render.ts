import { computeLayout } from './layout.ts';
import { DEFAULT_RULES } from './rules.ts';
import type { GameState, Point } from './types.ts';

type Layout = ReturnType<typeof computeLayout>;

const COLORS = Object.freeze({
  background: '#071013',
  panel: '#0d191d',
  grid: '#193036',
  track: '#29454c',
  text: '#eaf8f5',
  muted: '#769096',
  cyan: '#6cf5dd',
  red: '#ff4d58',
  amber: '#ffc857',
});

function modelScale(layout: Layout): number {
  return (layout.arena.size / 2 - 12) / DEFAULT_RULES.arenaRadius;
}

export function modelPointToCanvas(layout: Layout, point: Point): Point {
  const scale = modelScale(layout);
  return {
    x: (layout.arena.left + layout.arena.right) / 2 + point.x * scale,
    y: (layout.arena.top + layout.arena.bottom) / 2 + point.y * scale,
  };
}

export function canvasPointToModel(layout: Layout, x: number, y: number): Point {
  const scale = modelScale(layout);
  return {
    x: (x - (layout.arena.left + layout.arena.right) / 2) / scale,
    y: (y - (layout.arena.top + layout.arena.bottom) / 2) / scale,
  };
}

function drawLabel(
  context: CanvasRenderingContext2D,
  value: string,
  x: number,
  y: number,
  size: number,
  color: string,
  align: CanvasTextAlign = 'left',
): void {
  context.fillStyle = color;
  context.font = `600 ${size}px ui-monospace, SFMono-Regular, Menlo, Consolas, monospace`;
  context.textAlign = align;
  context.textBaseline = 'alphabetic';
  context.fillText(value, x, y);
}

function outcomeCopy(state: GameState): Readonly<{ title: string; detail: string; color: string }> {
  if (state.outcome === 'success') return { title: '同步', detail: '轻触进入下一组', color: COLORS.cyan };
  if (state.outcome === 'boundary') return { title: '越界', detail: '轻触立即重试', color: COLORS.red };
  if (state.outcome === 'timeout') return { title: '失步', detail: '轻触立即重试', color: COLORS.amber };
  if (state.click === null) return { title: '点一次', detail: '用距离编排折返', color: COLORS.text };
  return { title: '涟漪扩散中', detail: '观察触发顺序', color: COLORS.cyan };
}

export function renderGame(
  context: CanvasRenderingContext2D,
  state: GameState,
  width: number,
  height: number,
): void {
  const layout = computeLayout(width, height);
  const scale = modelScale(layout);
  const center = modelPointToCanvas(layout, { x: 0, y: 0 });
  const boundaryRadius = DEFAULT_RULES.arenaRadius * scale;

  context.fillStyle = COLORS.background;
  context.fillRect(0, 0, width, height);

  context.fillStyle = COLORS.panel;
  context.fillRect(layout.header.left, layout.header.top, layout.header.width, layout.header.height);
  drawLabel(context, 'PHASE / RIPPLE', layout.header.left + 14, layout.header.top + 26, 11, COLORS.cyan);
  drawLabel(context, `SEED ${state.level.seed}`, layout.header.left + 14, layout.header.bottom - 14, 19, COLORS.text);
  drawLabel(
    context,
    `${state.level.pieces.length} 颗 · 1 次`,
    layout.header.right - 14,
    layout.header.bottom - 15,
    11,
    COLORS.muted,
    'right',
  );

  context.save();
  context.beginPath();
  context.arc(center.x, center.y, boundaryRadius + 5, 0, Math.PI * 2);
  context.clip();

  for (const ratio of [0.25, 0.5, 0.75]) {
    context.beginPath();
    context.strokeStyle = COLORS.grid;
    context.lineWidth = 1;
    context.arc(center.x, center.y, boundaryRadius * ratio, 0, Math.PI * 2);
    context.stroke();
  }

  for (const piece of state.pieces) {
    const edge = modelPointToCanvas(layout, {
      x: Math.cos(piece.angle) * DEFAULT_RULES.arenaRadius,
      y: Math.sin(piece.angle) * DEFAULT_RULES.arenaRadius,
    });
    context.beginPath();
    context.moveTo(center.x, center.y);
    context.lineTo(edge.x, edge.y);
    context.strokeStyle = COLORS.track;
    context.lineWidth = 1;
    context.stroke();
  }

  if (state.click !== null) {
    const click = modelPointToCanvas(layout, state.click);
    const waveRadius = Math.max(0, state.time - state.click.time) * DEFAULT_RULES.rippleSpeed * scale;
    context.beginPath();
    context.arc(click.x, click.y, waveRadius, 0, Math.PI * 2);
    context.strokeStyle = COLORS.cyan;
    context.globalAlpha = 0.82;
    context.lineWidth = 3;
    context.stroke();
    context.globalAlpha = 1;
    context.beginPath();
    context.moveTo(click.x - 6, click.y);
    context.lineTo(click.x + 6, click.y);
    context.moveTo(click.x, click.y - 6);
    context.lineTo(click.x, click.y + 6);
    context.lineWidth = 1;
    context.stroke();
  }

  context.beginPath();
  context.arc(center.x, center.y, 13, 0, Math.PI * 2);
  context.strokeStyle = COLORS.cyan;
  context.lineWidth = 2;
  context.stroke();
  context.beginPath();
  context.arc(center.x, center.y, 4, 0, Math.PI * 2);
  context.fillStyle = COLORS.cyan;
  context.fill();

  for (const piece of state.pieces) {
    const position = modelPointToCanvas(layout, {
      x: Math.cos(piece.angle) * piece.radius,
      y: Math.sin(piece.angle) * piece.radius,
    });
    const towardCenter = piece.reversed ? piece.angle + Math.PI : piece.angle;
    context.beginPath();
    context.arc(position.x, position.y, DEFAULT_RULES.pieceRadius * scale, 0, Math.PI * 2);
    context.fillStyle = piece.reversed ? COLORS.cyan : COLORS.text;
    context.fill();
    context.strokeStyle = piece.reversed ? COLORS.background : COLORS.track;
    context.lineWidth = 2;
    context.stroke();
    context.beginPath();
    context.moveTo(position.x, position.y);
    context.lineTo(
      position.x + Math.cos(towardCenter) * 14,
      position.y + Math.sin(towardCenter) * 14,
    );
    context.strokeStyle = piece.reversed ? COLORS.cyan : COLORS.text;
    context.lineWidth = 2;
    context.stroke();
  }
  context.restore();

  context.beginPath();
  context.arc(center.x, center.y, boundaryRadius, 0, Math.PI * 2);
  context.strokeStyle = COLORS.red;
  context.lineWidth = 4;
  context.stroke();

  if (state.firstArrival !== null && state.outcome === 'playing') {
    const remaining = Math.max(0, 1 - (state.time - state.firstArrival) / DEFAULT_RULES.syncWindow);
    context.beginPath();
    context.arc(center.x, center.y, 20, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * remaining);
    context.strokeStyle = COLORS.amber;
    context.lineWidth = 4;
    context.stroke();
  }

  const copy = outcomeCopy(state);
  const controlCenter = (layout.controls.left + layout.controls.right) / 2;
  const titleY = layout.controls.top + Math.max(28, layout.controls.height * 0.28);
  drawLabel(context, copy.title, controlCenter, titleY, 24, copy.color, 'center');
  drawLabel(context, copy.detail, controlCenter, titleY + 28, 12, COLORS.muted, 'center');

  const triggered = state.pieces.filter((piece) => piece.reversed).length;
  drawLabel(
    context,
    `${String(triggered).padStart(2, '0')} / ${String(state.pieces.length).padStart(2, '0')}  TURNED`,
    controlCenter,
    Math.min(layout.controls.bottom - 12, titleY + 66),
    10,
    COLORS.track,
    'center',
  );
}
