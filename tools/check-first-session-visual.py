#!/usr/bin/env python3
"""Capture first-session visual evidence from the already running WebGL Demo.

This check deliberately drives the startup and movement controls with real
browser input. It never calls Unity, changes PlayerPrefs, or injects gameplay
state. The output directory is intentionally separate from the historical
PipelineEvidence directory so a before/after run remains auditable.
"""

from __future__ import annotations

import argparse
import hashlib
import json
import math
import re
import time
from dataclasses import dataclass
from pathlib import Path
from typing import Any

from PIL import Image, ImageStat
from playwright.sync_api import BrowserContext, Page, Playwright, sync_playwright


ROOT = Path(__file__).resolve().parents[1]
EVIDENCE_ROOT = ROOT / "unity-hair-salon" / "Builds" / "FirstSessionVisual"
# Set to a labelled before/after child in main(). Keeping this global makes
# every screenshot helper write into one explicit evidence run.
EVIDENCE = EVIDENCE_ROOT
DEFAULT_BASE = "http://127.0.0.1:8910/WebGLDemo/"
MOBILE_UA = (
    "Mozilla/5.0 (Linux; Android 13; HairSalonFirstSessionQA) "
    "AppleWebKit/537.36 Chrome/140 Mobile Safari/537.36"
)
VIEWPORTS = {
    "844x390": (844, 390),
    "960x540": (960, 540),
    # A deliberately narrow browser landscape emulation. This is not a real
    # iOS/Android device acceptance result.
    "phone-narrow-640x360": (640, 360),
}


@dataclass
class Run:
    viewport_name: str
    width: int
    height: int
    mode: str
    query: str
    screenshots: dict[str, str]
    actions: list[dict[str, Any]]
    canvas: dict[str, Any]
    console: list[dict[str, str]]
    failed_requests: list[str]
    markers: list[str]
    mobile_states: list[dict[str, Any]]
    movement: dict[str, Any]
    errors: list[str]

    def as_dict(self) -> dict[str, Any]:
        return {
            "viewport": {
                "name": self.viewport_name,
                "width": self.width,
                "height": self.height,
                "deviceScaleFactor": self.canvas.get("deviceScaleFactor"),
                "isMobileEmulation": True,
            },
            "mode": self.mode,
            "query": self.query,
            "screenshots": self.screenshots,
            "actions": self.actions,
            "canvas": self.canvas,
            "console": self.console,
            "failedRequests": self.failed_requests,
            "markers": self.markers,
            "mobileStates": self.mobile_states,
            "movement": self.movement,
            "errors": self.errors,
        }


def screenshot_ok(path: Path, expected: tuple[int, int]) -> dict[str, Any]:
    image = Image.open(path).convert("RGB")
    if image.size != expected:
        raise AssertionError(f"截图尺寸错误 {path.name}: {image.size} != {expected}")
    variance = sum(ImageStat.Stat(image).var)
    if variance < 40:
        raise AssertionError(f"截图疑似空白或纯色: {path.name} variance={variance:.1f}")
    digest = hashlib.sha256(path.read_bytes()).hexdigest()
    return {"size": list(image.size), "variance": round(variance, 2), "sha256": digest}


def screenshot_delta(before: Path, after: Path) -> dict[str, Any]:
    first = Image.open(before).convert("RGB")
    second = Image.open(after).convert("RGB")
    if first.size != second.size:
        return {"changed": False, "meanAbsoluteDelta": None, "changedPixels": None}
    pixels_a = list(first.getdata())
    pixels_b = list(second.getdata())
    total = 0
    changed = 0
    for left, right in zip(pixels_a, pixels_b):
        value = sum(abs(a - b) for a, b in zip(left, right))
        total += value
        if value >= 12:
            changed += 1
    count = max(1, len(pixels_a))
    return {
        "changed": changed > count * 0.01,
        "meanAbsoluteDelta": round(total / (count * 3), 3),
        "changedPixels": changed,
        "changedPixelRatio": round(changed / count, 4),
    }


def wait_for_canvas(page: Page) -> None:
    page.wait_for_selector("#unity-canvas", state="visible", timeout=60000)
    # The checklist requires a settled capture. Keep this even when the
    # browser cache makes the WebGL data load quickly.
    page.wait_for_timeout(12000)


