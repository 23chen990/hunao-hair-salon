#!/usr/bin/env python3
"""Verify the salon construction pad with real Chromium touch input.

This check deliberately reads only the development mobile telemetry and sends
the same joystick/button touches as a player. It does not call Unity gameplay
APIs or inject a wallet/save state.
"""
import importlib.util
import json
import math
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "unity-hair-salon" / "Builds" / "WebGLDemo"
REPORT_DIR = ROOT / "unity-hair-salon" / "Builds" / "ProximityPadEvidence"
REPORT_PATH = REPORT_DIR / "report.json"
PAD = {"x": -4.25, "y": 0.2, "z": 1.1}
FAR_POINT = {"x": 0.0, "y": 0.2, "z": -3.0}
SOURCE = {"x": 8.35, "y": 0.2, "z": 0.7}
ORIGINAL_RACK = {"x": -1.25, "y": 0.2, "z": 4.6}
EXPANDED_RACK = {"x": -4.1, "y": 0.2, "z": 1.3}


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mobile = load_module("mobile_check", "tools/check-mobile-salon.py")
qa = load_module("browser_check", "tools/browser-check.py")


def complete_haircut(driver, customer_id, timeout=45):
    driver.until(
        lambda state: any(
            customer.get("id") == customer_id
            and customer.get("need") == "Cut"
            and customer.get("state") == "Waiting"
            and customer.get("arrived")
            for customer in state.get("customers", [])
        ),
        timeout,
    )
    customer = next(customer for customer in driver.state["customers"] if customer["id"] == customer_id)
    assert driver.goto(customer["position"], radius=1.0)
    driver.action()
    driver.until(lambda state: state.get("guided") == customer_id, 8)

    station = next(
        station
        for station in driver.state["stations"]
        if station.get("type") == "Haircut" and not station.get("occupied")
    )
    assert driver.goto(station["position"], radius=1.0)
    driver.action()
    driver.until(lambda state: state.get("targetAction") == "Cut" and state.get("available"), 8)

    # Mobile orders can require scissors followed by thinning shears or
    # clippers. Keep resolving the visible action until the order leaves Cut.
    while driver.state.get("targetAction") == "Cut" and driver.state.get("available"):
        hold = 0.95 if driver.state.get("targetTool") == "Clippers" else 1.8
        driver.hold_action(hold)


def measure_walk(driver, target, radius=0.55, timeout=45):
    """Measure a real touch walk from telemetry samples, not a teleport."""
    start_index = len(driver.history)
    started = time.monotonic()
    start_point = (driver.state["player"]["x"], driver.state["player"]["z"])
    planned = driver.route(target, radius)
    planned_points = [(driver.state["player"]["x"], driver.state["player"]["z"])] + planned
    planned_distance = sum(
        math.dist(a, b) for a, b in zip(planned_points, planned_points[1:])
    )
    assert driver.goto(target, radius=radius, timeout=timeout)
    samples = driver.history[start_index:]
    points = [start_point] + [
        (sample["player"]["x"], sample["player"]["z"])
        for sample in samples
    ]
    if not points:
        points = [(driver.state["player"]["x"], driver.state["player"]["z"])]
    measured_distance = sum(math.dist(a, b) for a, b in zip(points, points[1:]))
    return {
        "plannedPathDistance": planned_distance,
        "measuredTelemetryDistance": measured_distance,
        "wallSeconds": time.monotonic() - started,
        "samples": len(points),
    }


def deliver_one_from_source(driver, rack, timeout=45):
    """Use the same automatic pickup/dropoff loop as a player."""
    assert measure_walk(driver, SOURCE, radius=0.55, timeout=timeout)
    driver.wait(0.65)
    assert driver.state.get("carriedWashKits", 0) > 0, driver.state
    before = driver.state.get("washRackWashKits", 0)
    route = measure_walk(driver, rack, radius=0.55, timeout=timeout)
    wait_started = time.monotonic()
    driver.until(lambda state: state.get("washRackWashKits", 0) > before, 8)
    route["unloadWaitSeconds"] = time.monotonic() - wait_started
    route["unloadCount"] = 1
    return route


