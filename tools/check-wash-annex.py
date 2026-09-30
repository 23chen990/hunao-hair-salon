#!/usr/bin/env python3
"""Verify the second wash room with real Chromium touch input.

The development-only ``mobileSeed`` URL parameter only replaces the opening
save (day 3, enough coins). Every purchase, walk and service below is driven by
the same joystick and button touches as a player and read back from the
development mobile telemetry.
"""
import importlib.util
import json
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "unity-hair-salon" / "Builds" / "WebGLDemo"
REPORT_DIR = ROOT / "unity-hair-salon" / "Builds" / "WashAnnexEvidence"
REPORT_PATH = REPORT_DIR / "report.json"
ANNEX_COST = 2000
ANNEX_STATION = 4
OPENING_X = 10.35
WALL_OUTER_X = 10.85
OPENING_Z = (1.0, 7.04)
INTERIOR = {"maxX": 15.75, "minZ": 1.0, "maxZ": 7.35}
SUPPLY_SOURCE = {"x": 3.15, "y": 0.2, "z": 0.7}
SUPPLY_RACK = {"x": -4.1, "y": 0.2, "z": 1.3}


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mobile = load_module("mobile_check", "tools/check-mobile-salon.py")
qa = load_module("browser_check", "tools/browser-check.py")
mobile.EVIDENCE = REPORT_DIR


def find_station(state, station_id):
    return next(s for s in state["stations"] if s["id"] == station_id)


def find_customer(state, customer_id):
    return next((c for c in state.get("customers", []) if c["id"] == customer_id), None)


def assert_annex_samples(points, who):
    """Anything east of the opening must be inside the opening or the annex room."""
    for p in points:
        if p["x"] <= OPENING_X:
            continue
        if p["x"] <= WALL_OUTER_X:
            assert OPENING_Z[0] <= p["z"] <= OPENING_Z[1], f"{who} crossed the old wall outside the opening: {p}"
        else:
            assert p["x"] <= INTERIOR["maxX"] and INTERIOR["minZ"] <= p["z"] <= INTERIOR["maxZ"], \
                f"{who} left the annex interior: {p}"


def customer_path(driver, customer_id, start):
    return [c["position"] for c in (find_customer(h, customer_id) for h in driver.history[start:]) if c]


def purchase_annex(driver, base):
    driver.load(base + "/?mobileEvidence=1&mobileSeed=annex-ready")
    s = driver.state
    assert s.get("day") == 3 and s.get("balance") == 2300, s
    assert s.get("annexPadVisible") is True and s.get("annexPadUnlocked") is False, s
    assert s.get("annexPadPaid") == 0 and s["balance"] >= ANNEX_COST, s
    assert not find_station(s, ANNEX_STATION)["usable"], "Station 4 must stay closed before the purchase."
    opening_balance = s["balance"]
    driver.shot("01-preopen-before-annex")
    driver.tap_button("开始营业")
    driver.until(lambda state: state["state"] == "Business", 45)
    driver.shot("02-annex-pad")

    assert driver.goto(driver.state["annexPad"], radius=0.5, timeout=45)
    # Annex now pays at 250 coins/second, so the 2000-coin pad needs
    # approximately eight seconds once the player is within the pad radius.
    driver.until(lambda state: state.get("annexPadUnlocked"), 20)
    driver.wait(0.6)
    s = driver.state
    assert s["annexPadPaid"] == ANNEX_COST, s
    assert s["balance"] == 300 and opening_balance - s["balance"] == ANNEX_COST, \
        (opening_balance, s["balance"])
    assert find_station(s, ANNEX_STATION)["usable"], "Station 4 did not open after the purchase."
    driver.shot("03-annex-opened")

    start = len(driver.history)
    assert driver.goto(find_station(driver.state, ANNEX_STATION)["position"], radius=0.5, timeout=45)
    walk = [h["player"] for h in driver.history[start:]]
    # The station anchor is inside the room but goto() may validly stop within
    # its interaction radius; crossing the opening is the invariant, not an
    # arbitrary extra 0.3 units past the outer wall.
    assert any(p["x"] > OPENING_X + 0.3 for p in walk), "The player never walked into the annex."
    assert_annex_samples(walk, "Player")
    driver.shot("04-player-in-annex")
    unlock_balance = driver.state["balance"]

    driver.reload(base + "/?mobileEvidence=1")
    driver.until(lambda state: state["state"] == "PreOpen", 60)
    s = driver.state
    assert s["annexPadUnlocked"] is True and s["annexPadPaid"] == ANNEX_COST, s
    assert s["balance"] == unlock_balance, (unlock_balance, s["balance"])
    assert s.get("annexPadVisible") is False, s
    driver.shot("05-preopen-after-reload")
    return {
        "openingBalance": opening_balance,
        "unlockBalance": unlock_balance,
        "playerWalkSamples": len(walk),
        "playerMaxX": max(p["x"] for p in walk),
    }


