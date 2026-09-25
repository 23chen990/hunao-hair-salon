#!/usr/bin/env python3
"""Exercise the approved R1 retry baselines through the real WebGL touch path.

The script deliberately uses only Chromium touch/joystick input and the
development telemetry already exposed by the formal mobile build. It never
writes wallet, pad, inventory, phase or save fields directly.
"""
import json
import importlib.util
import time
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location(
    "check_proximity_pad", ROOT / "tools" / "check-proximity-pad.py"
)
proximity = importlib.util.module_from_spec(spec)
spec.loader.exec_module(proximity)

BUILD = proximity.BUILD
FAR_POINT = proximity.FAR_POINT
PAD = proximity.PAD
complete_haircut = proximity.complete_haircut
capture_failure_snapshot = proximity.capture_failure_snapshot
qa = proximity.qa
mobile = proximity.mobile
REPORT_DIR = ROOT / "unity-hair-salon" / "Builds" / "R1RollbackEvidence"
REPORT_PATH = REPORT_DIR / "report.json"


def state_snapshot(driver, prefix):
    state = driver.state
    return {
        f"{prefix}Phase": state.get("state"),
        f"{prefix}Day": state.get("day"),
        f"{prefix}Balance": state.get("balance"),
        f"{prefix}Paid": state.get("padPaid"),
        f"{prefix}Unlocked": state.get("padUnlocked"),
        f"{prefix}Supply": {
            "sourceWashKits": state.get("sourceWashKits"),
            "carriedWashKits": state.get("carriedWashKits"),
            "washRackWashKits": state.get("washRackWashKits"),
        },
    }


def failure_and_retry(driver, label):
    driver.until(lambda state: state.get("state") == "Result", 260)
    failed = capture_failure_snapshot(driver)
    failed["failedPhase"] = driver.state.get("state")
    driver.tap_button("再试一次")
    driver.until(lambda state: state.get("state") == "PreOpen", 60)
    retry = {
        "retryPhase": driver.state.get("state"),
        "retryDay": driver.state.get("day"),
        "retryBalance": driver.state.get("balance"),
        "retryPaid": driver.state.get("padPaid"),
        "retryUnlocked": driver.state.get("padUnlocked"),
        "retrySupply": {
            "sourceWashKits": driver.state.get("sourceWashKits"),
            "carriedWashKits": driver.state.get("carriedWashKits"),
            "washRackWashKits": driver.state.get("washRackWashKits"),
        },
    }
    return {"label": label, **failed, **retry}


def earn_legal_balance(driver, minimum=240):
    """Earn a legal pre-opening balance by completing visible Cut orders."""
    completed_ids = set()
    for _ in range(5):
        if driver.state.get("balance", 0) >= minimum:
            return
        driver.until(
            lambda state: any(
                customer.get("id") not in completed_ids
                and customer.get("need") == "Cut"
                and customer.get("state") == "Waiting"
                and customer.get("arrived")
                for customer in state.get("customers", [])
            ),
            60,
        )
        customer_id = next(
            customer["id"] for customer in driver.state.get("customers", [])
            if customer.get("id") not in completed_ids
            and customer.get("need") == "Cut"
            and customer.get("state") == "Waiting"
            and customer.get("arrived")
        )
        complete_haircut(driver, customer_id, timeout=60)
        completed_ids.add(customer_id)
        driver.until(lambda state: state.get("balance", 0) > 0, 15)
    assert driver.state.get("balance", 0) >= minimum, driver.state


