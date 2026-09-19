---
name: salon-visual-qa
description: Build and inspect the approved Hair Salon scenes in a real Chromium browser at the target landscape viewport, producing screenshots and a structured visual issue report. Use for visual QA or regression comparison; do not use to redesign the approved art direction.
---

# Salon Visual QA

Collect reproducible visual evidence from the current Demo and compare it with the product owner’s approved reference when one exists.

## Inputs and outputs

Input: scene (`demo`, `asset-lab`, `candidate` or `reference`), optional stable asset IDs, optional approved screenshot/reference, and any named focus area. Default target viewport is 844×390. `HairSalonDemo` 是唯一正式 Visual Integration Scene。Asset Lab 与旧的独立 Reference Scene 只保留为技术检查环境，不能批准最终游戏构图。

For each production 2.5D candidate, output at least: Source 100% preview, Asset Lab Inspect, Inspect debug overlay, Asset Lab Context and Unity 100% Pixel view. Candidate mode additionally captures the asset beside characters during the real service flow. Output browser canvas size/devicePixelRatio, runtime texture dimensions, console/resource results, `visual-qa-report.json`, and a concise Chinese issue list classified by severity and evidence. Read [references/checklist.md](references/checklist.md) before judging images.

## Workflow

1. Read the project context and confirm the approved reference. If none exists, mark visual comparisons as `baseline-missing`; do not invent a target.
2. Run `scripts/run.sh demo`, `scripts/run.sh asset-lab`, `scripts/run.sh candidate`, or `scripts/run.sh reference`. Asset-lab mode automatically selects production candidates and generates Source/Inspect/Context/Pixel evidence; it must reject any WebGL runtime texture size that differs from Manifest source dimensions. Demo mode builds the real playable scene, runs its browser regression and captures named integration areas. For the wash-area slice it must produce A) approved-reference crop, B) formal Demo Before, C) formal Demo After and D) close player/station context from the same `HairSalonDemo` build.
   Asset Lab Context and Reference Scene are only engineering aids for PNG clarity, pivot, collision, anchor, sorting, texture and single-asset inspection. They cannot be the sole basis for final scale, layout or scene-integration approval. Final Context QA must happen in `HairSalonDemo` without changing the approved service state machine or silently replacing the release default.
3. Inspect screenshots directly. Compare Source versus Inspect at the same viewport before judging blur. Check console and missing resources first, then clarity/alpha edge, proportion, position, direction, shadow, occlusion/depth, character size, viewport crop and safe area.
   Treat an approved effect image as visual-direction and composition evidence, not a pixel-perfect 3D camera reconstruction. Compare screen-relative player weight, furniture presence, room framing, texture density and scene activity; separate direct observations from inferences about camera parameters.
4. Record each issue with scene, viewport, category, severity, observed evidence, expected reference and a minimal suggested correction. Separate fact from inference.
5. Do not edit the game unless the user separately asks for a fix.

## Completion

Complete only when the requested scene launched in Chromium, required Source/Inspect/Context evidence is nonblank, canvas/devicePixelRatio and runtime texture dimensions are recorded, console/resource status is recorded, every checklist category has a result, and the report links evidence. “Matches approved target” is allowed only with an actual approved reference.

## Do not trigger

Do not use for gameplay validation, art generation, broad UI redesign, engine changes, or subjective approval without a product-owner reference.

## Example

“Use `$salon-visual-qa` on the current main scene at the target landscape size and report any overlap, shadow or safe-area regressions against this approved screenshot.”
