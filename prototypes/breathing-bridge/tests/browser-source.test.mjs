import assert from 'node:assert/strict';
import { existsSync, readFileSync } from 'node:fs';
import test from 'node:test';

const root = new URL('../', import.meta.url);

test('the dependency-free browser source contains a canvas and free restart control', () => {
  for (const file of ['index.html', 'styles.css', 'src/browser.mjs']) {
    assert.equal(existsSync(new URL(file, root)), true, `${file} must exist`);
  }
  const html = readFileSync(new URL('index.html', root), 'utf8');
  assert.match(html, /<canvas[^>]+id="bridge"/);
  assert.match(html, /id="restart"/);
  assert.match(html, /id="next-level"/);
  const browser = readFileSync(new URL('src/browser.mjs', root), 'utf8');
  assert.match(browser, /growing-bridge\.mjs/);
});

test('audit, dependency-free build, and browser smoke entrypoints exist', () => {
  for (const file of ['scripts/audit.mjs', 'scripts/build.mjs', 'scripts/smoke.mjs']) {
    assert.equal(existsSync(new URL(file, root)), true, `${file} must exist`);
  }
});
