---
name: salon-asset-ingest
description: Validate and ingest product-approved PNG artwork from assets/inbox into the Hair Salon Unity asset manifest, preview lab, and landscape screenshot evidence. Use for confirmed asset intake or replacement candidates; do not use to invent, redesign, or approve artwork.
---

# Salon Asset Ingest

Use only after the product owner identifies a PNG as approved for technical intake. The skill owns deterministic intake, not visual creation or product approval.

## Inputs

- inbox file name under `assets/inbox/`
- new stable kebab-case asset ID and one type: `furniture`, `character`, `ui-sprite`, `effect`, `prop`
- approved directions, category, pivot, footprint, collision, required anchors, shadow and sorting values

For production transparent 2.5D rendered furniture, the pipeline owns `importProfile=production-2.5d-rendered` and derives the initial `DesiredWorldSize` from footprint plus effective visible bounds. PNG pixel dimensions are evidence for clarity, never furniture world scale.

If configuration is missing, stop after inspection and report what the product owner must confirm. Never infer a new layout or orientation from taste.

## Workflow

1. Read `PROJECT_CONTEXT.md`, `STATUS.md`, `AGENTS.md` and [references/asset-contract.md](references/asset-contract.md).
2. Inspect without mutation: `node tools/asset-pipeline.mjs inspect --file assets/inbox/<file>`.
3. Reject damaged/unsupported images, abnormal whitespace, missing transparency when the approved use requires it, unstable IDs, or any attempt to overwrite an `approved` entry. Treat visible pixels touching an edge as a blocking review warning because the script cannot determine intent.
4. Run `scripts/ingest.sh --file <file> --id <id> --type <type> --config <config.json>` only when inputs are confirmed. The script performs naming, copy/archive, preview, Manifest update, applies the production 2.5D Unity texture policy, verifies that Unity kept the source pixel dimensions, validates, builds the asset lab and captures Chromium evidence.
   For an already-ingested candidate whose PNG must not be copied or replaced, run `scripts/ingest.sh --revalidate --id <id>`; this deliberately skips import and reruns the same policy, Manifest, lab-build and browser-evidence gates.
5. Review Source/100% Pixel, Inspect and Context evidence. Inspect must auto-frame from bounds; Context must retain game-world scale with the player and one-unit tiles as references. Verify pivot, footprint, collision, anchors, shadow and sorting; do not alter the source art to make a failed configuration appear correct.
6. Report imported paths, Manifest status, screenshots, warnings and rollback instructions.

## Completion

Complete only when the intake script succeeds, the Manifest validates, Unity and WebGL runtime texture sizes equal the source dimensions, the asset appears in Inspect and Context modes, Source/Pixel evidence exists, target-direction checks are appropriate, screenshots are nonblank, and browser console errors are empty. A candidate is not “approved” until the product owner approves the visible result.

Rollback: before a successful receipt, the script must leave no partial import. After success, revert the new draft Manifest entry and its generated Imported/preview/archive files together; never roll back an approved asset by overwriting it.

## Do not trigger

Do not use for image generation, broad art cleanup, gameplay changes, room redesign, UI redesign, engine migration, or direct edits to already approved assets.

## Example

“Use `$salon-asset-ingest` to inspect `assets/inbox/wash-chair.png` and ingest it as `furniture-wash-chair-v2` after I provide the approved footprint and anchors.”
