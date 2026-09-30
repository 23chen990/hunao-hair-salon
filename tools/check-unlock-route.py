#!/usr/bin/env python3
"""Real-touch checks for the day-gated salon expansion route."""
import argparse
import importlib.util
import json
import math
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "unity-hair-salon" / "Builds" / "WebGLDemo"
EVIDENCE = ROOT / "artifacts" / "unlock-route"


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mobile = load_module("mobile_check", "tools/check-mobile-salon.py")
qa = load_module("browser_check", "tools/browser-check.py")
mobile.EVIDENCE = EVIDENCE


def pad(state, pad_id):
    return next((item for item in state.get("pads", [])
                 if item.get("id") == pad_id), None)


def visible_pad(state, pad_id):
    item = pad(state, pad_id)
    assert item is not None, "Missing pad telemetry: " + pad_id
    assert item.get("visible") is True, "Pad is not visible: " + json.dumps(item, ensure_ascii=False)
    return item


def player_inside_obstacle(state):
    player = state.get("player") or {}
    x, z = player.get("x"), player.get("z")
    if x is None or z is None:
        return None
    for rect in state.get("obstacles") or []:
        if (rect["x"] <= x < rect["x"] + rect["width"] and
                rect["y"] <= z < rect["y"] + rect["height"]):
            return rect
    return None


def assert_player_free_after_build(driver, label):
    """New furniture appears on its pad; the player standing there must be
    nudged out instead of staying trapped inside the collision."""
    driver.wait(.6)
    rect = player_inside_obstacle(driver.state)
    assert rect is None, "%s trapped the player inside %s at %s" % (
        label, json.dumps(rect), json.dumps(driver.state.get("player")))
    return driver.state.get("player")


PAD_RADIUS = 1.25


def stand_on_pad(driver, position):
    """goto drops waypoints within 0.42 of the player, so it can stop that far
    short of its radius; aim well inside the pad's 1.25 interaction radius."""
    for radius in (.6, .35):
        assert driver.goto(position, radius=radius, timeout=45)
        player = driver.state["player"]
        if math.dist((player["x"], player["z"]), (position["x"], position["z"])) <= PAD_RADIUS - .1:
            return
    raise AssertionError("Could not stand on pad %s from %s" % (
        json.dumps(position), json.dumps(driver.state.get("player"))))


def run_day2(driver, base):
    driver.load(base + "/?mobileEvidence=1&mobileSeed=route-day2")
    assert driver.state.get("day") == 2 and driver.state.get("balance") == 900, driver.state
    seats = visible_pad(driver.state, "WaitingSeats")
    assert driver.state.get("waitingSeats") is False, driver.state
    driver.tap_button("开始营业")
    driver.until(lambda state: state.get("state") == "Business", 45)
    driver.until(lambda state: any(c.get("state") == "Waiting" and c.get("arrived")
                                   for c in state.get("customers", [])), 60)
    driver.shot("02-standing-queue")
    opening_balance = driver.state["balance"]
    stand_on_pad(driver, seats["position"])
    driver.until(lambda state: state.get("waitingSeats") is True
                 and state.get("balance") == opening_balance - 400, 20)
    player_after = assert_player_free_after_build(driver, "WaitingSeats")
    driver.shot("03-seats-built")
    assert any(c.get("state") == "Waiting" for c in driver.state.get("customers", [])), \
        "Waiting customer disappeared when seats were built"
    return {
        "seed": "route-day2",
        "assertions": ["WaitingSeats visible before purchase", "queue stands before seats",
                       "WaitingSeats costs 400", "waiting customer remains after purchase"],
        "balanceBefore": opening_balance,
        "balanceAfter": driver.state["balance"],
        "playerAfterBuild": player_after,
        "waitingCapacity": driver.state.get("waitingCapacity"),
    }


def run_day4(driver, base):
    driver.load(base + "/?mobileEvidence=1&mobileSeed=route-day4")
    assert driver.state.get("day") == 4 and driver.state.get("balance") == 1800, driver.state
    blow = visible_pad(driver.state, "BlowStand")
    assert driver.state.get("purchased") is False, driver.state
    driver.tap_button("开始营业")
    driver.until(lambda state: state.get("state") == "Business", 45)
    opening_balance = driver.state["balance"]
    stand_on_pad(driver, blow["position"])
    driver.until(lambda state: state.get("purchased") is True
                 and state.get("balance") == opening_balance - 1500, 20)
    player_after = assert_player_free_after_build(driver, "BlowStand")
    driver.shot("05-blow-stand-built")
    assert not pad(driver.state, "BlowStand").get("visible"), driver.state
    return {
        "seed": "route-day4",
        "assertions": ["BlowStand costs 1500", "auto-blow stand unlocks on day 4",
                       "BlowStand pad becomes hidden"],
        "balanceBefore": opening_balance,
        "balanceAfter": driver.state["balance"],
        "playerAfterBuild": player_after,
        "purchased": driver.state.get("purchased"),
    }