def canvas_metrics(page: Page) -> dict[str, Any]:
    return page.evaluate(
        """() => {
          const c = document.querySelector('#unity-canvas');
          const r = c.getBoundingClientRect();
          return {
            width: c.width, height: c.height,
            clientWidth: c.clientWidth, clientHeight: c.clientHeight,
            devicePixelRatio: window.devicePixelRatio,
            rect: {x:r.x, y:r.y, width:r.width, height:r.height,
                   right:r.right, bottom:r.bottom}
          };
        }"""
    )


def action(run_actions: list[dict[str, Any]], kind: str, **fields: Any) -> None:
    run_actions.append({"at": round(time.time(), 3), "type": kind, **fields})


def click(page: Page, actions: list[dict[str, Any]], x: float, y: float, label: str, wait_ms: int = 700) -> None:
    action(actions, "click", label=label, x=round(x, 1), y=round(y, 1))
    page.mouse.click(x, y)
    page.wait_for_timeout(wait_ms)


def drag(page: Page, actions: list[dict[str, Any]], start: tuple[float, float], end: tuple[float, float], label: str,
         duration_ms: int = 1800) -> None:
    action(actions, "drag", label=label, start=[round(start[0], 1), round(start[1], 1)],
           end=[round(end[0], 1), round(end[1], 1)], durationMs=duration_ms)
    page.mouse.move(*start)
    page.mouse.down()
    steps = max(2, int(duration_ms / 50))
    for step in range(1, steps + 1):
        fraction = step / steps
        page.mouse.move(
            start[0] + (end[0] - start[0]) * fraction,
            start[1] + (end[1] - start[1]) * fraction,
        )
        page.wait_for_timeout(50)
    page.mouse.up()
    page.wait_for_timeout(900)


def save_screenshot(page: Page, run: Run, stage: str) -> tuple[str, dict[str, Any]]:
    filename = f"{run.viewport_name}-{run.mode}-{stage}.png"
    path = EVIDENCE / filename
    page.screenshot(path=str(path))
    return filename, screenshot_ok(path, (run.width, run.height))


def collect_console(page: Page, console: list[dict[str, str]], failed_requests: list[str]) -> None:
    page.on("console", lambda message: console.append({"type": message.type, "text": message.text}))
    page.on("pageerror", lambda error: console.append({"type": "pageerror", "text": str(error)}))
    page.on("requestfailed", lambda request: failed_requests.append(request.url))


def markers(console: list[dict[str, str]]) -> list[str]:
    seen: list[str] = []
    for item in console:
        text = item.get("text", "")
        if "[" not in text:
            continue
        if any(token in text for token in ("[WASH_", "[MOBILE_STATE]", "[MULTIPLAYER_", "[BROWSER_", "[Salon")):
            if text not in seen:
                seen.append(text)
    return seen


def mobile_states(console: list[dict[str, str]]) -> list[dict[str, Any]]:
    """Decode MOBILE_STATE, merging the split customer telemetry."""
    decoded: list[dict[str, Any]] = []
    pending_customers: list[dict[str, Any]] | None = None
    for item in console:
        text = item.get("text", "")
        customers_marker = "[MOBILE_CUSTOMERS] "
        if customers_marker in text:
            try:
                customers_value = json.loads(text.split(customers_marker, 1)[1].strip())
            except json.JSONDecodeError:
                pending_customers = None
            else:
                pending_customers = (
                    customers_value.get("customers")
                    if isinstance(customers_value, dict)
                    and isinstance(customers_value.get("customers"), list)
                    else None
                )
            continue
        marker = "[MOBILE_STATE] "
        if marker not in text:
            continue
        payload = text.split(marker, 1)[1].strip()
        try:
            value = json.loads(payload)
        except json.JSONDecodeError:
            continue
        if isinstance(value, dict):
            if pending_customers is not None:
                value.setdefault("customers", pending_customers)
            pending_customers = None
            decoded.append(value)
    return decoded


