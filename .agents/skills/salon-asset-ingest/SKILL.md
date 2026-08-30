---
name: salon-asset-ingest
description: Validate and ingest product-approved PNG artwork from assets/inbox into the Hair Salon Unity asset manifest, preview lab, and landscape screenshot evidence. Use for confirmed asset intake or replacement candidates; do not use to invent, redesign, or approve artwork.
---

# Salon Asset Ingest

Use only after the product owner identifies a PNG as approved for technical intake. The skill owns deterministic intake, not visual creation or product approval.

## Inputs

- inbox file name under `assets/inbox/`
- new stable kebab-case asset ID and one type: `furniture`, `character`, `ui-sprite`, `effect`, `prop`
- approved directions, pivot, footprint, collision, required anchors, shadow and sorting values

If configuration is missing, stop after inspection and report what the product owner must confirm. Never infer a new layout or orientation from taste.

## Workflow

1. Read `PROJECT_CONTEXT.md`, `STATUS.md`, `AGENTS.md` and [references/asset-contract.md](references/asset-contract.md).
2. Inspect without mutation: `node tools/asset-pipeline.mjs inspect --file assets/inbox/<file>`.
3. Reject damaged/unsupported images, abnormal whitespace, missing transparency when the approved use requires it, unstable IDs, or any attempt to overwrite an `approved` entry. Treat visible pixels touching an edge as a blocking review warning because the script cannot determine intent.
4. Run `scripts/ingest.sh --file <file> --id <id> --type <type>` only when inputs are confirmed. The script performs naming, copy/archive, preview, Manifest update, validation, asset-lab build and 844×390 Chromium screenshots.
5. Review the asset lab in both wall directions. Verify pivot, footprint, collision, anchors, contact shadow, sorting and safe framing; do not alter the source art to make a failed configuration appear correct.
6. Report imported paths, Manifest status, screenshots, warnings and rollback instructions.

## Completion

Complete only when the intake script succeeds, the Manifest validates, the asset appears in the asset lab, both target-direction checks are appropriate, screenshots are nonblank, and browser console errors are empty. A draft is not “approved” until the product owner approves the visible result.

Rollback: before a successful receipt, the script must leave no partial import. After success, revert the new draft Manifest entry and its generated Imported/preview/archive files together; never roll back an approved asset by overwriting it.

## Do not trigger

Do not use for image generation, broad art cleanup, gameplay changes, room redesign, UI redesign, engine migration, or direct edits to already approved assets.

## Example

“Use `$salon-asset-ingest` to inspect `assets/inbox/wash-chair.png` and ingest it as `furniture-wash-chair-v2` after I provide the approved footprint and anchors.”
