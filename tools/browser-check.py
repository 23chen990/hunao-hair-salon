#!/usr/bin/env python3
import argparse
import json
import mimetypes
import threading
import time
from contextlib import contextmanager
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path

from PIL import Image, ImageStat
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD_ROOT = ROOT / "unity-hair-salon" / "Builds"
EVIDENCE = BUILD_ROOT / "PipelineEvidence"
VIEWPORT = {"width": 844, "height": 390}
MOBILE_USER_AGENT = "Mozilla/5.0 (Linux; Android 13; HairSalonQA) AppleWebKit/537.36 Chrome/140 Mobile Safari/537.36"


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


def wait_for_canvas(page):
    page.wait_for_selector("#unity-canvas", state="visible", timeout=60000)
    page.wait_for_timeout(12000)


def new_mobile_page(browser):
    return browser.new_page(
        viewport=VIEWPORT,
        user_agent=MOBILE_USER_AGENT,
        is_mobile=True,
        has_touch=True,
        device_scale_factor=1)


def main():
    parser = argparse.ArgumentParser(description="Run deterministic browser checks for the salon WebGL builds.")
    parser.add_argument("--mode", choices=("all", "asset-lab", "demo"), default="all")
    args = parser.parse_args()
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    report = {
        "viewport": VIEWPORT,
        "assetLab": {},
        "demo": {},
        "timestamp": time.strftime("%Y-%m-%dT%H:%M:%S%z")
    }
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(headless=True)

        if args.mode in ("all", "asset-lab"):
          with serve(BUILD_ROOT / "WebGLAssetLab") as base:
            page = new_mobile_page(browser)
            errors = []
            page.on("pageerror", lambda error: errors.append(str(error)))
            page.on("console", lambda message: errors.append(message.text) if message.type == "error" else None)
            page.goto(base, wait_until="domcontentloaded", timeout=60000)
            wait_for_canvas(page)
            first = EVIDENCE / "asset-lab-844x390.png"
            page.screenshot(path=str(first))
            assert_screenshot(first)
            page.mouse.click(340, 170)
            page.wait_for_timeout(500)
            second = EVIDENCE / "asset-lab-right-wall-844x390.png"
            page.screenshot(path=str(second))
            assert_screenshot(second)
            if errors:
                raise AssertionError("资产测试场景浏览器错误：" + " | ".join(errors))
            report["assetLab"] = {"loaded": True, "screenshots": [first.name, second.name], "errors": []}
            page.close()

        if args.mode in ("all", "demo"):
          with serve(BUILD_ROOT / "WebGLDemo") as base:
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
        browser.close()

    (EVIDENCE / "browser-check.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(report, ensure_ascii=False))


if __name__ == "__main__":
    main()
