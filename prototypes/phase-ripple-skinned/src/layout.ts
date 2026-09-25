export type Rectangle = Readonly<{
  left: number;
  top: number;
  right: number;
  bottom: number;
  width: number;
  height: number;
}>;

export type ArenaRectangle = Rectangle & Readonly<{ size: number }>;

function rectangle(left: number, top: number, right: number, bottom: number): Rectangle {
  return { left, top, right, bottom, width: right - left, height: bottom - top };
}

export function computeLayout(width: number, height: number): Readonly<{
  safeInset: number;
  header: Rectangle;
  arena: ArenaRectangle;
  controls: Rectangle;
}> {
  const safeInset = Math.max(14, Math.round(width * 0.035));
  const header = rectangle(width * 0.16, height * 0.043, width * 0.84, height * 0.17);

  // The generated plate is 853×1844. These normalized anchors keep live rule geometry inside
  // the blank enamel regions and the circular safe interior at both acceptance viewports.
  const arenaSize = width * 0.92;
  const arenaLeft = (width - arenaSize) / 2;
  const arenaTop = height * 0.495 - arenaSize / 2;
  const arena: ArenaRectangle = {
    ...rectangle(arenaLeft, arenaTop, arenaLeft + arenaSize, arenaTop + arenaSize),
    size: arenaSize,
  };
  const controls = rectangle(width * 0.16, height * 0.80, width * 0.84, height - safeInset);
  return { safeInset, header, arena, controls };
}
