export function snapDragToSteps(deltaPx, spacingPx) {
  if (!Number.isFinite(deltaPx) || !Number.isFinite(spacingPx) || spacingPx <= 0) {
    throw new TypeError('drag and spacing must be finite, with positive spacing');
  }
  return Math.sign(deltaPx) * Math.floor(Math.abs(deltaPx) / spacingPx + 0.5);
}

export function snapCatchWidth(spacingPx) {
  if (!Number.isFinite(spacingPx) || spacingPx <= 0) throw new TypeError('spacing must be positive');
  return spacingPx;
}
