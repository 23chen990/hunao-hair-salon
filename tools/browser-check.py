#!/usr/bin/env python3
import argparse
import json
import mimetypes
import re
import shutil
import threading
import time
from contextlib import contextmanager
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from PIL import Image, ImageDraw, ImageOps, ImageStat
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD_ROOT = ROOT / "unity-hair-salon" / "Builds"
EVIDENCE = BUILD_ROOT / "PipelineEvidence"
VIEWPORT = {"width": 844, "height": 390}
MOBILE_USER_AGENT = "Mozilla/5.0 (Linux; Android 13; HairSalonQA) AppleWebKit/537.36 Chrome/140 Mobile Safari/537.36"
MANIFEST_PATH = ROOT / "unity-hair-salon" / "Assets" / "Resources" / "AssetPipeline" / "asset-manifest.json"
REFERENCE_BASELINE_PATH = ROOT / "unity-hair-salon" / "Assets" / "Resources" / "AssetPipeline" / "reference-visual-baseline.json"
ASSET_QA_VIEWS = {"inspect": "view=inspect", "context": "view=context", "pixel": "view=pixel"}
APPROVED_REFERENCE = ROOT / "unity-hair-salon" / "Docs" / "VisualReferences" / "salon-overview-visual-reference.png"
OLD_REFERENCE_SCENE = ROOT / "unity-hair-salon" / "Docs" / "CalibrationEvidence" / "old-reference-scene-round2-2-844x390.png"


class UnityHandler(SimpleHTTPRequestHandler):
    def log_message(self, *_):
        pass

    def end_headers(self):
        path = self.path.split("?", 1)[0]
        if path.endswith(".br"):
            self.send_header("Content-Encoding", "br")
            if path.endswith(".wasm.br"):
                self.send_header("Content-Type", "application/wasm")
            elif path.endswith(".js.br"):
                self.send_header("Content-Type", "application/javascript")
            else:
                self.send_header("Content-Type", "application/octet-stream")
        elif path.endswith(".gz"):
            self.send_header("Content-Encoding", "gzip")
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")
        super().end_headers()


@contextmanager
def serve(directory):
    handler = partial(UnityHandler, directory=str(directory))
    server = ThreadingHTTPServer(("127.0.0.1", 0), handler)
    thread = threading.Thread(target=server.serve_forever, daemon=True)
    thread.start()
    try:
        yield f"http://127.0.0.1:{server.server_port}"
    finally:
        server.shutdown()
        thread.join(timeout=2)


def assert_screenshot(path):
    image = Image.open(path).convert("RGB")
    if image.size != (VIEWPORT["width"], VIEWPORT["height"]):
        raise AssertionError(f"截图尺寸错误：{image.size}")
    if sum(ImageStat.Stat(image).var) < 40:
        raise AssertionError(f"截图疑似空白或纯色：{path}")


def assert_no_magenta_shader_fallback(path):
    image = Image.open(path).convert("RGB")
    magenta = sum(1 for red, green, blue in image.getdata()
                  if red > 220 and blue > 220 and green < 45)
    if magenta > image.width * image.height * .002:
        raise AssertionError(f"Reference Scene contains magenta shader fallback：{path} ({magenta} pixels)")


def assert_no_day_transition_overlay(path):
    luminance = ImageStat.Stat(Image.open(path).convert("L")).mean[0]
    if luminance < 55:
        raise AssertionError(f"正式 Demo 截图仍被开店暗场覆盖：{path} (mean luminance={luminance:.1f})")


def wait_for_canvas(page, settle_ms=12000):
    page.wait_for_selector("#unity-canvas", state="visible", timeout=60000)
    if settle_ms:
        page.wait_for_timeout(settle_ms)


def wait_for_marker(page, messages, marker, timeout_seconds=45):
    deadline = time.time() + timeout_seconds
    while time.time() < deadline and not any(marker in item for item in messages):
        page.wait_for_timeout(100)
    matches = [item for item in messages if marker in item]
    if not matches:
        raise AssertionError(f"候选场景没有产生运行标记：{marker}")
    return matches[-1]


def new_mobile_page(browser):
    return browser.new_page(
        viewport=VIEWPORT,
        user_agent=MOBILE_USER_AGENT,
        is_mobile=True,
        has_touch=True,
        device_scale_factor=1)


