export type Rectangle = Readonly<{
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}>;

export type ArenaRectangle = Rectangle & Readonly<{ size: number }>;

export function computeLayout(width: number, height: number): Readonly<{
  safeInset: number;
  header: Rectangle;
  arena: ArenaRectangle;
  controls: Rectangle;
}> {
  const safeInset = Math.max(16, Math.round(width * 0.041));
  const scale = width / 390;
  const headerHeight = Math.round(76 * scale);
  const header: Rectangle = {
    left: safeInset,
    top: safeInset,
    right: width - safeInset,
    bottom: safeInset + headerHeight,
    width: width - safeInset * 2,
    height: headerHeight,
  };
  const arenaSize = Math.floor(Math.min(width - safeInset * 2, height * 0.47));
  const arenaLeft = Math.floor((width - arenaSize) / 2);
  const arenaTop = header.bottom + Math.round(12 * scale);
  const arena: ArenaRectangle = {
    left: arenaLeft,
    top: arenaTop,
    right: arenaLeft + arenaSize,
    bottom: arenaTop + arenaSize,
    width: arenaSize,
    height: arenaSize,
    size: arenaSize,
  };
  const controlsTop = arena.bottom + Math.round(16 * scale);
  const controlsBottom = height - safeInset;
  const controls: Rectangle = {
    left: safeInset,
    top: controlsTop,
    right: width - safeInset,
    bottom: controlsBottom,
    width: width - safeInset * 2,
    height: controlsBottom - controlsTop,
  };
  return { safeInset, header, arena, controls };
}