def measure_supply_cycle(driver, rack, wash_station, timeout=45, shot_name=None):
    """Measure source -> rack -> wash anchor with real joystick input."""
    first = deliver_one_from_source(driver, rack, timeout)
    if shot_name:
        driver.shot(shot_name)
    second = measure_walk(driver, wash_station, radius=0.75, timeout=timeout)
    return {
        "sourceToRack": first,
        "rackToWash": second,
        "pathDistance": first["measuredTelemetryDistance"] + second["measuredTelemetryDistance"],
        "walkWallSeconds": first["wallSeconds"] + second["wallSeconds"],
        "unloadWaitSeconds": first["unloadWaitSeconds"],
        "unloadCount": first["unloadCount"],
    }


def capture_failure_snapshot(driver):
    """Copy the state at the Result screen before any retry input is sent."""
    state = driver.state
    return {
        "failedCompleted": state.get("completed"),
        "failedTarget": state.get("target"),
        "failedBalance": state.get("balance"),
        "failedPaid": state.get("padPaid"),
        "failedUnlocked": state.get("padUnlocked"),
        "failedSupply": {
            "sourceWashKits": state.get("sourceWashKits"),
            "carriedWashKits": state.get("carriedWashKits"),
            "washRackWashKits": state.get("washRackWashKits"),
        },
    }


