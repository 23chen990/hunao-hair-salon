import assert from 'node:assert/strict';
import { existsSync } from 'node:fs';
import test from 'node:test';

test('the deterministic simulation module exists', () => {
  assert.equal(existsSync(new URL('../src/sim.mjs', import.meta.url)), true);
});

