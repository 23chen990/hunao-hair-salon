import assert from 'node:assert/strict';
import { access, readFile, readdir, stat } from 'node:fs/promises';
import { readFileSync } from 'node:fs';
import { join, resolve } from 'node:path';
import { test } from 'node:test';
import vm from 'node:vm';
import { transformToCommonJs } from '../tools/build-lib.mjs';
import { buildDouyinPackage } from '../tools/build.mjs';

const platformRoot = resolve(import.meta.dirname, '..');
const outputRoot = join(platformRoot, 'dist');

test('transformToCommonJs rewrites local TypeScript imports and named exports', () => {
  const source = `
    import { alpha, beta as renamed } from './dependency.ts';
    import type { Hidden } from './types.ts';
    export const value: number = alpha + renamed;
    export function read(): number { return value; }
    export class Counter { current = 0; }
  `;

  const output = transformToCommonJs(source, (specifier) => `./${specifier.split('/').at(-1).replace('.ts', '.js')}`);

  assert.match(output, /const \{ alpha, beta: renamed \} = require\('\.\/dependency\.js'\);/);
  assert.match(output, /module\.exports\.value = value;/);
  assert.match(output, /module\.exports\.read = read;/);
  assert.match(output, /module\.exports\.Counter = Counter;/);
  assert.doesNotMatch(output, /\bimport\b|\bexport\b|\.ts['"]/);
});

test('build creates a Douyin developer-tools package without browser globals', async () => {
  await buildDouyinPackage();

  for (const relativePath of ['game.js', 'game.json', 'project.config.json', 'js/launch.js', 'js/douyin-app.js']) {
    await access(join(outputRoot, relativePath));
  }

  const gameConfig = JSON.parse(await readFile(join(outputRoot, 'game.json'), 'utf8'));
  assert.equal(gameConfig.deviceOrientation, 'portrait');
  assert.equal(gameConfig.showStatusBar, false);

  const projectConfig = JSON.parse(await readFile(join(outputRoot, 'project.config.json'), 'utf8'));
  assert.equal(projectConfig.appid, 'REPLACE_WITH_DOUYIN_APPID');

  const gameEntry = await readFile(join(outputRoot, 'game.js'), 'utf8');
  assert.equal(gameEntry.trim(), [
    "require('./js/launch.js');",
    "require('./js/douyin-app.js');",
  ].join('\n'));

  const scripts = await readdir(join(outputRoot, 'js'));
  assert.ok(scripts.length >= 10);
  for (const script of scripts) {
    const content = await readFile(join(outputRoot, 'js', script), 'utf8');
    assert.doesNotMatch(content, /\b(?:document|window|localStorage|HTMLImageElement)\b/);
    assert.doesNotMatch(content, /(?:^|\n)\s*(?:import|export)\s/m);
    assert.doesNotMatch(content, /\.ts['"]/);
  }

  let packageBytes = 0;
  async function addDirectory(directory) {
    for (const entry of await readdir(directory, { withFileTypes: true })) {
      const path = join(directory, entry.name);
      if (entry.isDirectory()) await addDirectory(path);
      else packageBytes += (await stat(path)).size;
    }
  }
  await addDirectory(outputRoot);
  assert.ok(packageBytes < 20 * 1024 * 1024, `package is ${(packageBytes / 1024 / 1024).toFixed(2)} MiB`);
});

test('built game boots in a fake tt runtime and schedules its first playable frame', async () => {
  await buildDouyinPackage();
  const frameCallbacks = [];
  const showCallbacks = [];
  const touchCallbacks = [];
  const errors = [];
  const values = new Map();
  const context = new Proxy({}, {
    get(target, property) {
      if (!(property in target)) target[property] = () => undefined;
      return target[property];
    },
    set(target, property, value) {
      target[property] = value;
      return true;
    },
  });
  const canvas = {
    getContext: () => context,
    requestAnimationFrame: (callback) => frameCallbacks.push(callback),
    createImage: () => ({
      onload: null,
      onerror: null,
      set src(_value) { queueMicrotask(() => this.onload()); },
    }),
  };
  const sandbox = {
    tt: {
      createCanvas: () => canvas,
      getSystemInfoSync: () => ({ windowWidth: 390, windowHeight: 844, pixelRatio: 2 }),
      getLaunchOptionsSync: () => ({ scene: 'normal' }),
      getStorageSync: (key) => values.get(key),
      setStorageSync: (key, value) => values.set(key, value),
      checkScene: ({ success }) => success({ isExist: true }),
      onShow: (callback) => showCallbacks.push(callback),
      onHide: () => undefined,
      onTouchStart: (callback) => touchCallbacks.push(callback),
      showToast: () => undefined,
    },
    console: {
      log: () => undefined,
      warn: () => undefined,
      error: (...args) => errors.push(args),
    },
    Date,
    Error,
    JSON,
    Map,
    Math,
    Number,
    Object,
    Promise,
    Set,
    String,
    queueMicrotask,
    setTimeout,
  };
  const cache = new Map();
  function loadCommonJs(path) {
    const normalized = resolve(path);
    if (cache.has(normalized)) return cache.get(normalized).exports;
    const module = { exports: {} };
    cache.set(normalized, module);
    const localRequire = (specifier) => loadCommonJs(resolve(normalized, '..', specifier));
    const wrapper = vm.runInNewContext(
      `(function (require, module, exports) {\n${readFileSync(normalized, 'utf8')}\n})`,
      sandbox,
      { filename: normalized },
    );
    wrapper(localRequire, module, module.exports);
    return module.exports;
  }

  loadCommonJs(join(outputRoot, 'game.js'));
  assert.ok(showCallbacks.length >= 1, 'launch onShow listener must register synchronously');
  for (let index = 0; index < 8; index += 1) await new Promise((resolveTick) => setImmediate(resolveTick));

  assert.equal(errors.length, 0);
  assert.equal(touchCallbacks.length, 1);
  assert.ok(showCallbacks.length >= 2, 'runtime recovery listener should also be registered');
  assert.equal(frameCallbacks.length, 1);

  const nextFrame = frameCallbacks.shift();
  nextFrame(Date.now() + 16);
  assert.equal(frameCallbacks.length, 1);
  assert.equal(errors.length, 0);
});
