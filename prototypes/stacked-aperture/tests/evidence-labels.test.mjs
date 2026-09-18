import test from 'node:test';
import assert from 'node:assert/strict';

let labels = null;
try {
  labels = await import('../src/evidence-labels.js');
} catch {
  // Intentional RED for the cross-renderer font fallback fix.
}

test('headless evidence labels stay ASCII-safe across Canvas font backends', () => {
  assert.ok(labels, 'evidence label implementation is missing');
  for (const [name, value] of Object.entries(labels.EVIDENCE_LABELS)) {
    assert.match(value, /^[\x20-\x7E]+$/, `${name} must avoid backend-dependent glyph fallback`);
  }
});