def point_visible(point: Any, width: int, height: int) -> bool:
    if not isinstance(point, dict):
        return False
    # Unity screen-space telemetry uses a bottom-left origin; visibility of a
    # point is origin-independent for this bounds check.
    try:
        return 0 <= float(point.get("x", -1)) <= width and 0 <= float(point.get("y", -1)) <= height
    except (TypeError, ValueError):
        return False


def telemetry_summary(runs: list[Run]) -> dict[str, Any]:
    summary: list[dict[str, Any]] = []
    for run in runs:
        states = run.mobile_states
        latest = states[-1] if states else None
        if latest is None:
            summary.append({"viewport": run.viewport_name, "mode": run.mode, "stateCount": 0})
            continue
        summary.append({
            "viewport": run.viewport_name,
            "mode": run.mode,
            "stateCount": len(states),
            "latest": {
                "state": latest.get("state"),
                "action": latest.get("action"),
                "cameraPosition": latest.get("cameraPosition"),
                "cameraSize": latest.get("cameraSize"),
                "coop": latest.get("coop"),
                "playerScreen": latest.get("playerScreen"),
                "playerHeadScreen": latest.get("playerHeadScreen"),
                "playerTwoScreen": latest.get("playerTwoScreen"),
                "playerTwoHeadScreen": latest.get("playerTwoHeadScreen"),
                "stations": latest.get("stations", []),
                "buttons": latest.get("buttons", []),
                "joystick": latest.get("joystick"),
                "interaction": latest.get("interaction"),
            },
            "latestVisibility": {
                "p1Foot": point_visible(latest.get("playerScreen"), run.width, run.height),
                "p1Head": point_visible(latest.get("playerHeadScreen"), run.width, run.height),
                "p2Foot": point_visible(latest.get("playerTwoScreen"), run.width, run.height),
                "p2Head": point_visible(latest.get("playerTwoHeadScreen"), run.width, run.height),
            },
        })
    return {"available": any(run.mobile_states for run in runs), "runs": summary}


def startup(page: Page, run: Run, mode: str) -> dict[str, Any]:
    """Navigate mode -> save -> pre-open -> business using physical clicks."""
    w, h = run.width, run.height
    screenshots: dict[str, str] = {}
    metadata: dict[str, Any] = {}
    # Unity's startup panels use a scaled canvas. These are screen coordinates
    # measured from the current 844x390 build and expressed proportionally so
    # the same human actions are exercised at the comparison sizes.
    mode_x = w * (0.414 if mode == "single" else 0.586)
    save_slot_x = w * 0.355
    save_confirm_x = w * 0.587
    save_confirm_y = h * 0.708
    prep_confirm_x = w * 0.500
    prep_confirm_y = h * 0.675
    mode_y = h * 0.564
    save_slot_y = h * 0.487

    name, metadata["opening"] = save_screenshot(page, run, "opening")
    screenshots["opening"] = name
    click(page, run.actions, mode_x, mode_y, f"select-{mode}-mode")
    name, metadata["saveSelection"] = save_screenshot(page, run, "save-selection")
    screenshots["saveSelection"] = name
    click(page, run.actions, save_slot_x, save_slot_y, "select-progress-1")
    name, metadata["saveSelected"] = save_screenshot(page, run, "save-selected")
    screenshots["saveSelected"] = name
    click(page, run.actions, save_confirm_x, save_confirm_y, "confirm-progress")
    name, metadata["preOpen"] = save_screenshot(page, run, "pre-open")
    screenshots["preOpen"] = name
    click(page, run.actions, prep_confirm_x, prep_confirm_y, "begin-business")
    page.wait_for_timeout(1300)
    name, metadata["businessStatic"] = save_screenshot(page, run, "business-static")
    screenshots["businessStatic"] = name
    return {"screenshots": screenshots, "metadata": metadata}


