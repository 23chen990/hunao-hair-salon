import { computeLayout } from './layout.js';
import { DEFAULT_RULES } from './rules.js';
                                                   

                                               

                                   
                          
                                  
                                  
   

const COLORS = Object.freeze({
  ink: '#174f4c',
  muted: '#6f6557',
  teal: '#078e91',
  tealSoft: 'rgba(8, 157, 158, 0.17)',
  charcoal: '#3d3832',
  coral: '#e65d4d',
  amber: '#d9911c',
  gold: '#d6a84e',
  ivory: '#fff7e7',
});

function modelScale(layout        )         {
  return (layout.arena.size / 2 - 13) / DEFAULT_RULES.arenaRadius;
}

export function modelPointToCanvas(layout        , point       )        {
  const scale = modelScale(layout);
  return {
    x: (layout.arena.left + layout.arena.right) / 2 + point.x * scale,
    y: (layout.arena.top + layout.arena.bottom) / 2 + point.y * scale,
  };
}

export function canvasPointToModel(layout        , x        , y        )        {
  const scale = modelScale(layout);
  return {
    x: (x - (layout.arena.left + layout.arena.right) / 2) / scale,
    y: (y - (layout.arena.top + layout.arena.bottom) / 2) / scale,
  };
}

function drawText(
  context                          ,
  value        ,
  x        ,
  y        ,
  size        ,
  color        ,
  weight = 700,
)       {
  context.fillStyle = color;
  context.font = `${weight} ${size}px "Songti SC", "STSong", "Noto Serif CJK SC", serif`;
  context.textAlign = 'center';
  context.textBaseline = 'middle';
  context.fillText(value, x, y);
}

function copyFor(state           )                                                             {
  if (state.outcome === 'success') return { title: '一起回家', detail: '轻触 · 下一关', color: COLORS.ink };
  if (state.outcome === 'boundary') return { title: '越界', detail: '轻触 · 再试一次', color: COLORS.coral };
  if (state.outcome === 'timeout') return { title: '同步超时', detail: '轻触 · 再试一次', color: COLORS.amber };
  if (state.click === null) return { title: '点击一次', detail: '让它们同时回家', color: COLORS.ink };
  const returned = state.pieces.filter((piece) => piece.reversed).length;
  return { title: '涟漪扩散中', detail: `${returned} / ${state.pieces.length} 已折返`, color: COLORS.teal };
}

function drawTrack(
  context                          ,
  center       ,
  angle        ,
  length        ,
  reversed         ,
)       {
  const endX = center.x + Math.cos(angle) * length;
  const endY = center.y + Math.sin(angle) * length;
  context.beginPath();
  context.moveTo(center.x, center.y);
  context.lineTo(endX, endY);
  context.setLineDash([5, 7]);
  context.strokeStyle = reversed ? 'rgba(0, 126, 130, .48)' : 'rgba(74, 61, 45, .27)';
  context.lineWidth = reversed ? 1.8 : 1.2;
  context.stroke();
  context.setLineDash([]);

  const direction = reversed ? angle + Math.PI : angle;
  const chevronRadius = length * 0.72;
  const cx = center.x + Math.cos(angle) * chevronRadius;
  const cy = center.y + Math.sin(angle) * chevronRadius;
  context.save();
  context.translate(cx, cy);
  context.rotate(direction);
  context.beginPath();
  context.moveTo(5, 0);
  context.lineTo(-3, -4);
  context.lineTo(-3, 4);
  context.closePath();
  context.fillStyle = reversed ? COLORS.teal : 'rgba(61, 56, 50, .52)';
  context.fill();
  context.restore();
}

function drawToken(
  context                          ,
  image                  ,
  position       ,
  direction        ,
  displaySize        ,
)       {
  context.save();
  context.translate(position.x, position.y);
  context.rotate(direction + Math.PI / 2);
  context.drawImage(image, -displaySize / 2, -displaySize / 2, displaySize, displaySize);
  context.restore();
}

