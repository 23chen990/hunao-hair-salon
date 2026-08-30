# Bug triage and verification

## Evidence routing

- collision: footprint/collider visualization, blocked movement, overlap or tunneling
- anchor: character reaches the wrong interaction point, wrong facing at a correct station, tool/VFX offset
- sorting: correct transforms but wrong in-front/behind result, sprite sorting order or world-Z conflict
- animation: state is correct but clip/direction/frame/presentation is wrong
- state machine: service/day/customer state or transition is wrong; visual follows the incorrect state
- resource: missing path, magenta/default material, stripped type, unavailable host font
- layout: safe area, RectTransform, viewport scaling, approved room position

Check the copied debug snapshot before inspecting broad code. For world furniture, inspect the Manifest before changing scene coordinates.

## Test selection

- pure rules/state: focused EditMode test, then related suite
- asset/Manifest: Node asset tests plus `BuildScript.ValidatePipeline`
- runtime view/animation: focused Unity view test plus browser screenshot
- WebGL-only resource/stripping/font: packaged-resource test, WebGL rebuild and real Chromium console
- scene layout/sorting: fixed viewport before/after screenshots plus relevant Unity tests

Use fixed customer IDs, seeds, scenarios and viewport where supported. Unity `-runTests` must not be paired with `-quit`; verify the XML result, not only exit code.

## Adjacent-flow examples

- chair anchor fix: both back-wall and right-wall variants
- service state fix: correct action, wrong action and cancellation/cleanup
- font/resource fix: Editor test, WebGL startup and formal scene HUD
- collision fix: blocked point and legal interaction anchor

If a crash prevents a normal “before” frame, capture the browser failure/loading frame and retain the console exception as supporting evidence.