def asset_definition(asset_id):
    manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
    return next((asset for asset in manifest.get("Assets", []) if asset.get("Id") == asset_id), None)


def render_source_preview(asset_id):
    asset = asset_definition(asset_id)
    if not asset:
        raise AssertionError(f"Manifest 中找不到资产：{asset_id}")
    original = Path(asset.get("Source", {}).get("OriginalPath", ""))
    if not original.is_file():
        resource = asset.get("ResourcePath", "")
        original = ROOT / "unity-hair-salon" / "Assets" / "Resources" / f"{resource}.png"
    source = Image.open(original).convert("RGBA")
    canvas = Image.new("RGBA", (VIEWPORT["width"], VIEWPORT["height"]), (0, 0, 0, 255))
    draw = ImageDraw.Draw(canvas)
    tile = 24
    for y in range(0, VIEWPORT["height"], tile):
        for x in range(0, VIEWPORT["width"], tile):
            value = 196 if ((x // tile + y // tile) & 1) == 0 else 142
            draw.rectangle((x, y, x + tile - 1, y + tile - 1), fill=(value, value, value, 255))
    x_offset = (VIEWPORT["width"] - source.width) // 2
    y_offset = (VIEWPORT["height"] - source.height) // 2
    canvas.alpha_composite(source, (x_offset, y_offset))
    shot = EVIDENCE / f"source-preview-{asset_id}-100pct-844x390.png"
    canvas.convert("RGB").save(shot)
    assert_screenshot(shot)
    return {"assetId": asset_id, "screenshot": shot.name, "scale": "1 texture pixel : 1 evidence pixel",
            "sourceSize": [source.width, source.height], "sourcePath": str(original)}


def capture_asset_lab_page(browser, base, asset_id, view, diagnostics, ui, stem, verify_runtime=True,
                           context_scale=None):
    page = new_mobile_page(browser)
    errors = []
    messages = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: (messages.append(message.text), errors.append(message.text) if message.type == "error" else None))
    query = f"?assetId={asset_id}&{ASSET_QA_VIEWS[view]}&diagnostics={diagnostics}&ui={1 if ui else 0}"
    if context_scale is not None:
        query += f"&contextScale={context_scale:.3f}"
    page.goto(base + "/" + query, wait_until="domcontentloaded", timeout=60000)
    wait_for_canvas(page)
    ready = [item for item in messages if "[ASSET_LAB_READY]" in item and f"id={asset_id} " in item]
    if not ready:
        raise AssertionError(f"资产实验室没有确认目标资产：{asset_id}")
    if "realArtwork=True" not in ready[-1] or f"view={view}" not in ready[-1]:
        raise AssertionError(f"资产实验室显示状态不符合预期：{ready[-1]}")
    if context_scale is not None and f"contextScale={context_scale:.3f}" not in ready[-1]:
        raise AssertionError(f"资产实验室没有应用 Context 比例 {context_scale:.3f}：{ready[-1]}")
    asset = asset_definition(asset_id)
    expected_runtime = f"runtimeTexture={asset['Source']['Width']}x{asset['Source']['Height']}"
    if verify_runtime and expected_runtime not in ready[-1]:
        raise AssertionError(f"WebGL 纹理尺寸发生降采样；期望 {expected_runtime}，实际 {ready[-1]}")
    canvas = page.evaluate("""() => { const c = document.querySelector('#unity-canvas'); return {
        width: c.width, height: c.height, clientWidth: c.clientWidth, clientHeight: c.clientHeight,
        devicePixelRatio: window.devicePixelRatio
    }; }""")
    shot = EVIDENCE / stem
    page.screenshot(path=str(shot))
    assert_screenshot(shot)
    assert_no_magenta_shader_fallback(shot)
    if errors:
        raise AssertionError("资产测试场景浏览器错误：" + " | ".join(errors))
    page.close()
    return {"assetId": asset_id, "view": view, "diagnostics": diagnostics, "ui": ui,
            "contextScale": context_scale,
            "screenshot": shot.name, "ready": ready[-1], "canvas": canvas, "errors": []}


def compose_context_scale_comparison(asset_id, captures, scale_values):
    panels = [Image.open(EVIDENCE / item["screenshot"]).convert("RGB") for item in captures]
    header_height = 40
    comparison = Image.new("RGB", (VIEWPORT["width"] * len(panels), VIEWPORT["height"] + header_height),
                           (24, 27, 29))
    draw = ImageDraw.Draw(comparison)
    asset = asset_definition(asset_id)
    base_size = asset["DesiredWorldSize"]
    labels = []
    for index, (panel, scale) in enumerate(zip(panels, scale_values)):
        x = index * VIEWPORT["width"]
        comparison.paste(panel, (x, header_height))
        world_width = base_size["x"] * scale
        world_height = base_size["y"] * scale
        label = f"{scale:.2f}x  |  {world_width:.3f} x {world_height:.3f} units"
        labels.append(label)
        draw.text((x + 18, 13), label, fill=(245, 245, 245))
        if index:
            draw.line((x, 0, x, comparison.height), fill=(220, 220, 220), width=2)
    scale_stem = "-".join(f"{round(value * 100):03d}" for value in scale_values)
    output = EVIDENCE / f"asset-lab-{asset_id}-context-scale-comparison-{scale_stem}.png"
    comparison.save(output)
    if sum(ImageStat.Stat(comparison).var) < 40:
        raise AssertionError(f"Context 比例对比图疑似空白：{output}")
    return {"assetId": asset_id, "screenshot": output.name, "scales": scale_values,
            "labels": labels, "panelViewport": VIEWPORT}


def capture_reference_page(browser, base, query, stem):
    page = new_mobile_page(browser)
    errors = []
    messages = []
    failed_requests = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: (messages.append(message.text), errors.append(message.text)
                                              if message.type == "error" else None))
    page.on("requestfailed", lambda request: failed_requests.append(request.url))
    page.goto(base + "/" + query, wait_until="domcontentloaded", timeout=60000)
    wait_for_canvas(page, 0)
    ready = wait_for_marker(page, messages, "REFERENCE_VISUAL_READY")
    calibration = wait_for_marker(page, messages, "OVERLAY_CALIBRATION_READY")
    if "camera=orthographic" not in ready or "washStatus=NEEDS-REVIEW" not in ready:
        raise AssertionError("Reference Scene 运行标记不完整：" + ready)
    match = re.search(r"screenMetrics=(\{.*\})", calibration)
    if not match:
        raise AssertionError("Overlay Calibration 缺少屏幕相对指标：" + calibration)
    screen_metrics = json.loads(match.group(1))
    page.wait_for_timeout(1000)
    canvas = page.evaluate("""() => { const c = document.querySelector('#unity-canvas'); return {
        width: c.width, height: c.height, clientWidth: c.clientWidth, clientHeight: c.clientHeight,
        devicePixelRatio: window.devicePixelRatio
    }; }""")
    shot = EVIDENCE / stem
    page.screenshot(path=str(shot))
    assert_screenshot(shot)
    assert_no_magenta_shader_fallback(shot)
    page.close()
    if errors or failed_requests:
        raise AssertionError("Reference Scene 浏览器/资源错误：" + " | ".join(errors + failed_requests))
    return {"screenshot": shot.name, "ready": ready, "calibration": calibration,
            "screenMetrics": screen_metrics, "canvas": canvas,
            "errors": [], "failedRequests": []}


