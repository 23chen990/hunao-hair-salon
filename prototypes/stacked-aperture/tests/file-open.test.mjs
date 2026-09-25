import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

const root = resolve(import.meta.dirname, '..');

test('index can be opened directly from file:// without ES module loading', async () => {
  const html = await readFile(resolve(root, 'index.html'), 'utf8');
  let bundle = null;
  try {
    bundle = await readFile(resolve(root, 'dist', 'app.bundle.js'), 'utf8');
  } catch {
    // Intentional RED until the direct-open bundle is built.
  }

  assert.doesNotMatch(html, /<script[^>]+type=["']module["']/i, 'file:// entry must not depend on module loading');
  assert.match(html, /<script[^>]+src=["']\.\/dist\/app\.bundle\.js["']/i);
  assert.ok(bundle, 'direct-open bundle is missing');
  assert.match(bundle, /^\(\(\) => \{/);
  assert.doesNotMatch(bundle, /^\s*(?:import|export)\s/m, 'classic bundle must contain no ESM syntax');
});