def main():
    REPORT_DIR.mkdir(parents=True, exist_ok=True)
    report = {
        "passed": False,
        "input": "Chromium real touch events",
        "build": str(BUILD),
        "errors": [],
    }
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True,
            args=["--enable-webgl", "--disable-web-security"],
        )
        page = qa.new_mobile_page(browser)
        driver = mobile.MobileDriver(page)
        try:
            with qa.serve(BUILD) as base:
                driver.load(base.rstrip("/") + "/?mobileEvidence=1")
                assert driver.state.get("state") == "PreOpen"
                assert driver.state.get("padPaid") == 0

                # A: start a real attempt, partially pay, do not refresh, let
                # the day fail, and prove retry returns to that DayOpening.
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)
                earn_legal_balance(driver)
                assert driver.goto(PAD, radius=0.5, timeout=45)
                driver.wait(0.08)
                assert driver.goto(FAR_POINT, radius=1.0, timeout=45)
                initial_save = state_snapshot(driver, "initialSave")
                print("R1 initialSave", json.dumps(initial_save, ensure_ascii=False), flush=True)
                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)

                # A starts from that valid saved prep state. This makes the
                # later spend a real change against a non-zero DayOpening.
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)
                opening_a = state_snapshot(driver, "openingA")
                print("R1 openingA", json.dumps(opening_a, ensure_ascii=False), flush=True)
                assert opening_a["openingAPaid"] == initial_save["initialSavePaid"]
                assert opening_a["openingABalance"] == initial_save["initialSaveBalance"]
                assert driver.goto(PAD, radius=0.5, timeout=45)
                driver.wait(0.08)
                changed_a = state_snapshot(driver, "changedA")
                print("R1 changedA", json.dumps(changed_a, ensure_ascii=False), flush=True)
                assert 0 < changed_a["changedAPaid"] < 180
                assert changed_a["changedAPaid"] > opening_a["openingAPaid"]
                assert changed_a["changedABalance"] < opening_a["openingABalance"]
                assert driver.goto(FAR_POINT, radius=1.0, timeout=45)
                a_result = failure_and_retry(driver, "A_no_refresh_partial")
                assert a_result["retryBalance"] == opening_a["openingABalance"]
                assert a_result["retryPaid"] == opening_a["openingAPaid"]
                assert a_result["retryUnlocked"] == opening_a["openingAUnlocked"]
                assert a_result["retrySupply"] == opening_a["openingASupply"]
                assert a_result["failedPaid"] >= changed_a["changedAPaid"]
                assert a_result["failedPaid"] < 180

                # B: save partial progress, refresh into PreOpen, start a new
                # attempt from that state, finish the purchase, then fail. The
                # retry target must be the refreshed/new DayOpening.
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)
                assert driver.goto(PAD, radius=0.5, timeout=45)
                driver.wait(0.08)
                assert 0 < driver.state.get("padPaid", 0) < 180
                assert driver.goto(FAR_POINT, radius=1.0, timeout=45)
                saved_before_refresh = state_snapshot(driver, "savedBeforeRefresh")
                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                refresh_state = state_snapshot(driver, "refresh")
                assert refresh_state["refreshPaid"] == saved_before_refresh["savedBeforeRefreshPaid"]
                assert refresh_state["refreshBalance"] == saved_before_refresh["savedBeforeRefreshBalance"]
                assert refresh_state["refreshPhase"] == "PreOpen"

                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", 45)
                opening_b = state_snapshot(driver, "openingB")
                assert opening_b["openingBPaid"] == refresh_state["refreshPaid"]
                assert opening_b["openingBBalance"] == refresh_state["refreshBalance"]
                assert driver.goto(PAD, radius=0.5, timeout=45)
                deadline = time.monotonic() + 15
                while time.monotonic() < deadline and not driver.state.get("padUnlocked"):
                    driver.wait(0.2)
                changed_b = state_snapshot(driver, "changedB")
                assert changed_b["changedBUnlocked"] is True, driver.state
                assert changed_b["changedBPaid"] == 180
                b_result = failure_and_retry(driver, "B_refresh_new_baseline_full_purchase")
                assert b_result["retryBalance"] == opening_b["openingBBalance"]
                assert b_result["retryPaid"] == opening_b["openingBPaid"]
                assert b_result["retryUnlocked"] == opening_b["openingBUnlocked"]
                assert b_result["failedPaid"] == changed_b["changedBPaid"]
                assert b_result["failedBalance"] == changed_b["changedBBalance"]

                # D: after failure handling, a refresh must load the post-failure
                # valid save (the same state just verified by retry).
                driver.reload()
                driver.until(lambda state: state.get("state") == "PreOpen", 60)
                after_failure_refresh = state_snapshot(driver, "afterFailureRefresh")
                assert after_failure_refresh["afterFailureRefreshBalance"] == b_result["retryBalance"]
                assert after_failure_refresh["afterFailureRefreshPaid"] == b_result["retryPaid"]
                assert after_failure_refresh["afterFailureRefreshUnlocked"] == b_result["retryUnlocked"]
                assert after_failure_refresh["afterFailureRefreshSupply"] == b_result["retrySupply"]

                report.update({
                    "passed": True,
                    "initialSave": initial_save,
                    "A": {"opening": opening_a, "changed": changed_a, "result": a_result},
                    "B": {
                        "savedBeforeRefresh": saved_before_refresh,
                        "refresh": refresh_state,
                        "opening": opening_b,
                        "changed": changed_b,
                        "result": b_result,
                    },
                    "D": {"afterFailureRefresh": after_failure_refresh},
                })
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