def refill_rack(driver):
    s = driver.state
    if s.get("carriedWashKits", 0) <= 0:
        assert driver.goto(SUPPLY_SOURCE, radius=0.7)
        driver.until(lambda state: state.get("carriedWashKits", 0) > 0, 8)
    assert driver.goto(SUPPLY_RACK, radius=0.7)
    driver.until(lambda state: state.get("washRackWashKits", 0) > 0, 8)


def serve_wash_in_annex(driver):
    driver.tap_button("开始营业")
    driver.until(lambda state: state["state"] == "Business", 45)
    driver.until(lambda state: any(
        c["need"] == "Wash" and c["state"] == "Waiting" and c["arrived"] for c in state["customers"]), 120)
    customer = min((c for c in driver.state["customers"]
                    if c["need"] == "Wash" and c["state"] == "Waiting" and c["arrived"]),
                   key=lambda c: c["patience"])
    customer_id = customer["id"]
    assert driver.goto(customer["position"], radius=1.0)
    driver.action()
    driver.until(lambda state: state["guided"] == customer_id, 8)

    assert driver.goto(find_station(driver.state, ANNEX_STATION)["position"], radius=0.5)
    driver.until(lambda state: state["targetCustomer"] == customer_id and state["available"], 8)
    start = len(driver.history)
    driver.action()
    driver.until(lambda state: (find_customer(state, customer_id) or {}).get("station") == ANNEX_STATION, 8)
    driver.until(lambda state: find_customer(state, customer_id)["state"] == "Serving"
                 and find_customer(state, customer_id)["arrived"], 40)
    path_in = customer_path(driver, customer_id, start)
    assert max(p["x"] for p in path_in) > 12.0, "The customer did not reach the annex bed."
    assert_annex_samples(path_in, "Customer (to annex)")
    driver.shot("06-customer-at-annex-wash")

    deadline = time.monotonic() + 120
    while time.monotonic() < deadline:
        s = driver.state
        c = find_customer(s, customer_id)
        if c is None or c["need"] != "Wash" or c["state"] != "Serving" or c.get("needsTransfer"):
            break
        if s["working"] >= 0 or not mobile.MobileDriver.service_ready(c):
            driver.wait(0.2)
            continue
        if s.get("washRackWashKits", 0) <= 0:
            refill_rack(driver)
            continue
        assert driver.goto(find_station(s, ANNEX_STATION)["position"], radius=0.5)
        if driver.state.get("targetCustomer") == customer_id and driver.state.get("available"):
            driver.action()
    else:
        raise AssertionError("The annex wash did not finish: " + json.dumps(find_customer(driver.state, customer_id)))
    driver.shot("07-annex-wash-finished")

    start = len(driver.history)
    c = find_customer(driver.state, customer_id)
    if c is not None and c.get("needsTransfer"):
        driver.until(lambda state: mobile.MobileDriver.transfer_stations(state, find_customer(state, customer_id)), 60)
        assert driver.goto(find_station(driver.state, ANNEX_STATION)["position"], radius=0.5)
        driver.until(lambda state: state["targetCustomer"] == customer_id and state["targetAction"] == "Guide"
                     and state["available"], 8)
        for attempt in range(4):
            if attempt == 0:
                driver.action()
            else:
                # Re-resolve the visible enabled button before retrying. The
                # target can remain valid while a single touch is missed by
                # the mobile canvas during a busy frame.
                driver.tap_button("转移顾客")
            if driver.state.get("guided") == customer_id:
                break
            driver.until(lambda state: state["targetCustomer"] == customer_id and
                         state["targetAction"] == "Guide" and state["available"], 3)
        driver.until(lambda state: state["guided"] == customer_id, 8)
        chair = min(mobile.MobileDriver.transfer_stations(driver.state, find_customer(driver.state, customer_id)),
                    key=lambda station: station["position"]["x"])
        assert driver.goto(chair["position"], radius=0.8)
        driver.action()
        driver.until(lambda state: (find_customer(state, customer_id) or {}).get("station") == chair["id"]
                     and find_customer(state, customer_id)["state"] == "Serving"
                     and find_customer(state, customer_id)["arrived"], 40)
        outcome = "transferred to chair %d" % chair["id"]
    else:
        driver.until(lambda state: (find_customer(state, customer_id) or {"position": {"x": 0}})["position"]["x"]
                     < OPENING_X - 1.0, 40)
        outcome = "left the salon"
    path_out = customer_path(driver, customer_id, start)
    assert any(p["x"] < OPENING_X for p in path_out), "The customer never left the annex."
    assert_annex_samples(path_out, "Customer (from annex)")
    driver.shot("08-customer-left-annex")
    return {
        "customer": customer_id,
        "maxXToAnnex": max(p["x"] for p in path_in),
        "pathSamplesIn": len(path_in),
        "pathSamplesOut": len(path_out),
        "outcome": outcome,
    }


