import assert from 'node:assert/strict';
import { test } from 'node:test';
import {
  checkSidebarSupport,
  createPlatformStorage,
  isSidebarLaunchOptions,
  loadPlatformImage,
  normalizeTouchPoint,
  readViewport,
  scheduleFrame,
} from '../src/platform.ts';

test('storage adapter preserves the Storage getItem/setItem contract', () => {
  const values = new Map();
  const tt = {
    getStorageSync: (key) => values.get(key),
    setStorageSync: (key, value) => values.set(key, value),
  };
  const storage = createPlatformStorage(tt);

  assert.equal(storage.getItem('events'), null);
  storage.setItem('events', '[1]');
  assert.equal(storage.getItem('events'), '[1]');
});

test('readViewport clamps pixel ratio and supplies safe phone defaults', () => {
  assert.deepEqual(readViewport({
    getSystemInfoSync: () => ({ windowWidth: 430, windowHeight: 932, pixelRatio: 3 }),
  }), { width: 430, height: 932, pixelRatio: 2 });

  assert.deepEqual(readViewport({ getSystemInfoSync: () => ({}) }), {
    width: 390,
    height: 844,
    pixelRatio: 1,
  });
});

test('normalizeTouchPoint accepts changedTouches and touches', () => {
  assert.deepEqual(normalizeTouchPoint({ changedTouches: [{ clientX: 12, clientY: 34 }] }), { x: 12, y: 34 });
  assert.deepEqual(normalizeTouchPoint({ touches: [{ x: 56, y: 78 }] }), { x: 56, y: 78 });
  assert.equal(normalizeTouchPoint({ touches: [] }), null);
});

test('sidebar helpers recognize the official launch fields and capability result', async () => {
  assert.equal(isSidebarLaunchOptions({ launch_from: 'homepage', location: 'sidebar_card' }), true);
  assert.equal(isSidebarLaunchOptions({ launch_from: 'homepage', location: 'feed' }), false);

  const supported = await checkSidebarSupport({
    checkScene: ({ scene, success }) => success({ isExist: scene === 'sidebar' }),
  });
  assert.equal(supported, true);
  assert.equal(await checkSidebarSupport({}), false);
});

test('loadPlatformImage uses the canvas image factory and reports load failures', async () => {
  const assigned = [];
  const image = {
    onload: null,
    onerror: null,
    set src(value) {
      assigned.push(value);
      queueMicrotask(() => this.onload());
    },
  };
  const loaded = await loadPlatformImage({ createImage: () => image }, {}, 'assets/board.png');
  assert.equal(loaded, image);
  assert.deepEqual(assigned, ['assets/board.png']);

  const broken = {
    onload: null,
    onerror: null,
    set src(_value) { queueMicrotask(() => this.onerror()); },
  };
  await assert.rejects(
    loadPlatformImage({ createImage: () => broken }, {}, 'assets/missing.png'),
    /Failed to load assets\/missing\.png/,
  );
});

test('scheduleFrame normalizes platform frame timestamps to the Date.now clock', () => {
  let platformCallback = null;
  let received = 0;
  scheduleFrame({ requestAnimationFrame: (callback) => { platformCallback = callback; } }, (now) => { received = now; });
  platformCallback(17);
  assert.ok(received > 1_000_000_000_000, `received non-epoch timestamp ${received}`);
});