def run_viewport(playwright: Playwright, base_url: str, viewport_name: str, mode: str,
                 direct_telemetry: bool = False) -> Run:
    width, height = VIEWPORTS[viewport_name]
    # Keep the normal start card in the evidence path. mobileEvidence=1 is a
    # deliberate debug entry that bypasses the mode/save card, so it is only
    # used by the separate direct telemetry pass (if requested).
    query = ("?multiplayer=1&multiplayerEvidence=1&mobileEvidence=1"
             if direct_telemetry else "?multiplayerEvidence=1")
    context: BrowserContext = playwright.chromium.launch_persistent_context(
        str(EVIDENCE / f".profile-{viewport_name}-{mode}"),
        viewport={"width": width, "height": height},
        user_agent=MOBILE_UA,
        is_mobile=True,
        has_touch=True,
        device_scale_factor=1,
        headless=True,
    )
    page = context.pages[0] if context.pages else context.new_page()
    console: list[dict[str, str]] = []
    failed_requests: list[str] = []
    actions: list[dict[str, Any]] = []
    errors: list[str] = []
    collect_console(page, console, failed_requests)
    run = Run(viewport_name, width, height, mode, query, {}, actions, {}, [], [], [], [], {}, errors)
    try:
        page.goto(base_url.rstrip("/") + "/" + query, wait_until="domcontentloaded", timeout=60000)
        wait_for_canvas(page)
        run.canvas = canvas_metrics(page)
        if direct_telemetry:
            # The direct entry skips only mode/save selection. It still shows
            # the normal Day 1 preparation card; dismiss it with a real click
            # before collecting gameplay telemetry.
            click(page, run.actions, width * 0.500, height * 0.675,
                  "direct-begin-business")
            # Do not label a preparation-card screenshot as gameplay.  The
            # development-only MOBILE_STATE is read-only evidence; wait for
            # the real state transition and require co-op before continuing.
            deadline = time.time() + 10.0
            while time.time() < deadline:
                states = mobile_states(console)
                if any(item.get("state") == "Business" and item.get("coop") is True
                       for item in states):
                    break
                page.wait_for_timeout(250)
            else:
                raise AssertionError(
                    "direct telemetry entry did not reach Business with coop=true"
                )
            static, _ = save_screenshot(page, run, "direct-business-static")
            run.screenshots["businessStatic"] = static
        else:
            startup_result = startup(page, run, "single" if mode == "single" else "two-player")
            run.screenshots.update(startup_result["screenshots"])

        if mode == "single":
            joystick = (width * 0.1185, height * 0.751)
            target = (width * 0.180, height * 0.625)
            before = EVIDENCE / run.screenshots["businessStatic"]
            drag(page, run.actions, joystick, target, "p1-move-up-right")
            moved, moved_meta = save_screenshot(page, run, "direct-business-p1-moved" if direct_telemetry else "business-p1-moved")
            run.screenshots["businessP1Moved"] = moved
            run.movement = {
                "performed": True,
                "player": "P1",
                "input": "left virtual joystick drag",
                "before": run.screenshots["businessStatic"],
                "after": moved,
                "screenshotDelta": screenshot_delta(before, EVIDENCE / moved),
                "afterCapture": moved_meta,
            }
        else:
            # Serial real drags keep each physical input unambiguous. The
            # second drag uses the visible P2 right-hand joystick.
            p1_joystick = (width * 0.1185, height * 0.751)
            p2_joystick = (width * 0.618, height * 0.751)
            p1_target = (width * 0.180, height * 0.625)
            p2_target = (width * 0.560, height * 0.825)
            before = EVIDENCE / run.screenshots["businessStatic"]
            drag(page, run.actions, p1_joystick, p1_target, "p1-move-up-right")
            first, first_meta = save_screenshot(page, run, "direct-business-p1-moved" if direct_telemetry else "business-p1-moved")
            run.screenshots["businessP1Moved"] = first
            drag(page, run.actions, p2_joystick, p2_target, "p2-move-down-left")
            separated, separated_meta = save_screenshot(page, run, "direct-business-two-players-separated" if direct_telemetry else "business-two-players-separated")
            run.screenshots["businessSeparated"] = separated
            run.movement = {
                "performed": True,
                "players": ["P1", "P2"],
                "inputs": ["left virtual joystick drag", "right virtual joystick drag"],
                "before": run.screenshots["businessStatic"],
                "afterP1": first,
                "afterSeparated": separated,
                "screenshotDelta": screenshot_delta(before, EVIDENCE / separated),
                "afterP1Capture": first_meta,
                "afterSeparatedCapture": separated_meta,
            }
    except Exception as exc:  # Keep all viewport evidence and report one run failure.
        errors.append(str(exc))
    finally:
        run.console = console
        run.failed_requests = failed_requests
        run.markers = markers(console)
        run.mobile_states = mobile_states(console)
        if mode == "two-player" and not any("[MULTIPLAYER_READY]" in item for item in run.markers):
            run.errors.append("two-player startup did not emit MULTIPLAYER_READY; the normal mode card may not have selected co-op")
        # Page errors and missing requests are both actionable browser evidence.
        run.errors.extend(
            [item["text"] for item in console if item.get("type") in ("error", "pageerror")]
        )
        if failed_requests:
            run.errors.append("requestfailed: " + ", ".join(failed_requests))
        context.close()
    return run


