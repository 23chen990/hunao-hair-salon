import test from 'node:test';
import assert from 'node:assert/strict';

let evidence = null;
try {
  evidence = await import('../src/evidence-frame.js');
} catch {
  // Intentional RED until the model-derived evidence frame exists.
}

test('evidence frame is an actual near-solution gameplay state with a real first blocker', () => {
  assert.ok(evidence, 'evidence frame implementation is missing');
  const frame = evidence.createEvidenceFrame(1_001);
  assert.equal(frame.state.status, 'blocked');
  assert.ok(Number.isInteger(frame.state.lastDrop.blockerIndex));
  assert.equal(frame.appliedMoves.length, frame.solution.distance - 1);
  assert.equal(frame.state.moveCount, frame.appliedMoves.length);
  assert.equal(frame.state.dropCount, 1);
});
