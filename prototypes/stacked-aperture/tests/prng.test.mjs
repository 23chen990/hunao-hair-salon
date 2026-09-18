import test from 'node:test';
import assert from 'node:assert/strict';

let prng = null;
try {
  prng = await import('../src/prng.js');
} catch {
  // The assertion below is the intentional RED while the implementation is absent.
}

test('seeded RNG is deterministic and produces bounded integer choices', () => {
  assert.ok(prng, 'PRNG implementation is missing');
  const first = prng.createRng('same-seed');
  const second = prng.createRng('same-seed');
  const a = Array.from({ length: 20 }, () => [first.next(), first.int(-3, 3)]);
  const b = Array.from({ length: 20 }, () => [second.next(), second.int(-3, 3)]);
  assert.deepEqual(a, b);
  assert.ok(a.every(([unit, integer]) => unit >= 0 && unit < 1 && integer >= -3 && integer <= 3));
});