def run_day5(driver, base):
    driver.load(base + "/?mobileEvidence=1&mobileSeed=route-day5")
    assert driver.state.get("day") == 5 and driver.state.get("balance") == 1400, driver.state
    extra = visible_pad(driver.state, "ExtraSeats")
    assert driver.state.get("extraSeats") is False, driver.state
    driver.tap_button("开始营业")
    driver.until(lambda state: state.get("state") == "Business", 45)
    opening_balance = driver.state["balance"]
    stand_on_pad(driver, extra["position"])
    driver.until(lambda state: state.get("extraSeats") is True
                 and state.get("waitingCapacity") == 6
                 and state.get("maxCustomers") == 8
                 and state.get("balance") == opening_balance - 1200, 20)
    player_after = assert_player_free_after_build(driver, "ExtraSeats")
    driver.shot("07-bench-built")
    maximum_waiting = 0
    deadline = time.monotonic() + 120
    while time.monotonic() < deadline and driver.state.get("state") in ("Business", "ClosingGrace"):
        maximum_waiting = max(maximum_waiting, sum(
            c.get("state") == "Waiting" for c in driver.state.get("customers", [])))
        driver.wait(.2)
    maximum_waiting = max(maximum_waiting, max((
        sum(c.get("state") == "Waiting" for c in state.get("customers", []))
        for state in driver.history), default=0))
    return {
        "seed": "route-day5",
        "assertions": ["ExtraSeats costs 1200", "capacity becomes 6",
                       "maxCustomers becomes 8", "observe waiting peak without failing"],
        "balanceBefore": opening_balance,
        "balanceAfter": driver.state["balance"],
        "playerAfterBuild": player_after,
        "extraSeats": driver.state.get("extraSeats"),
        "waitingCapacity": driver.state.get("waitingCapacity"),
        "maxCustomers": driver.state.get("maxCustomers"),
        "maxWaitingObserved": maximum_waiting,
    }


def main(argv=None):
    parser = argparse.ArgumentParser(description="Unlock-route real-touch QA")
    parser.add_argument("--scenario", choices=("all", "route-day2", "route-day4", "route-day5"),
                        default="all")
    args = parser.parse_args(argv)
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    requested = ("route-day2", "route-day4", "route-day5") if args.scenario == "all" else (args.scenario,)
    report = {"passed": False, "input": "Chromium real touch events", "scenarios": {}}
    console_lengths = []
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True,
            args=["--enable-webgl", "--disable-web-security"])
        page = qa.new_mobile_page(browser)
        page.on("console", lambda message: console_lengths.append({
            "text": message.text, "length": len(message.text)}))
        driver = mobile.MobileDriver(page)
        try:
            with qa.serve(BUILD) as base:
                for scenario in requested:
                    start = len(console_lengths)
                    driver.history = []
                    driver.errors = []
                    result = {
                        "route-day2": run_day2,
                        "route-day4": run_day4,
                        "route-day5": run_day5,
                    }[scenario](driver, base.rstrip("/"))
                    state_lengths = [
                        item["length"] for item in console_lengths[start:]
                        if "[MOBILE_STATE] " in item["text"]
                    ]
                    assert state_lengths, "No MOBILE_STATE telemetry for " + scenario
                    result["maxMobileStateLineLength"] = max(state_lengths)
                    result["driverErrors"] = list(driver.errors)
                    assert not driver.errors, "Browser errors: " + str(driver.errors)
                    report["scenarios"][scenario] = result
                    (EVIDENCE / (scenario + "-states.json")).write_text(
                        json.dumps(driver.history, ensure_ascii=False), encoding="utf-8")
            report["passed"] = True
        except Exception as error:
            report["error"] = str(error)
            driver.shot("failure")
            raise
        finally:
            report["driverErrors"] = driver.errors
            report["consoleLineLengths"] = [
                item["length"] for item in console_lengths if "[MOBILE_STATE] " in item["text"]
            ]
            (EVIDENCE / "report.json").write_text(
                json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            browser.close()


if __name__ == "__main__":
    main()