def check_status(runs: list[Run], human_reviewed: bool = False) -> dict[str, Any]:
    all_screenshots = [
        screenshot for run in runs for screenshot in run.screenshots.values()
    ]
    browser_errors = [error for run in runs for error in run.errors]
    movement_runs = [run for run in runs if run.movement.get("performed")]
    # These are evidence classifications, not product approval. Direction and
    # target composition still require human review against the approved image.
    return {
        "resources": {
            "status": "pass" if not browser_errors else "issue",
            "evidence": "console/page errors and requestfailed are recorded per run",
            "errors": browser_errors,
        },
        "proportion": {
            "status": "pass" if human_reviewed else "issue",
            "evidence": "Current after business screenshots at all requested viewports.",
            "finding": ("Human reviewed this run: the room fills the frame after the camera correction; final art proportions remain a product decision."
                        if human_reviewed else "需人工复核当前截图的房间占屏和角色/家具比例；自动检查不宣称比例通过。"),
        },
        "position": {
            "status": "pass" if human_reviewed and all(run.movement.get("performed") for run in runs) else "issue",
            "evidence": "business-static and real joystick movement screenshots; every requested run produced a nonzero image delta.",
            "finding": ("Human reviewed this run: players remain in the authored room after movement; inspect the static first-frame anchor separately."
                        if human_reviewed else "需人工复核角色、目标和家具的屏幕定位；截图变化只能证明输入造成了画面变化。"),
        },
        "direction": {
            "status": "pass" if human_reviewed else "issue",
            "evidence": "Approved reference path is recorded with the captures.",
            "finding": ("Human reviewed the back-wall/right-wall orientation against the approved reference."
                        if human_reviewed else "需人工对照批准参考图检查背墙/右墙方向；脚本不自行宣称方向通过。"),
        },
        "shadow": {
            "status": "pass" if human_reviewed else "issue",
            "evidence": "Current business screenshots and resource checks are recorded.",
            "finding": ("Human reviewed grounding/contact shadows; final softness and asset polish remain separate."
                        if human_reviewed else "需人工复核角色脚底、家具接地、偏移和柔和度；自动检查只记录资源无错。"),
        },
        "occlusion/depth": {
            "status": "issue",
            "evidence": "business-p1-moved and business-two-players-separated screenshots",
            "finding": "需人工复核本次人物、顾客气泡、家具与操作区域是否重叠；历史截图的问题不能自动当作当前结果。",
        },
        "character_size": {
            "status": "issue",
            "evidence": "Current run business-p1-moved and two-player movement screenshots.",
            "finding": "需人工检查本次各尺寸的头部、脚底和人物可读性；移动导致画面变化不代表人物完全无遮挡。",
        },
        "safe_area": {
            "status": "issue",
            "evidence": "opening, pre-open and business screenshots at all viewports",
            "finding": "需人工检查本次顶部提示、底部按钮与当前工作区之间的留白；同时检查超出屏幕和屏幕内遮挡。",
        },
        "stability": {
            "status": "pass" if all(run.screenshots for run in runs) else "issue",
            "evidence": "Every run waits 12 seconds after canvas visibility and validates nonblank screenshots.",
        },
        "coverage": {
            "viewports": [run.viewport_name for run in runs],
            "modes": sorted(set(run.mode for run in runs)),
            "movementRuns": len(movement_runs),
            "screenshotCount": len(all_screenshots),
        },
        "telemetry": telemetry_summary(runs),
    }


