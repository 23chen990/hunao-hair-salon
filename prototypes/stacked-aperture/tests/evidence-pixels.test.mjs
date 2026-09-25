import test from 'node:test';
import assert from 'node:assert/strict';
import { createRequire } from 'node:module';
import { resolve } from 'node:path';

const require = createRequire(import.meta.url);
const { createCanvas, loadImage } = require('/Users/kker/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/@napi-rs/canvas');

test('model evidence header contains no offscreen-composite arc artifact', async () => {
  const image = await loadImage(resolve(import.meta.dirname, '..', 'evidence', 'stacked-aperture-model-frame-390x844.png'));
  const canvas = createCanvas(image.width, image.height);
  const context = canvas.getContext('2d');
  context.drawImage(image, 0, 0);
  const [red, green, blue] = context.getImageData(280, 100, 1, 1).data;
  assert.ok(red < 80 && green < 90 && blue < 110, `unexpected bright artifact pixel: ${red},${green},${blue}`);
});
