import test from 'node:test';
import assert from 'node:assert/strict';

async function optionalModule(path) {
  try {
    return await import(path);
  } catch {
    return null;
  }
}

const input = await optionalModule('../src/input.js');
const layout = await optionalModule('../src/layout.js');

test('snap regions provide at least 24 px ordinary release tolerance', () => {
  assert.ok(input, 'input implementation is missing');
  const spacing = 54;
  assert.equal(input.snapDragToSteps(26.9, spacing), 0);
  assert.equal(input.snapDragToSteps(27.1, spacing), 1);
  assert.equal(input.snapDragToSteps(80.9, spacing), 1);
  assert.equal(input.snapDragToSteps(81.1, spacing), 2);
  assert.ok(input.snapCatchWidth(spacing) >= 48);
});

test('layer hit targets and controls stay safe at both required portrait viewports', () => {
  assert.ok(layout, 'layout implementation is missing');
  for (const [width, height] of [[390, 844], [430, 932]]) {
    for (const layerCount of [3, 4]) {
      const value = layout.computeLayout(width, height, layerCount);
      assert.ok(value.board.x >= 16 && value.board.x + value.board.width <= width - 16);
      assert.ok(value.controls.y + value.controls.height <= height - value.safeBottom);
      assert.ok(value.layers.every((layer) => layer.hitHeight >= 54));
      for (let i = 1; i < value.layers.length; i += 1) {
        assert.ok(value.layers[i].hitTop >= value.layers[i - 1].hitTop + value.layers[i - 1].hitHeight);
      }
    }
  }
});
