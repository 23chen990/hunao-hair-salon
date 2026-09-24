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
                driver.wait(0.5)
                assert driver.state.get("padPaid") == paid_at_exit

                assert driver.goto(PAD, radius=0.5, timeout=45)
                deadline = time.monotonic() + 10
                while time.monotonic() < deadline and not driver.state.get("padUnlocked"):
                    driver.wait(0.2)
                assert driver.state.get("padUnlocked") is True, driver.state
                assert driver.state.get("padPaid") == 180, driver.state
                unlock_balance = driver.state.get("balance")

                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                assert driver.state.get("padUnlocked") is True
                assert driver.state.get("padPaid") == 180
                report.update(
                    {
                        "passed": True,
                        "earnedBalance": earned_balance,
                        "partialPaid": partial_paid,
                        "paidAtExit": paid_at_exit,
                        "unlockBalance": unlock_balance,
                        "reloadUnlocked": driver.state.get("padUnlocked"),
                        "reloadPaid": driver.state.get("padPaid"),
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