def main():
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    report = {"passed": False, "input": "Chromium real touch events", "errors": []}
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True, args=["--enable-webgl", "--disable-web-security"]
        )
        page = qa.new_mobile_page(browser)
        driver = mobile.MobileDriver(page)
        try:
            with qa.serve(BUILD) as base:
                driver.load(base.rstrip("/") + "/?mobileEvidence=1")
                assert driver.state.get("padPaid") == 0
                assert driver.state.get("padUnlocked") is False
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)

                complete_haircut(driver, 0)
                driver.until(lambda state: state.get("balance", 0) > 0, 15)
                first_balance = driver.state["balance"]

                # The deterministic Day 1 sequence keeps O002 as a wash order;
                # O003 is the next cut order and supplies the second payment.
                if first_balance < 180:
                    complete_haircut(driver, 2, timeout=45)
                    driver.until(lambda state: state.get("balance", 0) >= 180, 20)
                earned_balance = driver.state["balance"]
                assert earned_balance >= 180

                # Use a tight stopping radius so the avatar is reliably inside
                # the gameplay trigger circle after walking from the salon.
                assert driver.goto(PAD, radius=0.5, timeout=45)
                driver.wait(1.1)
                partial_paid = driver.state.get("padPaid")
                assert 0 < partial_paid < 180, driver.state
                assert driver.state.get("balance", 0) < earned_balance

                # The player may still pay for the short interval while walking
                # out of the circle. Once outside, the amount must stay fixed.
                assert driver.goto(FAR_POINT, radius=1.0, timeout=45)
                driver.until(
                    lambda state: math.dist(
                        (state["player"]["x"], state["player"]["z"]), (PAD["x"], PAD["z"])
                    ) > 2.0,
                    12,
                )
                paid_at_exit = driver.state.get("padPaid")
                partial_balance = driver.state.get("balance")
                driver.wait(0.5)
                assert driver.state.get("padPaid") == paid_at_exit

                # Leaving the pad is a safe checkpoint boundary. A page
                # refresh must restore the same balance/Paid pair while still
                # entering the day at PreOpen.
                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                assert driver.state.get("padUnlocked") is False
                assert driver.state.get("padPaid") == paid_at_exit
                assert driver.state.get("balance") == partial_balance
                partial_reload_paid = driver.state.get("padPaid")
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)

                # Measure the pre-purchase route from the saved partial-build
                # checkpoint. Reloading again below resets temporary stock to
                # the same opening values before the post-purchase run.
                wash_station = next(
                    station for station in driver.state["stations"]
                    if station.get("type") == "Wash"
                )["position"]
                assert driver.goto(wash_station, radius=0.5, timeout=45)
                stock_before_route = {
                    key: driver.state.get(key) for key in
                    ("sourceWashKits", "carriedWashKits", "washRackWashKits")
                }
                before_route = measure_supply_cycle(
                    driver, ORIGINAL_RACK, wash_station, shot_name="r1-route-before")

                # The partial payment is a saved checkpoint, so this reload
                # restores both the same BUILD progress and fresh temporary
                # supply stock before completing the construction.
                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                route_start_paid = driver.state.get("padPaid")
                assert 0 <= route_start_paid <= 180
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)

                assert driver.goto(PAD, radius=0.5, timeout=45)
                deadline = time.monotonic() + 10
                while time.monotonic() < deadline and not driver.state.get("padUnlocked"):
                    driver.wait(0.2)
                assert driver.state.get("padUnlocked") is True, driver.state
                assert driver.state.get("padPaid") == 180, driver.state
                unlock_balance = driver.state.get("balance")

                # The second reload above reset the day's temporary stock to
                # its opening state. Start at the same wash anchor before
                # measuring the newly unlocked unload entrance.
                assert driver.goto(wash_station, radius=0.5, timeout=45)
                stock_after_route = {
                    key: driver.state.get(key) for key in
                    ("sourceWashKits", "carriedWashKits", "washRackWashKits")
                }
                assert stock_after_route == stock_before_route, (
                    stock_before_route, stock_after_route)
                after_route = measure_supply_cycle(
                    driver, EXPANDED_RACK, wash_station, shot_name="r1-route-after")
                driver.shot("r1-route-value")

                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                assert driver.state.get("padUnlocked") is True
                assert driver.state.get("padPaid") == 180
                purchased_opening_balance = driver.state.get("balance")

                # A purchased rack is part of the immutable DayOpening. Let
                # the next day fail without serving anyone, then use the
                # actual retry button and verify the built rack remains.
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Result", 260)
                assert driver.state.get("completed", 0) < driver.state.get("target", 1)
                purchased_failure = capture_failure_snapshot(driver)
                driver.tap_button("再试一次")
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                assert driver.state.get("padUnlocked") is True
                assert driver.state.get("padPaid") == 180
                assert driver.state.get("balance") == purchased_opening_balance
                purchased_retry = {
                    **purchased_failure,
                    "retryCompleted": driver.state.get("completed"),
                    "retryTarget": driver.state.get("target"),
                    "retryBalance": driver.state.get("balance"),
                    "retryUnlocked": driver.state.get("padUnlocked"),
                    "retryPaid": driver.state.get("padPaid"),
                    "retrySupply": {
                        "sourceWashKits": driver.state.get("sourceWashKits"),
                        "carriedWashKits": driver.state.get("carriedWashKits"),
                        "washRackWashKits": driver.state.get("washRackWashKits"),
                    },
                }
                report.update(
                    {
                        "passed": True,
                        "earnedBalance": earned_balance,
                        "partialPaid": partial_paid,
                        "paidAtExit": paid_at_exit,
                        "partialReloadPaid": partial_reload_paid,
                        "partialReloadBalance": partial_balance,
                        "routeStartPaid": route_start_paid,
                        "unlockBalance": unlock_balance,
                        "reloadUnlocked": driver.state.get("padUnlocked"),
                        "reloadPaid": driver.state.get("padPaid"),
                        "purchasedFailureRetry": purchased_retry,
                        "routeMeasurement": {
                            "startStockBeforePurchase": stock_before_route,
                            "startStockAfterPurchase": stock_after_route,
                            "beforePurchase": before_route,
                            "afterPurchase": after_route,
                            "pathDistanceDelta": after_route["pathDistance"] - before_route["pathDistance"],
                            "walkWallSecondsDelta": after_route["walkWallSeconds"] - before_route["walkWallSeconds"],
                            "unloadCountDelta": after_route["unloadCount"] - before_route["unloadCount"],
                        },
                    }
                )
            if driver.errors:
                raise AssertionError("Browser errors: " + str(driver.errors))
        except Exception as error:
            report["error"] = str(error)
            raise
        finally:
            report["errors"] = driver.errors
            REPORT_PATH.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            browser.close()


if __name__ == "__main__":
    main()
