type PlatformApi = Readonly<Record<string, any>>;

export function createPlatformStorage(ttApi: PlatformApi): Readonly<{
  getItem: (key: string) => string | null;
  setItem: (key: string, value: string) => void;
}> {
  return {
    getItem(key: string): string | null {
      try {
        const value = ttApi.getStorageSync(key);
        return typeof value === 'string' ? value : null;
      } catch {
        return null;
      }
    },
    setItem(key: string, value: string): void {
      try {
        ttApi.setStorageSync(key, value);
      } catch {
        // Event logging must never make the game unplayable.
      }
    },
  };
}

export function readViewport(ttApi: PlatformApi): Readonly<{
  width: number;
  height: number;
  pixelRatio: number;
}> {
  let info: Readonly<Record<string, unknown>> = {};
  try {
    info = ttApi.getSystemInfoSync() ?? {};
  } catch {
    // Safe phone defaults keep the first frame renderable in degraded runtimes.
  }
  const width = Number(info.windowWidth);
  const height = Number(info.windowHeight);
  const ratio = Number(info.pixelRatio);
  return {
    width: Number.isFinite(width) && width > 0 ? Math.round(width) : 390,
    height: Number.isFinite(height) && height > 0 ? Math.round(height) : 844,
    pixelRatio: Number.isFinite(ratio) && ratio > 0 ? Math.min(2, ratio) : 1,
  };
}

export function normalizeTouchPoint(event: Readonly<Record<string, any>>): Readonly<{
  x: number;
  y: number;
}> | null {
  const touch = event.changedTouches?.[0] ?? event.touches?.[0];
  if (touch === undefined) return null;
  const x = Number(touch.clientX ?? touch.x);
  const y = Number(touch.clientY ?? touch.y);
  return Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null;
}

export function loadPlatformImage(
  canvas: Readonly<Record<string, any>>,
  ttApi: PlatformApi,
  source: string,
): Promise<any> {
  return new Promise((resolve, reject) => {
    try {
      const image = typeof canvas.createImage === 'function'
        ? canvas.createImage()
        : ttApi.createImage();
      image.onload = () => resolve(image);
      image.onerror = () => reject(new Error(`Failed to load ${source}`));
      image.src = source;
    } catch (error) {
      reject(error instanceof Error ? error : new Error(`Failed to load ${source}`));
    }
  });
}

export function configureCanvas(
  canvas: Readonly<Record<string, any>>,
  context: Readonly<Record<string, any>>,
  viewport: Readonly<{ width: number; height: number; pixelRatio: number }>,
): void {
  canvas.width = Math.round(viewport.width * viewport.pixelRatio);
  canvas.height = Math.round(viewport.height * viewport.pixelRatio);
  if (typeof context.setTransform === 'function') {
    context.setTransform(viewport.pixelRatio, 0, 0, viewport.pixelRatio, 0, 0);
  } else if (typeof context.scale === 'function') {
    context.scale(viewport.pixelRatio, viewport.pixelRatio);
  }
  context.imageSmoothingEnabled = true;
}

export function installRoundRect(context: Readonly<Record<string, any>>): void {
  if (typeof context.roundRect === 'function') return;
  context.roundRect = function roundRect(x: number, y: number, width: number, height: number, radius: number): void {
    const safeRadius = Math.max(0, Math.min(radius, Math.abs(width) / 2, Math.abs(height) / 2));
    this.moveTo(x + safeRadius, y);
    this.lineTo(x + width - safeRadius, y);
    this.quadraticCurveTo(x + width, y, x + width, y + safeRadius);
    this.lineTo(x + width, y + height - safeRadius);
    this.quadraticCurveTo(x + width, y + height, x + width - safeRadius, y + height);
    this.lineTo(x + safeRadius, y + height);
    this.quadraticCurveTo(x, y + height, x, y + height - safeRadius);
    this.lineTo(x, y + safeRadius);
    this.quadraticCurveTo(x, y, x + safeRadius, y);
    this.closePath();
  };
}

export function isSidebarLaunchOptions(options: Readonly<Record<string, unknown>> | null | undefined): boolean {
  return options?.launch_from === 'homepage' && options?.location === 'sidebar_card';
}

export function checkSidebarSupport(ttApi: PlatformApi): Promise<boolean> {
  if (typeof ttApi.checkScene !== 'function') return Promise.resolve(false);
  return new Promise((resolve) => {
    ttApi.checkScene({
      scene: 'sidebar',
      success: (result: Readonly<Record<string, unknown>>) => resolve(result?.isExist === true),
      fail: () => resolve(false),
    });
  });
}

export function navigateToSidebar(ttApi: PlatformApi): void {
  if (typeof ttApi.navigateToScene !== 'function') return;
  ttApi.navigateToScene({
    scene: 'sidebar',
    fail: (error: unknown) => console.warn('navigateToScene failed', error),
  });
}

export function scheduleFrame(
  canvas: Readonly<Record<string, any>>,
  callback: (now: number) => void,
): void {
  if (typeof canvas.requestAnimationFrame === 'function') {
    canvas.requestAnimationFrame(() => callback(Date.now()));
    return;
  }
  if (typeof requestAnimationFrame === 'function') {
    requestAnimationFrame(() => callback(Date.now()));
    return;
  }
  setTimeout(() => callback(Date.now()), 16);
}
