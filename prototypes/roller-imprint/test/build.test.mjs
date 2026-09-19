import test from 'node:test';
import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import { spawnSync } from 'node:child_process';

test('build emits a runnable network-free browser entry', () => {
  const result = spawnSync(process.execPath, ['tools/build.mjs'], {
    cwd: new URL('..', import.meta.url),
    encoding: 'utf8',
  });
  assert.equal(result.status, 0, `${result.stdout}\n${result.stderr}`);
  const root = new URL('..', import.meta.url);
  for (const relative of ['dist/index.html', 'dist/styles.css', 'dist/src/app.mjs']) {
    assert.ok(existsSync(new URL(relative, root)), `${relative} is missing`);
  }
});
