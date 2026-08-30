---
name: salon-bug-fix
description: Reproduce, minimally fix, and verify Hair Salon Demo bugs from screenshots, recordings, Chinese descriptions, or copied debug-state JSON. Use for an approved bug-fix task; do not use for feature requests, redesign, or speculative refactoring.
---

# Salon Bug Fix

Fix one reproducible defect with the smallest coherent change while preserving the approved Demo.

## Inputs and outputs

Input: screenshot/recording/text, copied debug state when available, expected behavior, and reproduction frequency.

Output: reproducible steps, category/root cause, failing regression check, minimal fix, affected and adjacent-flow results, before/after screenshots, and a plain-Chinese report. Read [references/triage.md](references/triage.md) for project-specific evidence routing.

## Workflow

1. Read all supplied evidence. Capture scene, character position/state, service state, target station, viewport and build version from debug JSON.
2. Reproduce without changing code. Prefer a fixed seed or fixed acceptance scenario; record exact steps and before screenshot.
3. Classify the primary cause as collision, anchor, sorting, animation, state machine, resource, or layout. Do not combine unrelated cleanup.
4. Before implementation, add the smallest regression test/check and observe the expected failure.
5. Make one coherent fix. Do not change approved gameplay, art, direction or room layout to hide the defect.
6. Run `scripts/verify.sh --filter <test>` for the regression. Also run the affected runtime/browser flow and at least one adjacent flow proportional to risk.
7. Capture an after screenshot at the same viewport and state. Compare it with the before evidence; report limitations if exact state reproduction is impossible.

## Completion

Complete only when reproduction is documented, the regression check failed before and passes after, the affected flow passes, adjacent behavior is checked, console errors are empty, and before/after evidence plus a Chinese explanation are delivered. If the issue cannot be reproduced, report that; do not guess-fix.

## Do not trigger

Do not use for new features, balancing, art redesign, engine migration, mass refactoring, or unconfirmed visual preferences.

## Example

“Use `$salon-bug-fix` for this recording: the stylist reaches the right-wall chair but faces away. Debug state and expected facing are attached.”
