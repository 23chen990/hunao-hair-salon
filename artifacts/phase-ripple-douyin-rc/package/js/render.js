const { computeLayout } = require('./layout.js');
const { DEFAULT_RULES } = require('./rules.js');
const { resultActions } = require('./ui-actions.js');
                                                   
                                                     

                                               

                                   
                          
                                  
                                  
   

                                     
                      
                  
                  
                  
                       
                  
                       
                           
                   
                     
     
                      
                         
                              
                   
                        
                        
     
                      
                        
   

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

function modelPointToCanvas(layout        , point       )        {
  const scale = modelScale(layout);
  return {
    x: (layout.arena.left + layout.arena.right) / 2 + point.x * scale,
    y: (layout.arena.top + layout.arena.bottom) / 2 + point.y * scale,
  };
}

function canvasPointToModel(layout        , x        , y        )        {
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

function arrivalSpread(state           )                {
  const arrivals = state.pieces
    .map((piece) => piece.arrivalTime)
    .filter((time)                 => time !== null);
  if (arrivals.length !== state.pieces.length) return null;
  return Math.max(...arrivals) - Math.min(...arrivals);
}

function copyFor(
  state           ,
  meta               ,
)                                                             {
  const tutorial = meta?.tutorial;
  if (tutorial?.phase === 'guided') {
    if (tutorial.lesson === 1) {
      return tutorial.waitingForTarget
        ? { title: '点一下，看看会发生什么', detail: '只看这一枚棋子', color: COLORS.teal }
        : { title: '先看它往外跑', detail: '光点亮起时，再点一下', color: COLORS.ink };
    }
    return tutorial.waitingForTarget
      ? { title: '点这里，安排先后顺序', detail: '这次观察第 ①、② 次碰撞', color: COLORS.teal }
      : { title: '两枚棋子正在向外跑', detail: '点的位置，会改变涟漪先碰到谁', color: COLORS.ink };
  }
  if (meta?.tutorial.phase === 'impact') {
    if (tutorial?.lesson === 1) {
      return { title: '它掉头了！', detail: '涟漪碰到棋子 → 棋子立刻掉头', color: COLORS.teal };
    }
    const ordinal = tutorial?.impactIndex === 1 ? '① 第一枚先掉头' : '② 第二枚后掉头';
    return { title: ordinal, detail: '点的位置，决定涟漪碰撞的先后', color: COLORS.teal };
  }
  if (tutorial?.phase === 'result') {
    return tutorial.lesson === 1
      ? { title: '它回到中心了', detail: '你已经学会：涟漪碰到 → 掉头', color: COLORS.ink }
      : { title: '先后掉头，同时到家', detail: '你已经学会：落点决定碰撞顺序', color: COLORS.ink };
  }
  if (state.outcome === 'success') {
    const spread = arrivalSpread(state) ?? DEFAULT_RULES.syncWindow;
    const sync = Math.max(0, Math.round((1 - spread / DEFAULT_RULES.syncWindow) * 100));
    const streak = (meta?.progress.streak ?? 0) + 1;
    const progressCopy = meta?.progress.stageComplete
      ? `第 ${meta.progress.stage} 关完成`
      : `回合 ${meta?.progress.round ?? 1} / ${meta?.progress.roundTotal ?? 1}`;
    return { title: '完美归心！', detail: `${progressCopy} · 同步度 ${sync}% · ${streak} 连胜`, color: COLORS.ink };
  }
  if (state.outcome === 'boundary') {
    return { title: '有棋子跑出去了', detail: '直接重试，或用提示查看安全落点', color: COLORS.coral };
  }
  if (state.outcome === 'timeout') {
    return { title: '到家时间没对齐', detail: '直接重试，或用提示查看安全落点', color: COLORS.amber };
  }
  if (state.click === null) {
    if (meta?.hintActive) {
      return { title: '试试发光区域附近', detail: '落点决定涟漪先碰到谁', color: COLORS.teal };
    }
    return { title: '选一个落点，只点一次', detail: '落点决定折返顺序 · 目标是同时归心', color: COLORS.ink };
  }
  const returned = state.pieces.filter((piece) => piece.reversed).length;
  return { title: `${returned} / ${state.pieces.length} 已折返`, detail: '涟漪碰到棋子，它就会掉头', color: COLORS.teal };
}

function drawRoundProgress(
  context                          ,
  width        ,
  y        ,
  round        ,
  roundTotal        ,
)       {
  const spacing = 18;
  const startX = width / 2 - ((roundTotal - 1) * spacing) / 2;
  for (let index = 0; index < roundTotal; index += 1) {
    context.beginPath();
    context.arc(startX + index * spacing, y, index + 1 === round ? 4.5 : 3.5, 0, Math.PI * 2);
    context.fillStyle = index + 1 <= round ? COLORS.teal : 'rgba(111, 101, 87, .28)';
    context.fill();
  }
}

function drawRoundRect(
  context                          ,
  x        ,
  y        ,
  width        ,
  height        ,
  radius        ,
)       {
  context.beginPath();
  context.roundRect(x, y, width, height, radius);
}

function drawGoal(context                          , center       , width        )       {
  context.beginPath();
  context.arc(center.x, center.y, Math.max(17, width * 0.047), 0, Math.PI * 2);
  context.fillStyle = 'rgba(255, 247, 231, .72)';
  context.fill();
  context.strokeStyle = 'rgba(7, 142, 145, .72)';
  context.lineWidth = 2;
  context.stroke();
  drawText(context, '归心', center.x, center.y, Math.round(width * 0.026), COLORS.ink, 800);
}

function drawHintTarget(
  context                          ,
  point       ,
  width        ,
  animationTime        ,
  label        ,
)       {
  const pulse = 1 + Math.sin(animationTime * 6) * 0.12;
  const radius = Math.max(21, width * 0.061) * pulse;
  context.beginPath();
  context.arc(point.x, point.y, radius, 0, Math.PI * 2);
  context.fillStyle = 'rgba(255, 247, 231, .78)';
  context.fill();
  context.strokeStyle = COLORS.teal;
  context.lineWidth = 4;
  context.stroke();
  context.beginPath();
  context.arc(point.x, point.y, 6, 0, Math.PI * 2);
  context.fillStyle = COLORS.teal;
  context.fill();
  drawText(context, label, point.x, point.y - radius - 14, Math.round(width * 0.03), COLORS.ink, 800);
}

function drawSuccessBurst(
  context                          ,
  center       ,
  animationTime        ,
)       {
  for (let index = 0; index < 12; index += 1) {
    const angle = (index / 12) * Math.PI * 2 + animationTime * 0.18;
    const distance = 47 + (index % 3) * 13;
    const x = center.x + Math.cos(angle) * distance;
    const y = center.y + Math.sin(angle) * distance;
    context.save();
    context.translate(x, y);
    context.rotate(angle);
    context.fillStyle = index % 2 === 0 ? COLORS.gold : COLORS.teal;
    context.fillRect(-4, -2, 9, 4);
    context.restore();
  }
}

function drawImpactFreeze(
  context                          ,
  position       ,
  width        ,
  animationTime        ,
  label        ,
)       {
  const pulse = 34 + Math.sin(animationTime * 7) * 3;
  context.beginPath();
  context.arc(position.x, position.y, pulse, 0, Math.PI * 2);
  context.strokeStyle = COLORS.gold;
  context.lineWidth = 7;
  context.globalAlpha = 0.84;
  context.stroke();
  context.globalAlpha = 1;
  drawRoundRect(context, position.x - 49, position.y - 59, 98, 28, 14);
  context.fillStyle = COLORS.ivory;
  context.fill();
  context.strokeStyle = COLORS.teal;
  context.lineWidth = 2;
  context.stroke();
  drawText(context, label, position.x, position.y - 45, Math.round(width * 0.03), COLORS.ink, 800);
}

function drawImpactOrderBadge(
  context                          ,
  position       ,
  index        ,
  width        ,
)       {
  context.beginPath();
  context.arc(position.x + 24, position.y - 24, 13, 0, Math.PI * 2);
  context.fillStyle = COLORS.teal;
  context.fill();
  drawText(context, index === 1 ? '①' : '②', position.x + 24, position.y - 24, Math.round(width * 0.031), COLORS.ivory, 800);
}

function drawResultButtons(
  context                          ,
  state           ,
  width        ,
  height        ,
  tutorial                           ,
)       {
  const actions = resultActions(width, height, state.outcome);
  for (const entry of actions) {
    drawRoundRect(context, entry.left, entry.top, entry.width, entry.height, entry.height / 2);
    context.fillStyle = entry.action === 'hint' ? COLORS.ivory : COLORS.teal;
    context.fill();
    context.strokeStyle = COLORS.teal;
    context.lineWidth = 2;
    context.stroke();
    const label = tutorial?.phase === 'result' && entry.action === 'next'
      ? (tutorial.lesson === tutorial.lessonTotal ? '开始第 1 关' : '下一步')
      : (entry.action === 'hint' ? `▶ ${entry.label}` : entry.label);
    drawText(
      context,
      label,
      entry.left + entry.width / 2,
      entry.top + entry.height / 2,
      Math.round(width * 0.029),
      entry.action === 'hint' ? COLORS.ink : COLORS.ivory,
      800,
    );
  }
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

function renderGame(
  context                          ,
  state           ,
  width        ,
  height        ,
  assets            ,
  meta               ,
)       {
  const layout = computeLayout(width, height);
  const scale = modelScale(layout);
  const center = modelPointToCanvas(layout, { x: 0, y: 0 });
  const boundaryRadius = DEFAULT_RULES.arenaRadius * scale;

  context.clearRect(0, 0, width, height);
  context.drawImage(assets.board, 0, 0, width, height);

  drawText(context, '相位涟漪', width / 2, layout.header.top + layout.header.height * 0.48, Math.round(width * 0.068), COLORS.ink, 800);
  const tutorialActive = meta !== undefined && meta.tutorial.phase !== 'complete';
  const levelCopy = meta === undefined
    ? `SEED ${state.level.seed}`
    : tutorialActive
      ? `教学 ${meta.tutorial.lesson} / ${meta.tutorial.lessonTotal}  ·  ${state.pieces.length} 枚棋子`
      : `第 ${meta.progress.stage} 关  ·  回合 ${meta.progress.round} / ${meta.progress.roundTotal}  ·  ${state.pieces.length} 枚`;
  drawText(context, levelCopy, width / 2, layout.header.top + layout.header.height * 0.76, Math.round(width * 0.023), COLORS.muted, 700);
  if (meta !== undefined) {
    drawRoundProgress(
      context,
      width,
      layout.header.top + layout.header.height * 0.91,
      tutorialActive ? meta.tutorial.lesson : meta.progress.round,
      tutorialActive ? meta.tutorial.lessonTotal : meta.progress.roundTotal,
    );
  }

  context.save();
  context.beginPath();
  context.arc(center.x, center.y, boundaryRadius + 2, 0, Math.PI * 2);
  context.clip();

  for (const piece of state.pieces) {
    drawTrack(context, center, piece.angle, boundaryRadius, piece.reversed);
  }

  drawGoal(context, center, width);

  if (meta?.tutorial.waitingForTarget || (meta?.hintActive && state.click === null)) {
    const target = modelPointToCanvas(layout, state.level.referenceInput);
    drawHintTarget(
      context,
      target,
      width,
      meta?.animationTime ?? state.time,
      meta?.tutorial.waitingForTarget ? '点这里' : '提示区',
    );
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
    drawSuccessBurst(context, center, meta?.animationTime ?? state.time);
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
    if (tutorialActive && meta?.tutorial.lesson === 2 && piece.reversed) {
      const order = state.triggerOrder.indexOf(piece.id) + 1;
      if (order > 0) drawImpactOrderBadge(context, position, order, width);
    }
  }

  if (meta?.tutorial.phase === 'impact') {
    const firstId = state.triggerOrder[0];
    const impacted = state.pieces.find((piece) => piece.id === firstId);
    if (impacted !== undefined) {
      const impactLabel = meta.tutorial.lesson === 1
        ? '碰到 → 掉头'
        : (meta.tutorial.impactIndex === 1 ? '① 先碰到' : '② 后碰到');
      drawImpactFreeze(context, modelPointToCanvas(layout, {
        x: Math.cos(impacted.angle) * impacted.radius,
        y: Math.sin(impacted.angle) * impacted.radius,
      }), width, meta.animationTime, impactLabel);
    }
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

  const copy = copyFor(state, meta);
  const copyCenterY = layout.controls.top + layout.controls.height * 0.31;
  drawText(context, copy.title, width / 2, copyCenterY, Math.round(width * 0.072), copy.color, 800);
  drawText(context, copy.detail, width / 2, copyCenterY + Math.round(width * 0.09), Math.round(width * 0.031), COLORS.muted, 600);
  if (meta?.tutorial.phase === 'impact') {
    drawText(context, '轻触继续', width / 2, copyCenterY + Math.round(width * 0.155), Math.round(width * 0.028), COLORS.teal, 800);
  } else if (state.outcome !== 'playing') {
    drawResultButtons(context, state, width, height, meta?.tutorial);
  }
}

module.exports.modelPointToCanvas = modelPointToCanvas;
module.exports.canvasPointToModel = canvasPointToModel;
module.exports.renderGame = renderGame;
