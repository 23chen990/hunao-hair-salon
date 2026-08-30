# Visual QA checklist

Use the screenshot, browser log and approved reference. Report `pass`, `issue`, `not-applicable`, or `baseline-missing` for every category.

| Category | Evidence to check |
|---|---|
| resources | missing textures, magenta materials, placeholder art, console load errors |
| proportion | furniture/character/UI relative size; source aspect ratio distortion |
| position | approved room layout, anchor alignment, screen-space offsets |
| direction | back-wall/right-wall orientation and facing consistency |
| shadow | contact, size, offset, softness, opacity; no shadow baked into new furniture art |
| occlusion/depth | character behind/in front of furniture, sorting order, world-Z conflicts |
| character size | feet contact, headroom, relation to chairs and counters |
| safe area | HUD and buttons inside the visible 844×390 frame; no edge clipping |
| stability | wait at least 12 seconds after the canvas appears; capture only after assets settle |

Severity:

- blocker: scene fails to start, missing critical resource, interaction-obscuring layout
- high: wrong direction/scale/occlusion that changes approved reading
- medium: visible offset, shadow, crop or safe-area defect
- low: polish discrepancy that does not affect reading or interaction

The current browser script writes evidence under `unity-hair-salon/Builds/PipelineEvidence/`. The main scene debug screenshot intentionally includes the development overlay; do not mistake it for formal HUD.
