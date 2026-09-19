# RED / GREEN evidence

Every behavior group is introduced by a test that fails because its implementation is absent, then receives only the implementation needed to turn that group green.

## Cycle 1 — discrete roll model, exact undo, restart, and replay

RED command:

```text
$ npm run test:core
exit 1
✖ drag distance maps to discrete turns and clamps to the 12-column tape
✖ forward and backward rolls contact deterministic columns and faces
✖ a roll stamps raised face bits and reports wrong and missing cells
✖ whole-pass undo restores the exact prior playable-state hash
✖ free restart restores the seed initial state without an ad branch
✖ serialized event replay reproduces every frame hash deterministically
✖ 30/60/120fps pointer sampling yields identical stamped cells
ℹ tests 7
ℹ pass 0
ℹ fail 7
AssertionError [ERR_ASSERTION]: actionFromPointerSamples implementation is missing
+ actual - expected
+ 'undefined'
- 'function'
```

The other six failures had the same expected cause: the named model function was absent. This is an assertion failure from missing behavior, not a syntax/configuration error.

GREEN command:

```text
$ npm run test:core
exit 0
✔ drag distance maps to discrete turns and clamps to the 12-column tape
✔ forward and backward rolls contact deterministic columns and faces
✔ a roll stamps raised face bits and reports wrong and missing cells
✔ whole-pass undo restores the exact prior playable-state hash
✔ free restart restores the seed initial state without an ad branch
✔ serialized event replay reproduces every frame hash deterministically
✔ 30/60/120fps pointer sampling yields identical stamped cells
ℹ tests 7
ℹ pass 7
ℹ fail 0
```

Status: GREEN. Minimal implementation is `src/model.mjs`; no renderer is involved in rule truth.

## Cycle 2 — generated targets, shortest solver, suboptimal path, and perturbations

RED command:

```text
$ node --test test/solver.test.mjs
exit 1
✖ legal action space contains each face, direction, and discrete stop
✖ generated targets are non-empty, exact, seeded, and shortest in 2–5 passes
✖ every generated acceptance level exposes a legal, genuinely longer exact solution
✖ neighbor viability measures face, direction, and stop perturbations against real targets
✖ uniform random legal roll sequences are sampled by the actual model
ℹ tests 5
ℹ pass 0
ℹ fail 5
AssertionError [ERR_ASSERTION]: allLegalActions implementation is missing
+ actual - expected
+ 'undefined'
- 'function'
```

Status: RED confirmed. All five assertions failed for the intended absent solver behavior.

GREEN command:

```text
$ node --test test/solver.test.mjs
exit 0
✔ legal action space contains each face, direction, and discrete stop
✔ generated targets are non-empty, exact, seeded, and shortest in 2–5 passes
✔ every generated acceptance level exposes a legal, genuinely longer exact solution
✔ neighbor viability measures face, direction, and stop perturbations against real targets
✔ uniform random legal roll sequences are sampled by the actual model
ℹ tests 5
ℹ pass 5
ℹ fail 0
```

Status: GREEN. The target is built from actual legal masks and accepted only after the actual breadth-first solver proves the requested minimum depth.

## Cycle 3 — local IAA eligibility proxy, no-ad path, and simulated freeze

RED command:

```text
$ node --test test/iaa.test.mjs
exit 1
✖ local ad policy defaults off, forbids requests, and caps voluntary entries at two
✖ simulated ad freezes rolls, tape, and clock then restores the exact hash
✖ no-ad regression completes and freely restarts every sampled seed
✖ round-length and voluntary eligibility proxies stay inside declared bands
✖ 7–11 minute sessions have one or two settlement-only points after three ad-free rounds
ℹ tests 5
ℹ pass 0
ℹ fail 5
AssertionError [ERR_ASSERTION]: createLocalAdPolicy implementation is missing
+ actual - expected
+ 'undefined'
- 'function'
```

Status: RED confirmed. Failures are the intended absent local eligibility behavior; there is no SDK or request code to misconfigure.

GREEN command:

```text
$ node --test test/iaa.test.mjs
exit 0
✔ local ad policy defaults off, forbids requests, and caps voluntary entries at two
✔ simulated ad freezes rolls, tape, and clock then restores the exact hash
✔ no-ad regression completes and freely restarts every sampled seed
✔ round-length and voluntary eligibility proxies stay inside declared bands
✔ 7–11 minute sessions have one or two settlement-only points after three ad-free rounds
ℹ tests 5
ℹ pass 5
ℹ fail 0
```