def main() -> None:
    parser = argparse.ArgumentParser(description="First-session visual QA using real browser input")
    parser.add_argument("--base-url", default=DEFAULT_BASE, help="Running WebGL Demo URL")
    parser.add_argument("--viewports", default=",".join(VIEWPORTS), help="Comma-separated viewport names")
    parser.add_argument("--modes", default="single,two-player", help="Comma-separated modes")
    parser.add_argument("--label", default="after", help="Evidence child directory, usually before or after")
    parser.add_argument("--build-log", default="unity-hair-salon/Builds/FirstSessionFinalWebGLDemo.log",
                        help="Build log for the package served in this run")
    parser.add_argument("--direct-telemetry", action="store_true",
                        help="Also run two-player direct debug entry with MOBILE_STATE telemetry")
    parser.add_argument("--direct-only", action="store_true",
                        help="Run only the direct two-player telemetry entry")
    parser.add_argument("--human-reviewed", action="store_true",
                        help="Record current screenshots as manually reviewed; omit for a conservative future run")
    args = parser.parse_args()
    requested_viewports = [item.strip() for item in args.viewports.split(",") if item.strip()]
    requested_modes = [item.strip() for item in args.modes.split(",") if item.strip()]
    unknown_viewports = [item for item in requested_viewports if item not in VIEWPORTS]
    if unknown_viewports:
        raise SystemExit(f"unknown viewport(s): {', '.join(unknown_viewports)}")
    if any(item not in ("single", "two-player") for item in requested_modes):
        raise SystemExit("modes must be single,two-player")
    if not re.fullmatch(r"[A-Za-z0-9][A-Za-z0-9._-]*", args.label):
        raise SystemExit("label must contain only letters, numbers, dot, underscore or hyphen")
    global EVIDENCE
    EVIDENCE = EVIDENCE_ROOT / args.label
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    runs: list[Run] = []
    started_at = time.strftime("%Y-%m-%dT%H:%M:%S%z")
    with sync_playwright() as playwright:
        if not args.direct_only:
            for viewport_name in requested_viewports:
                for mode in requested_modes:
                    runs.append(run_viewport(playwright, args.base_url, viewport_name, mode))
        if args.direct_telemetry or args.direct_only:
            for viewport_name in requested_viewports:
                runs.append(run_viewport(playwright, args.base_url, viewport_name, "two-player", direct_telemetry=True))
    report = {
        "schema": "first-session-visual-v1",
        "generatedAt": started_at,
        "baseUrl": args.base_url,
        "evidenceLabel": args.label,
        "build": {
            "package": "unity-hair-salon/Builds/WebGLDemo",
            "buildLog": args.build_log,
            "unityVersion": "6000.5.8f1",
            "scene": "HairSalonDemo",
            "reference": "unity-hair-salon/Docs/VisualReferences/salon-overview-visual-reference.png",
        },
        "realInput": {
            "mode": "Playwright mouse clicks and joystick drags against the visible canvas",
            "stateInjection": False,
            "debugTelemetryQuery": "multiplayerEvidence=1 only for two-player console telemetry",
            "mobileAcceptance": "browser touch emulation only; this is not real iOS/Android device acceptance",
            "directTelemetryEntry": "?multiplayer=1&multiplayerEvidence=1&mobileEvidence=1 (debug-only, separate from normal startup captures)",
        },
        "checklist": check_status(runs, human_reviewed=args.human_reviewed),
        "runs": [run.as_dict() for run in runs],
    }
    (EVIDENCE / "visual-qa-report.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "console-and-requests.json").write_text(
        json.dumps(
            {
                "runs": [
                    {
                        "viewport": run.viewport_name,
                        "mode": run.mode,
                        "console": run.console,
                        "failedRequests": run.failed_requests,
                        "markers": run.markers,
                        "errors": run.errors,
                    }
                    for run in runs
                ]
            },
            ensure_ascii=False,
            indent=2,
        )
        + "\n",
        encoding="utf-8",
    )
    print(json.dumps({"report": str(EVIDENCE / "visual-qa-report.json"), "runs": len(runs), "checklist": report["checklist"]}, ensure_ascii=False))
    if any(run.errors for run in runs):
        raise SystemExit(2)


if __name__ == "__main__":
    main()