def prepare_reference_calibration_sources():
    if not APPROVED_REFERENCE.is_file():
        raise AssertionError(f"已批准参考图缺失：{APPROVED_REFERENCE}")
    if not OLD_REFERENCE_SCENE.is_file():
        raise AssertionError(f"Round 2.2 旧 Reference Scene 证据缺失：{OLD_REFERENCE_SCENE}")
    approved_original = EVIDENCE / "reference-visual-approved-reference-original.png"
    shutil.copyfile(APPROVED_REFERENCE, approved_original)
    approved_cover = ImageOps.fit(
        Image.open(APPROVED_REFERENCE).convert("RGB"),
        (VIEWPORT["width"], VIEWPORT["height"]),
        method=Image.Resampling.LANCZOS,
        centering=(.5, .5))
    approved_cover_path = EVIDENCE / "reference-visual-approved-reference-cover-844x390.png"
    approved_cover.save(approved_cover_path)
    old = EVIDENCE / "reference-visual-old-round2-2-844x390.png"
    shutil.copyfile(OLD_REFERENCE_SCENE, old)
    assert_screenshot(approved_cover_path)
    assert_screenshot(old)
    return {
        "approvedOriginal": approved_original.name,
        "approvedCover": approved_cover_path.name,
        "oldReferenceScene": old.name
    }


