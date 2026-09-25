import { applyOffsetMove, dropBead } from './model.js';

function keyOf(offsets) {
  return offsets.join(',');
}

export function solveLevel(level, startOffsets = level.initialOffsets) {
  const start = [...startOffsets];
  if (dropBead(level, start).passed) return { distance: 0, moves: [], offsets: start };
  const queue = [start];
  let cursor = 0;
  const parent = new Map([[keyOf(start), null]]);
  let goalKey = null;

  while (cursor < queue.length && goalKey === null) {
    const offsets = queue[cursor];
    cursor += 1;
    for (let layer = 0; layer < level.layerCount; layer += 1) {
      for (const delta of [-1, 1]) {
        const candidate = offsets[layer] + delta;
        if (candidate < level.slotMin || candidate > level.slotMax) continue;
        const move = { layer, delta };
        const next = applyOffsetMove(level, offsets, move);
        const key = keyOf(next);
        if (parent.has(key)) continue;
        parent.set(key, { previous: keyOf(offsets), move });
        if (dropBead(level, next).passed) {
          goalKey = key;
          break;
        }
        queue.push(next);
      }
      if (goalKey !== null) break;
    }
  }

  if (goalKey === null) return null;
  const moves = [];
  let key = goalKey;
  while (parent.get(key) !== null) {
    const entry = parent.get(key);
    moves.push(entry.move);
    key = entry.previous;
  }
  moves.reverse();
  return {
    distance: moves.length,
    moves,
    offsets: goalKey.split(',').map(Number),
  };
}

export function applyMoves(level, offsets, moves) {
  return moves.reduce((current, move) => applyOffsetMove(level, current, move), [...offsets]);
}

export function* enumerateOffsets(level) {
  const offsets = Array(level.layerCount).fill(level.slotMin);
  function* visit(index) {
    if (index === offsets.length) {
      yield [...offsets];
      return;
    }
    for (let slot = level.slotMin; slot <= level.slotMax; slot += 1) {
      offsets[index] = slot;
      yield* visit(index + 1);
    }
  }
  yield* visit(0);
}
