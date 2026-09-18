const MIN_TOUCH = 44;

function boardGeometry(x, y, cell) {
  const width = cell * 12;
  const height = cell * 8;
  return {
    x,
    y,
    cell,
    columns: 12,
    rows: 8,
    width,
    height,
    right: x + width,
    bottom: y + height,
  };
}

export function computeLayout(viewportWidth, viewportHeight) {
  const width = Math.max(320, Number(viewportWidth) || 390);
  const height = Math.max(600, Number(viewportHeight) || 844);
  const margin = 22;
  const cell = (width - margin * 2) / 12;
  const targetBoard = boardGeometry(margin, 30, cell);
  const rollerY = targetBoard.bottom + 39;
  const inkBoard = boardGeometry(margin, targetBoard.bottom + 78, cell);
  const canvasHeight = Math.ceil(inkBoard.bottom + 22);
  const leftHandle = { x: targetBoard.x, y: rollerY, radius: MIN_TOUCH / 2 };
  const rightHandle = { x: targetBoard.right, y: rollerY, radius: MIN_TOUCH / 2 };
  return {
    viewportWidth: width,
    viewportHeight: height,
    canvasHeight,
    targetBoard,
    inkBoard,
    rollerY,
    leftHandle,
    rightHandle,
    touchTargets: {
      leftHandle: { width: MIN_TOUCH, height: MIN_TOUCH },
      rightHandle: { width: MIN_TOUCH, height: MIN_TOUCH },
      facePrevious: { width: MIN_TOUCH, height: 48 },
      faceNext: { width: MIN_TOUCH, height: 48 },
      undo: { width: MIN_TOUCH, height: 48 },
      restart: { width: MIN_TOUCH, height: 48 },
      nextSeed: { width: MIN_TOUCH, height: 48 },
      contactHint: { width: MIN_TOUCH, height: 48 },
      simulateAd: { width: MIN_TOUCH, height: 48 },
    },
  };
}

function insideCircle(handle, x, y) {
  return Math.hypot(x - handle.x, y - handle.y) <= handle.radius + 4;
}

export function hitTestRollStart(layout, x, y) {
  if (insideCircle(layout.leftHandle, x, y)) return 1;
  if (insideCircle(layout.rightHandle, x, y)) return -1;
  return 0;
}