Status: GREEN. Eligibility is deterministic local simulation over actual board errors. It is explicitly not an SDK, request, viewing-rate, fill-rate, device, or revenue measurement.

## Cycle 4 — responsive Canvas contract, build, and real PointerEvent smoke path

Status: RED tests and smoke runner written; responsive UI/build implementation is intentionally absent.

RED commands:

```text
$ node --test test/ui.test.mjs test/build.test.mjs
exit 1
✖ build emits a runnable network-free browser entry
✖ both required portrait viewports fit two 8×12 boards and roll handles
✖ all declared touch targets are at least 44 CSS pixels
✖ roll-start hit testing distinguishes forward and backward handles
ℹ tests 4
ℹ pass 0
ℹ fail 4
Error: Cannot find module '.../tools/build.mjs'
AssertionError [ERR_ASSERTION]: computeLayout implementation is missing

$ npm run smoke
exit 1
AssertionError [ERR_ASSERTION]: build failed
Error: Cannot find module '.../tools/build.mjs'
```

Status: RED confirmed before UI/build code. Smoke stopped at the deliberately absent build, before any browser/environment interpretation.

## Cycle 5 — one focused solver performance repair

Pre-audit diagnostic on 100 cold seeds (generation + another strict solve):

```json
{
  "p50": 1.219958,
  "p95": 149.772209,
  "max": 5145.353,
  "robust": 78,
  "depths": { "2": 25, "3": 25, "4": 25, "5": 25 },
  "maxAttempts": 15,
  "total": 9220.11483
}
```

Status: performance regression test written before changing the search implementation.

RED command:

```text
$ node --test test/solver-performance.test.mjs
exit 1
✖ cold generation plus strict shortest solving has development-machine p95 <=30ms (45904.662ms)
AssertionError [ERR_ASSERTION]: development-machine proxy p95 494.029ms exceeds 30ms
ℹ tests 1
ℹ pass 0
ℹ fail 1
```

Status: RED confirmed. This is the one allowed focused-repair target.

GREEN commands:

```text
$ node --test test/solver.test.mjs
exit 0
ℹ tests 5
ℹ pass 5
ℹ fail 0

$ node --test test/solver-performance.test.mjs
exit 0
✔ cold generation plus strict shortest solving has development-machine p95 <=30ms
ℹ tests 1
ℹ pass 1
ℹ fail 0
```

Focused change: target-subset action dominance removal plus iterative-deepening set-cover search that branches on the rarest uncovered bit. Exact shortest depth and all earlier solver tests stayed green; targets and thresholds were not relaxed.

Status: GREEN after one focused repair.

## Cycle 4 GREEN / browser environment note

```text
$ node --test test/ui.test.mjs test/build.test.mjs
exit 0
ℹ tests 4
ℹ pass 4
ℹ fail 0

$ npm run smoke              # first, sandboxed as required
exit 1
Error: listen EPERM: operation not permitted 127.0.0.1

$ npm run smoke              # one normal permission request
REJECTED by host policy: security policy is `never`
```

Responsive geometry and build are GREEN. The real Chromium mouse/touch run is `ENVIRONMENT_UNVERIFIED`, not a mechanic KILL; no second request or policy workaround was attempted.

## Cycle 6 — machine-audit compositor

Status: RED tests written; the audit compositor and JSON artifact writer are absent.

RED command:

```text
$ node --test test/audit.test.mjs
exit 1
✖ machine audit reports raw counts from actual model runs
✖ browser status is explicit and cannot silently become a mechanics failure
ℹ tests 2
ℹ pass 0
ℹ fail 2
AssertionError [ERR_ASSERTION]: runAudit implementation is missing
```

Status: RED confirmed for the absent evidence compositor.

GREEN command:

```text
$ node --test test/audit.test.mjs
exit 0
✔ machine audit reports raw counts from actual model runs
✔ browser status is explicit and cannot silently become a mechanics failure
ℹ tests 2
ℹ pass 2
ℹ fail 0
```

Status: GREEN. `tools/audit.mjs` writes raw model-derived evidence to `artifacts/machine-audit.json` and exits nonzero only when a non-browser implementation gate fails.

## Final regression

```text
$ npm test && npm run build
exit 0
ℹ tests 24
ℹ pass 24
ℹ fail 0
ℹ duration_ms 167.210709
built /Users/kker/Documents/ChatGPT/game2/prototypes/roller-imprint/dist

$ npm run audit
exit 0
implementationGatesPass: true
browser.status: ENVIRONMENT_UNVERIFIED
```

No RED was erased or overwritten; earlier failing commands remain above as evidence that each behavior group was first absent or genuinely over budget.
