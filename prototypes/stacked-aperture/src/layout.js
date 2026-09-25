export function computeLayout(width, height, layerCount) {
  if (width <= 0 || height <= 0 || ![3, 4].includes(layerCount)) {
    throw new RangeError('layout requires a positive viewport and 3–4 layers');
  }
  const safeTop = 18;
  const safeBottom = 16;
  const side = Math.max(18, Math.round(width * 0.055));
  const controls = {
    x: side,
    y: height - safeBottom - 116,
    width: width - side * 2,
    height: 104,
  };
  const board = {
    x: side,
    y: 172,
    width: width - side * 2,
    height: Math.min(430, controls.y - 192),
  };
  const pitch = board.height / layerCount;
  const hitHeight = Math.max(54, Math.min(72, pitch - 12));
  const sheetHeight = Math.min(48, hitHeight - 8);
  const layers = Array.from({ length: layerCount }, (_, index) => {
    const hitTop = board.y + index * pitch + (pitch - hitHeight) / 2;
    return {
      index,
      hitTop,
      hitHeight,
      centerY: hitTop + hitHeight / 2,
      sheetHeight,
    };
  });
  return {
    width,
    height,
    safeTop,
    safeBottom,
    header: { x: side, y: safeTop, width: width - side * 2, height: 130 },
    board,
    layers,
    controls,
  };
}
