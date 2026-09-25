function createPlatformStorage(ttApi             )            
                                          
                                                
   {
  return {
    getItem(key        )                {
      try {
        const value = ttApi.getStorageSync(key);
        return typeof value === 'string' ? value : null;
      } catch {
        return null;
      }
    },
    setItem(key        , value        )       {
      try {
        ttApi.setStorageSync(key, value);
      } catch {
        // Event logging must never make the game unplayable.
      }
    },
  };
}

function readViewport(ttApi             )            
                
                 
                     
   {
  let info                                    = {};
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

function normalizeTouchPoint(event                               )            
            
            
          {
  const touch = event.changedTouches?.[0] ?? event.touches?.[0];
  if (touch === undefined) return null;
  const x = Number(touch.clientX ?? touch.x);
  const y = Number(touch.clientY ?? touch.y);
  return Number.isFinite(x) && Number.isFinite(y) ? { x, y } : null;
}

function loadPlatformImage(
  canvas                               ,
  ttApi             ,
  source        ,
)               {
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

function configureCanvas(
  canvas                               ,
  context                               ,
  viewport                                                                 ,
)       {
  canvas.width = Math.round(viewport.width * viewport.pixelRatio);
  canvas.height = Math.round(viewport.height * viewport.pixelRatio);
  if (typeof context.setTransform === 'function') {
    context.setTransform(viewport.pixelRatio, 0, 0, viewport.pixelRatio, 0, 0);
  } else if (typeof context.scale === 'function') {
    context.scale(viewport.pixelRatio, viewport.pixelRatio);
  }
  context.imageSmoothingEnabled = true;
}

function installRoundRect(context                               )       {
  if (typeof context.roundRect === 'function') return;
  context.roundRect = function roundRect(x        , y        , width        , height        , radius        )       {
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

function isSidebarLaunchOptions(options                                                      )          {
  return options?.launch_from === 'homepage' && options?.location === 'sidebar_card';
}

function checkSidebarSupport(ttApi             )                   {
  if (typeof ttApi.checkScene !== 'function') return Promise.resolve(false);
  return new Promise((resolve) => {
    ttApi.checkScene({
      scene: 'sidebar',
      success: (result                                   ) => resolve(result?.isExist === true),
      fail: () => resolve(false),
    });
  });
}

function navigateToSidebar(ttApi             )       {
  if (typeof ttApi.navigateToScene !== 'function') return;
  ttApi.navigateToScene({
    scene: 'sidebar',
    fail: (error         ) => console.warn('navigateToScene failed', error),
  });
}

function scheduleFrame(
  canvas                               ,
  callback                       ,
)       {
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

module.exports.createPlatformStorage = createPlatformStorage;
module.exports.readViewport = readViewport;
module.exports.normalizeTouchPoint = normalizeTouchPoint;
module.exports.loadPlatformImage = loadPlatformImage;
module.exports.configureCanvas = configureCanvas;
module.exports.installRoundRect = installRoundRect;
module.exports.isSidebarLaunchOptions = isSidebarLaunchOptions;
module.exports.checkSidebarSupport = checkSidebarSupport;
module.exports.navigateToSidebar = navigateToSidebar;
module.exports.scheduleFrame = scheduleFrame;