def assert_reference_metric_alignment(metrics):
    baseline = json.loads(REFERENCE_BASELINE_PATH.read_text(encoding="utf-8"))
    targets = baseline["VisualMetrics"]
    checks = (
        ("playerScreenHeightRatio", targets["PlayerScreenHeightRatio"], .065),
        ("roomScreenWidthRatio", targets["RoomScreenWidthRatio"], .12),
        ("roomScreenHeightRatio", targets["RoomScreenHeightRatio"], .16),
        ("washToPlayerHeightRatio", targets["WashToPlayerHeightRatio"], .22),
        ("washFootprintToVisibleFloorRatio", targets["WashFootprintToVisibleFloorRatio"], .03),
    )
    failures = []
    for name, expected, tolerance in checks:
        actual = metrics.get(name)
        if actual is None or abs(actual - expected) > tolerance:
            failures.append(f"{name}: actual={actual} target={expected} tolerance={tolerance}")
    expected_tiles = baseline["Ground"]["VisibleTileColumns"]
    if metrics.get("visibleTileColumns") != expected_tiles:
        failures.append(f"visibleTileColumns: actual={metrics.get('visibleTileColumns')} target={expected_tiles}")
    if metrics.get("densityState") != baseline["Density"]["DefaultState"]:
        failures.append(f"densityState: actual={metrics.get('densityState')} target={baseline['Density']['DefaultState']}")
    if failures:
        raise AssertionError("Reference overlay metric alignment failed：" + " | ".join(failures))


def compose_reference_side_by_side(runtime_capture):
    if not APPROVED_REFERENCE.is_file():
        raise AssertionError(f"已批准参考图缺失：{APPROVED_REFERENCE}")
    approved = Image.open(APPROVED_REFERENCE).convert("RGB")
    approved_panel = ImageOps.fit(approved, (VIEWPORT["width"], VIEWPORT["height"]),
                                  method=Image.Resampling.LANCZOS, centering=(.5, .5))
    runtime = Image.open(EVIDENCE / runtime_capture["screenshot"]).convert("RGB")
    header = 42
    output = Image.new("RGB", (VIEWPORT["width"] * 2, VIEWPORT["height"] + header), (24, 27, 29))
    output.paste(approved_panel, (0, header))
    output.paste(runtime, (VIEWPORT["width"], header))
    draw = ImageDraw.Draw(output)
    draw.text((18, 14), "APPROVED SALON EFFECT REFERENCE", fill=(245, 245, 245))
    draw.text((VIEWPORT["width"] + 18, 14), "REFERENCE VISUAL SCENE  |  844 x 390", fill=(245, 245, 245))
    draw.line((VIEWPORT["width"], 0, VIEWPORT["width"], output.height), fill=(230, 230, 230), width=2)
    path = EVIDENCE / "reference-visual-side-by-side-approved-vs-runtime.png"
    output.save(path)
    return path.name


def compose_old_vs_reference_context(reference_capture):
    old_path = EVIDENCE / "asset-lab-furniture-wash-station-vintage-right-wall-context-844x390.png"
    if not old_path.is_file():
        return None
    old = Image.open(old_path).convert("RGB")
    current = Image.open(EVIDENCE / reference_capture["screenshot"]).convert("RGB")
    header = 42
    output = Image.new("RGB", (VIEWPORT["width"] * 2, VIEWPORT["height"] + header), (24, 27, 29))
    output.paste(old, (0, header))
    output.paste(current, (VIEWPORT["width"], header))
    draw = ImageDraw.Draw(output)
    draw.text((18, 14), "OLD ASSET LAB ENGINEERING CONTEXT", fill=(245, 245, 245))
    draw.text((VIEWPORT["width"] + 18, 14), "NEW VISUAL REFERENCE CONTEXT", fill=(245, 245, 245))
    draw.line((VIEWPORT["width"], 0, VIEWPORT["width"], output.height), fill=(230, 230, 230), width=2)
    path = EVIDENCE / "old-context-vs-reference-visual-context.png"
    output.save(path)
    return path.name


