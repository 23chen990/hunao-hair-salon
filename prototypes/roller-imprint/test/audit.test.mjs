import test from 'node:test';
import assert from 'node:assert/strict';

let audit = {};
try {
  audit = await import('../src/audit.mjs');
} catch {
  // Expected RED until the evidence compositor exists.
}

function need(name) {
  assert.equal(typeof audit[name], 'function', `${name} implementation is missing`);
  return audit[name];
}

test('machine audit reports raw counts from actual model runs', () => {
  const runAudit = need('runAudit');
  const result = runAudit({ seedCount: 16, randomTrials: 320 });
  assert.equal(result.configuration.seedCount, 16);
  assert.equal(result.reachability.reachable, 16);
  assert.equal(result.reachability.zeroActionComplete, 0);
  assert.equal(result.determinism.fpsSampleMatches, 16);
  assert.equal(result.determinism.undoHashMatches, 16);
  assert.equal(result.determinism.replayMatches, 16);
  assert.equal(result.randomSequences.trials, 320);
  assert.equal(result.alternatives.exactSuboptimal, 16);
  assert.equal(result.noAd.completed, 16);
  assert.equal(result.adFreeze.hashUnchanged, 16);
});

test('browser status is explicit and cannot silently become a mechanics failure', () => {
  const runAudit = need('runAudit');
  const result = runAudit({ seedCount: 8, randomTrials: 80 });
  assert.ok(['PASS', 'ENVIRONMENT_UNVERIFIED'].includes(result.browser.status));
  assert.notEqual(result.browser.status, 'KILL');
});
