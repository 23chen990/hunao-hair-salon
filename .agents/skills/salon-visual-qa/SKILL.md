---
name: salon-visual-qa
description: Build and inspect the approved Hair Salon scenes in a real Chromium browser at the target landscape viewport, producing screenshots and a structured visual issue report. Use for visual QA or regression comparison; do not use to redesign the approved art direction.
---

# Salon Visual QA

Collect reproducible visual evidence from the current Demo and compare it with the product owner’s approved reference when one exists.

## Inputs and outputs

Input: scene (`demo` or `asset-lab`), optional approved screenshot/reference, and any named focus area. Default target viewport is 844×390.

Output: browser screenshots, console/resource results, `visual-qa-report.json`, and a concise Chinese issue list classified by severity and evidence. Read [references/checklist.md](references/checklist.md) before judging images.

## Workflow

1. Read the project context and confirm the approved reference. If none exists, mark visual comparisons as `baseline-missing`; do not invent a target.
2. Run `scripts/run.sh demo` for the formal scene or `scripts/run.sh asset-lab` for the fixed asset scene. This validates resources, builds WebGL, launches real Chromium, waits for assets/animation, and captures 844×390 evidence.
3. Inspect screenshots directly. Check console and missing resources first, then proportion, position, direction, shadow, occlusion/depth, character size, viewport crop and safe area.
4. Record each issue with scene, viewport, category, severity, observed evidence, expected reference and a minimal suggested correction. Separate fact from inference.
5. Do not edit the game unless the user separately asks for a fix.

## Completion

Complete only when the requested scene launched in Chromium, screenshots are nonblank, console/resource status is recorded, every checklist category has a result, and the report links evidence. “Matches approved target” is allowed only with an actual approved reference.

## Do not trigger

Do not use for gameplay validation, art generation, broad UI redesign, engine changes, or subjective approval without a product-owner reference.

## Example

“Use `$salon-visual-qa` on the current main scene at the target landscape size and report any overlap, shadow or safe-area regressions against this approved screenshot.”