def prepare_wash_area_reference_crop():
    if not APPROVED_REFERENCE.is_file():
        raise AssertionError(f"已批准参考图缺失：{APPROVED_REFERENCE}")
    approved = Image.open(APPROVED_REFERENCE).convert("RGB")
    # Product-approved upper-left wash zone: walls, floor, three wash beds and nearby service cart.
    crop = approved.crop((110, 50, 1070, 494)).resize(
        (VIEWPORT["width"], VIEWPORT["height"]), Image.Resampling.LANCZOS)
    path = EVIDENCE / "wash-area-approved-reference-crop-844x390.png"
    crop.save(path)
    assert_screenshot(path)
    return path.name


def capture_demo_wash_area(browser, base, query, mode, stem):
    page = new_mobile_page(browser)
    errors = []
    messages = []
    failed_requests = []
    page.on("pageerror", lambda error: errors.append(str(error)))
    page.on("console", lambda message: (messages.append(message.text), errors.append(message.text)
                                              if message.type == "error" else None))
    page.on("requestfailed", lambda request: failed_requests.append(request.url))
    page.goto(base + "/" + query, wait_until="domcontentloaded", timeout=60000)
    wait_for_canvas(page, 0)
    ready = wait_for_marker(page, messages, "WASH_AREA_VISUAL_READY")
    core_flow = wait_for_marker(page, messages, "BROWSER_CORE_FLOW_PASS")
    if f"mode={mode}" not in ready or "scene=HairSalonDemo" not in ready:
        raise AssertionError("正式 Demo 洗发区证据标记不完整：" + ready)
    if mode in ("after", "context") and not all(token in ready for token in (
            "realWash=True", "realDecoration=True", "orientation=right-wall",
            "mirrored=False", "rotated=False", "negativeScale=False",
            "collidersPreserved=True", "anchorsPreserved=True")):
        raise AssertionError("正式 Demo 洗发区视觉替换破坏了生产约束：" + ready)
    page.wait_for_timeout(1200)
    canvas = page.evaluate("""() => { const c = document.querySelector('#unity-canvas'); return {
        width: c.width, height: c.height, clientWidth: c.clientWidth, clientHeight: c.clientHeight,
        devicePixelRatio: window.devicePixelRatio
    }; }""")
    shot = EVIDENCE / stem
    page.screenshot(path=str(shot))
    assert_screenshot(shot)
    assert_no_magenta_shader_fallback(shot)
    assert_no_day_transition_overlay(shot)
    page.close()
    if errors or failed_requests:
        raise AssertionError("正式 Demo 洗发区浏览器/资源错误：" + " | ".join(errors + failed_requests))
    return {"mode": mode, "screenshot": shot.name, "ready": ready, "coreFlow": core_flow, "canvas": canvas,
            "errors": [], "failedRequests": []}