export function renderGame(
  context                          ,
  state           ,
  width        ,
  height        ,
  assets            ,
)       {
  const layout = computeLayout(width, height);
  const scale = modelScale(layout);
  const center = modelPointToCanvas(layout, { x: 0, y: 0 });
  const boundaryRadius = DEFAULT_RULES.arenaRadius * scale;

  context.clearRect(0, 0, width, height);
  context.drawImage(assets.board, 0, 0, width, height);

  drawText(context, '相位涟漪', width / 2, layout.header.top + layout.header.height * 0.48, Math.round(width * 0.068), COLORS.ink, 800);
  drawText(context, `SEED ${state.level.seed}`, width / 2, layout.header.top + layout.header.height * 0.76, Math.round(width * 0.022), COLORS.muted, 600);

  context.save();
  context.beginPath();
  context.arc(center.x, center.y, boundaryRadius + 2, 0, Math.PI * 2);
  context.clip();

  for (const piece of state.pieces) {
    drawTrack(context, center, piece.angle, boundaryRadius, piece.reversed);
  }

  if (state.click !== null) {
    const click = modelPointToCanvas(layout, state.click);
    const waveRadius = Math.max(0, state.time - state.click.time) * DEFAULT_RULES.rippleSpeed * scale;
    context.beginPath();
    context.arc(click.x, click.y, waveRadius, 0, Math.PI * 2);
    context.fillStyle = COLORS.tealSoft;
    context.fill();
    context.strokeStyle = COLORS.teal;
    context.globalAlpha = 0.84;
    context.lineWidth = Math.max(2.5, width * 0.008);
    context.stroke();
    context.globalAlpha = 1;

    context.beginPath();
    context.arc(click.x, click.y, 5, 0, Math.PI * 2);
    context.fillStyle = COLORS.ivory;
    context.fill();
    context.strokeStyle = COLORS.teal;
    context.lineWidth = 2;
    context.stroke();
  }

  if (state.outcome === 'success') {
    context.beginPath();
    context.arc(center.x, center.y, 31 + Math.sin(state.time * 8) * 3, 0, Math.PI * 2);
    context.strokeStyle = COLORS.gold;
    context.globalAlpha = 0.72;
    context.lineWidth = 8;
    context.stroke();
    context.globalAlpha = 1;
  }

  if (state.firstArrival !== null && state.outcome === 'playing') {
    const remaining = Math.max(0, 1 - (state.time - state.firstArrival) / DEFAULT_RULES.syncWindow);
    context.beginPath();
    context.arc(center.x, center.y, 27, -Math.PI / 2, -Math.PI / 2 + Math.PI * 2 * remaining);
    context.strokeStyle = COLORS.amber;
    context.lineWidth = 5;
    context.stroke();
  }

  const tokenSize = Math.max(54, width * 0.16);
  for (const piece of state.pieces) {
    const position = modelPointToCanvas(layout, {
      x: Math.cos(piece.angle) * piece.radius,
      y: Math.sin(piece.angle) * piece.radius,
    });
    const direction = piece.reversed ? piece.angle + Math.PI : piece.angle;
    drawToken(
      context,
      piece.reversed ? assets.returnedPiece : assets.outboundPiece,
      position,
      direction,
      tokenSize,
    );
  }
  context.restore();

  if (state.outcome === 'boundary') {
    context.beginPath();
    context.arc(center.x, center.y, boundaryRadius, 0, Math.PI * 2);
    context.strokeStyle = COLORS.coral;
    context.globalAlpha = 0.84;
    context.lineWidth = 9;
    context.stroke();
    context.globalAlpha = 1;
  }

  const copy = copyFor(state);
  const copyCenterY = layout.controls.top + layout.controls.height * 0.31;
  drawText(context, copy.title, width / 2, copyCenterY, Math.round(width * 0.072), copy.color, 800);
  drawText(context, copy.detail, width / 2, copyCenterY + Math.round(width * 0.09), Math.round(width * 0.031), COLORS.muted, 600);
}
