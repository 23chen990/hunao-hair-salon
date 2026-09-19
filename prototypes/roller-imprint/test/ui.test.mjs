import test from 'node:test';
import assert from 'node:assert/strict';

let ui = {};
try {
  ui = await import('../src/ui-contract.mjs');
} catch {
  // Expected RED until responsive geometry is implemented.
}

function need(name) {
  assert.equal(typeof ui[name], 'function', `${name} implementation is missing`);
  return ui[name];
}

test('both required portrait viewports fit two 8×12 boards and roll handles', () => {
  const computeLayout = need('computeLayout');
  for (const [width, height] of [[390, 844], [430, 932]]) {
    const layout = computeLayout(width, height);
    assert.equal(layout.targetBoard.columns, 12);
    assert.equal(layout.targetBoard.rows, 8);
    assert.equal(layout.inkBoard.columns, 12);
    assert.equal(layout.inkBoard.rows, 8);
    assert.ok(layout.targetBoard.x >= 0 && layout.targetBoard.right <= width);
    assert.ok(layout.inkBoard.x >= 0 && layout.inkBoard.right <= width);
    assert.ok(layout.inkBoard.bottom <= layout.canvasHeight);
    assert.ok(layout.leftHandle.x < layout.rightHandle.x);
  }
});

test('all declared touch targets are at least 44 CSS pixels', () => {
  const computeLayout = need('computeLayout');
  for (const [width, height] of [[390, 844], [430, 932]]) {
    const layout = computeLayout(width, height);
    for (const target of Object.values(layout.touchTargets)) {
      assert.ok(target.width >= 44, `${width} target width ${target.width}`);
      assert.ok(target.height >= 44, `${height} target height ${target.height}`);
    }
  }
});

test('roll-start hit testing distinguishes forward and backward handles', () => {
  const computeLayout = need('computeLayout');
  const hitTestRollStart = need('hitTestRollStart');
  const layout = computeLayout(390, 844);
  assert.equal(hitTestRollStart(layout, layout.leftHandle.x, layout.leftHandle.y), 1);
  assert.equal(hitTestRollStart(layout, layout.rightHandle.x, layout.rightHandle.y), -1);
  assert.equal(hitTestRollStart(layout, layout.targetBoard.x + 100, 10), 0);
});