def main():
    parser = argparse.ArgumentParser(description="Run deterministic browser checks for the salon WebGL builds.")
    parser.add_argument("--mode", choices=("all", "asset-lab", "demo", "candidate", "reference"), default="all")
    parser.add_argument("--asset-id", action="append", default=[], help="Asset-lab stable ID to capture; repeatable.")
    parser.add_argument("--asset-lab-build", default="WebGLAssetLab", help="Asset-lab build folder under Builds.")
    parser.add_argument("--allow-runtime-downsample", action="store_true", help="A/B legacy evidence only; do not use for final QA.")
    parser.add_argument("--asset-lab-capture", choices=("full", "inspect-only"), default="full")
    parser.add_argument("--context-scale-compare", help="Stable asset ID for a fixed-camera Context scale comparison.")
    parser.add_argument("--context-scale-values", default="0.80,0.85,0.90",
                        help="Comma-separated Context scale multipliers used by --context-scale-compare.")
    args = parser.parse_args()
    scale_values = [float(item.strip()) for item in args.context_scale_values.split(",") if item.strip()]
    if args.context_scale_compare and (len(scale_values) < 2 or len(scale_values) > 6 or
                                       any(value < .25 or value > 2 for value in scale_values)):
        raise AssertionError("Context scale comparison needs 2-6 values between 0.25 and 2.0.")
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    report = {
        "viewport": VIEWPORT,
        "assetLab": {},
        "demo": {},
        "candidate": {},
        "reference": {},
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S%z")
    }
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)

        if args.mode in ("all", "reference"):
          with serve(BUILD_ROOT / "WebGLReferenceVisual") as base:
            calibration_sources = prepare_reference_calibration_sources()
            overview = capture_reference_page(
                browser, base, "?density=target-density&overlay=0",
                "reference-visual-new-calibrated-844x390.png")
            assert_reference_metric_alignment(overview["screenMetrics"])
            low_density = capture_reference_page(
                browser, base, "?density=low-density&overlay=0",
                "reference-visual-low-density-844x390.png")
            overlay = capture_reference_page(
                browser, base, "?density=target-density&overlay=1&overlayOpacity=0.50",
                "reference-visual-overlay-calibration-844x390.png")
            debug = capture_reference_page(
                browser, base, "?density=target-density&overlay=0&debug=1",
                "reference-visual-debug-844x390.png")
            side_by_side = compose_reference_side_by_side(overview)
            old_vs_reference = compose_old_vs_reference_context(overview)
            screenshots = [calibration_sources["approvedOriginal"], calibration_sources["oldReferenceScene"],
                           overview["screenshot"], overlay["screenshot"], side_by_side,
                           low_density["screenshot"], debug["screenshot"]]
            if old_vs_reference:
                screenshots.append(old_vs_reference)
            report["reference"] = {
                "loaded": True,
                "approvedReference": str(APPROVED_REFERENCE),
                "screenshots": screenshots,
                "calibrationEvidence": {
                    "A_approvedReference": calibration_sources["approvedOriginal"],
                    "B_oldReferenceScene": calibration_sources["oldReferenceScene"],
                    "C_newReferenceScene": overview["screenshot"],
                    "D_overlay": overlay["screenshot"]
                },
                "captures": {"new": overview, "overlay": overlay, "lowDensity": low_density, "debug": debug},
                "screenMetrics": overview["screenMetrics"],
                "oldVsReferenceContext": old_vs_reference,
                "errors": []
            }

        if args.mode in ("all", "asset-lab"):
          with serve(BUILD_ROOT / args.asset_lab_build) as base:
            manifest = json.loads(MANIFEST_PATH.read_text(encoding="utf-8"))
            targets = args.asset_id or [asset["Id"] for asset in manifest.get("Assets", [])
                                       if asset.get("Status") in ("candidate", "NEEDS-REVIEW") and
                                       asset.get("ImportProfile") == "production-2.5d-rendered"]
            if args.context_scale_compare and args.context_scale_compare not in targets:
              targets.append(args.context_scale_compare)
            captures = []
            source_previews = []
            for asset_id in targets:
              if not re.fullmatch(r"[a-z0-9]+(?:-[a-z0-9]+)*", asset_id):
                raise AssertionError(f"非法 asset ID：{asset_id}")
              source_previews.append(render_source_preview(asset_id))
              specs = (
                  ("inspect", "preview", False, f"asset-lab-{asset_id}-inspect-844x390.png"),
                  ("inspect", "all", False, f"asset-lab-{asset_id}-inspect-debug-844x390.png"),
                  ("context", "preview", False, f"asset-lab-{asset_id}-context-844x390.png"),
                  ("pixel", "preview", False, f"asset-lab-{asset_id}-pixel-100pct-844x390.png"),
              )
              if args.asset_lab_capture == "inspect-only":
                specs = specs[:1]
              for view, diagnostics, ui, stem in specs:
                captures.append(capture_asset_lab_page(
                    browser, base, asset_id, view, diagnostics, ui, stem,
                    verify_runtime=not args.allow_runtime_downsample))
            scale_comparison = None
            if args.context_scale_compare:
              scale_captures = []
              for scale in scale_values:
                scale_code = f"{round(scale * 100):03d}"
                scale_captures.append(capture_asset_lab_page(
                    browser, base, args.context_scale_compare, "context", "preview", False,
                    f"asset-lab-{args.context_scale_compare}-context-scale-{scale_code}-844x390.png",
                    verify_runtime=not args.allow_runtime_downsample, context_scale=scale))
              captures.extend(scale_captures)
              scale_comparison = compose_context_scale_comparison(
                  args.context_scale_compare, scale_captures, scale_values)
            report["assetLab"] = {
                "loaded": True,
                "sourcePreviews": source_previews,
                "screenshots": ([item["screenshot"] for item in source_previews] +
                                [item["screenshot"] for item in captures]),
                "captures": captures,
                "contextScaleComparison": scale_comparison,
                "errors": []
            }

        if args.mode in ("all", "candidate"):
          with serve(BUILD_ROOT / "WebGLCandidate") as base:
            candidate_runs = []
            for debug in (False, True):
              page = new_mobile_page(browser)
              errors = []
              messages = []
              failed_requests = []
              page.on("pageerror", lambda error, errors=errors: errors.append(str(error)))
              page.on("console", lambda message, errors=errors, messages=messages: (messages.append(message.text), errors.append(message.text) if message.type == "error" else None))
              page.on("requestfailed", lambda request, failed_requests=failed_requests: failed_requests.append(request.url))
              page.goto(base + ("/?debug=1" if debug else "/"), wait_until="domcontentloaded", timeout=60000)
              wait_for_canvas(page, 0)

              wait_for_marker(page, messages, "CANDIDATE_ASSETS_READY")
              if debug:
                page.keyboard.press("F3")
                page.wait_for_timeout(300)
              if not debug:
                overview = EVIDENCE / "candidate-overview-844x390.png"
                page.screenshot(path=str(overview))
                assert_screenshot(overview)

              alignment_marker = wait_for_marker(page, messages, "CANDIDATE_ALIGNMENT_READY")
              alignment = EVIDENCE / ("candidate-debug-alignment-844x390.png" if debug else "candidate-alignment-844x390.png")
              page.screenshot(path=str(alignment))
              assert_screenshot(alignment)

              active_marker = wait_for_marker(page, messages, "CANDIDATE_SERVICE_ACTIVE")
              active = EVIDENCE / ("candidate-debug-service-active-844x390.png" if debug else "candidate-service-active-844x390.png")
              page.screenshot(path=str(active))
              assert_screenshot(active)

              completion_marker = wait_for_marker(page, messages, "CANDIDATE_FLOW_PASS", 60)
              complete = EVIDENCE / ("candidate-debug-complete-844x390.png" if debug else "candidate-complete-844x390.png")
              page.screenshot(path=str(complete))
              assert_screenshot(complete)
              if errors or failed_requests:
                raise AssertionError("候选场景浏览器/资源错误：" + " | ".join(errors + failed_requests))
              candidate_runs.append({
                  "debug": debug,
                  "screenshots": [alignment.name, active.name, complete.name],
                  "alignment": alignment_marker,
                  "activeService": active_marker,
                  "completion": completion_marker,
                  "errors": [],
                  "failedRequests": []
              })
              page.close()
            report["candidate"] = {
                "loaded": True,
                "coreFlowPassed": True,
                "runs": candidate_runs,
                "screenshots": (["candidate-overview-844x390.png"] +
                                [shot for run in candidate_runs for shot in run["screenshots"]]),
                "errors": []
            }

        if args.mode in ("all", "demo"):
          with serve(BUILD_ROOT / "WebGLDemo") as base:
            opening_page = new_mobile_page(browser)
            opening_errors = []
            opening_page.on("pageerror", lambda error: opening_errors.append(str(error)))
            opening_page.on("console", lambda message: opening_errors.append(message.text)
                            if message.type == "error" else None)
            opening_page.goto(base + "/", wait_until="domcontentloaded", timeout=60000)
            wait_for_canvas(opening_page)
            opening_shot = EVIDENCE / "demo-ui-opening-844x390.png"
            opening_page.screenshot(path=str(opening_shot))
            assert_screenshot(opening_shot)
            if opening_errors:
                raise AssertionError("Demo 开店 UI 浏览器错误：" + " | ".join(opening_errors))
            opening_page.close()

            page = new_mobile_page(browser)
            errors = []
            messages = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("console", lambda message: (messages.append(message.text), errors.append(message.text) if message.type == "error" else None))
            page.goto(base + "/?browserSmoke=1", wait_until="domcontentloaded", timeout=60000)
            wait_for_canvas(page)
            deadline = time.time() + 30
            while time.time() < deadline and not any("[BROWSER_CORE_FLOW_PASS]" in item for item in messages):
                page.wait_for_timeout(250)
            if not any("[BROWSER_CORE_FLOW_PASS]" in item for item in messages):
                relevant = [item for item in messages if "BROWSER_CORE_FLOW" in item]
                raise AssertionError("真实浏览器核心流程未通过：" + " | ".join(relevant or messages[-10:]))
            shot = EVIDENCE / "demo-core-flow-844x390.png"
            page.screenshot(path=str(shot))
            assert_screenshot(shot)
            if errors:
                raise AssertionError("Demo 浏览器错误：" + " | ".join(errors))
            report["demo"] = {
                "loaded": True,
                "coreFlowPassed": True,
                "screenshot": shot.name,
                "errors": []
            }
            page.close()

            service_page = new_mobile_page(browser)
            service_errors = []
            service_messages = []
            service_page.on("pageerror", lambda error: service_errors.append(str(error)))
            service_page.on("console", lambda message: (service_messages.append(message.text),
                            service_errors.append(message.text) if message.type == "error" else None))
            service_page.goto(base + "/?browserSmoke=1&uiEvidence=service",
                              wait_until="domcontentloaded", timeout=60000)
            wait_for_canvas(service_page, 0)
            service_ready = wait_for_marker(service_page, service_messages, "UI_SERVICE_READY")
            service_page.wait_for_timeout(300)
            service_shot = EVIDENCE / "demo-ui-active-service-844x390.png"
            service_page.screenshot(path=str(service_shot))
            assert_screenshot(service_shot)
            if service_errors:
                raise AssertionError("Demo 服务工具栏浏览器错误：" + " | ".join(service_errors))
            report["demo"]["uiEvidence"] = {
                "opening": opening_shot.name,
                "activeService": service_shot.name,
                "serviceReady": service_ready,
                "viewport": VIEWPORT,
                "errors": []
            }
            service_page.close()

            debug_page = new_mobile_page(browser)
            debug_errors = []
            debug_messages = []
            debug_page.on("pageerror", lambda error: debug_errors.append(str(error)))
            debug_page.on("console", lambda message: (debug_messages.append(message.text), debug_errors.append(message.text) if message.type == "error" else None))
            debug_page.goto(base + "/?debug=1&browserSmoke=1", wait_until="domcontentloaded", timeout=60000)
            wait_for_canvas(debug_page)
            deadline = time.time() + 30
            while time.time() < deadline and not any("[BROWSER_CORE_FLOW_PASS]" in item for item in debug_messages):
                debug_page.wait_for_timeout(250)
            if not any("[BROWSER_CORE_FLOW_PASS]" in item for item in debug_messages):
                raise AssertionError("Demo 调试模式核心流程未通过")
            debug_shot = EVIDENCE / "demo-core-flow-debug-844x390.png"
            debug_page.screenshot(path=str(debug_shot))
            assert_screenshot(debug_shot)
            if debug_errors:
                raise AssertionError("Demo 调试模式浏览器错误：" + " | ".join(debug_errors))
            report["demo"]["debugModeVisible"] = True
            report["demo"]["debugScreenshot"] = debug_shot.name
            debug_page.close()

            approved_crop = prepare_wash_area_reference_crop()
            wash_specs = (
                ("?washAreaVisual=before&browserSmoke=1", "before", "wash-area-demo-before-844x390.png"),
                ("?washAreaVisual=after&browserSmoke=1", "after", "wash-area-demo-after-844x390.png"),
                ("?washAreaVisual=context&browserSmoke=1", "context", "wash-area-player-context-844x390.png"),
            )
            wash_captures = {
                mode: capture_demo_wash_area(browser, base, query, mode, stem)
                for query, mode, stem in wash_specs
            }

            report["demo"]["washAreaEvidence"] = {
                "A_approvedReferenceCrop": approved_crop,
                "B_formalDemoBefore": wash_captures["before"]["screenshot"],
                "C_formalDemoAfter": wash_captures["after"]["screenshot"],
                "D_playerWashContext": wash_captures["context"]["screenshot"],
                "captures": wash_captures,
                "integrationCoreFlow": wash_captures["after"]["coreFlow"],
                "integrationReady": wash_captures["after"]["ready"],
                "releaseDefaultChanged": False,
            }
        browser.close()

    serialized = json.dumps(report, ensure_ascii=False, indent=2) + "\n"
    (EVIDENCE / "browser-check.json").write_text(serialized, encoding="utf-8")
    (EVIDENCE / f"browser-check-{args.mode}.json").write_text(serialized, encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