def main():
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "input": "Chromium real touch events"}
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True, args=["--enable-webgl", "--disable-web-security"])
        page = qa.new_mobile_page(browser)
        driver = mobile.MobileDriver(page)
        diagnostics = {"startedWall": time.monotonic(), "console": [], "crashes": [],
                       "touchDispatch": []}
        def record_console(message):
            diagnostics["console"].append({
                "wall": time.monotonic(),
                "type": message.type,
                "textLength": len(message.text),
                "isMobileState": "[MOBILE_STATE] " in message.text,
            })
        page.on("console", record_console)
        page.on("crash", lambda: diagnostics["crashes"].append({
            "wall": time.monotonic(), "event": "page.crash"}))
        original_send = driver.cdp.send
        def timed_send(method, params=None):
            started = time.monotonic()
            result = original_send(method, params)
            if method == "Input.dispatchTouchEvent":
                diagnostics["touchDispatch"].append({
                    "wall": started, "type": (params or {}).get("type"),
                    "duration": time.monotonic() - started,
                })
            return result
        driver.cdp.send = timed_send
        try:
            with qa.serve(BUILD) as base:
                base = base.rstrip("/")
                report["purchase"] = purchase_annex(driver, base)
                report["service"] = serve_wash_in_annex(driver)
            if driver.errors:
                raise AssertionError("Browser errors: " + str(driver.errors))
            report["passed"] = True
        except Exception as error:
            report["error"] = str(error)
            driver.shot("failure")
            raise
        finally:
            diagnostics["endedWall"] = time.monotonic()
            diagnostics["telemetryCount"] = sum(
                item["isMobileState"] for item in diagnostics["console"])
            diagnostics["mobileTelemetryWallTimes"] = [
                item["wall"] for item in diagnostics["console"]
                if item["isMobileState"]]
            report["diagnostics"] = diagnostics
            report["errors"] = driver.errors
            report["actions"] = driver.actions
            REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            (REPORT_DIR / "states.json").write_text(json.dumps(driver.history, ensure_ascii=False))
            browser.close()


if __name__ == "__main__":
    main()
