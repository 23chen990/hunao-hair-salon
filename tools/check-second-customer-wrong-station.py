#!/usr/bin/env python3
"""Real Chromium touch check for the Day 1 O002 wrong-station mistake path."""
import argparse
import importlib.util
import json
import math
import sys
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD_ROOT = ROOT / "unity-hair-salon" / "Builds"
EVIDENCE = BUILD_ROOT / "MobileEvidenceWrongStation"
VIEWPORT = {"width": 844, "height": 390}
MOBILE_USER_AGENT = (
    "Mozilla/5.0 (Linux; Android 13; HairSalonQA) AppleWebKit/537.36 "
    "Chrome/140 Mobile Safari/537.36"
)

spec = importlib.util.spec_from_file_location(
    "salon_mobile_driver", ROOT / "tools" / "check-mobile-salon.py"
)
mobile = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mobile)
qa = mobile.qa
mobile.EVIDENCE = EVIDENCE


def customer(state, customer_id):
    return next((item for item in state.get("customers", [])
                 if item.get("id") == customer_id), None)


def interaction_button(state):
    label = state.get("action", "")
    return next((button for button in state.get("buttons", [])
                 if button.get("label") == label), None)


def route_distance(driver, station):
    route = [(driver.state["player"]["x"], driver.state["player"]["z"])] + \
        driver.route(station["position"], radius=1.2)
    return sum(math.dist(a, b) for a, b in zip(route, route[1:]))


def assert_target(driver, customer_id, action, label, available=True):
    state = driver.state
    assert state.get("targetCustomer") == customer_id, \
        f"Expected customer {customer_id} target, got {MobileDriverSnapshot(state)}"
    assert state.get("targetAction") == action, \
        f"Expected {action} action, got {MobileDriverSnapshot(state)}"
    assert state.get("action") == label, \
        f"Expected interaction label {label!r}, got {MobileDriverSnapshot(state)}"
    assert state.get("available") is available, \
        f"Expected available={available}, got {MobileDriverSnapshot(state)}"
    button = interaction_button(state)
    assert button is not None and button.get("label") == label and \
        button.get("available") is available, \
        f"Rendered touch button does not match the action target: {MobileDriverSnapshot(state)}"


def MobileDriverSnapshot(state):
    return json.dumps({key: state.get(key) for key in
                       ("guided", "targetCustomer", "targetAction", "action", "available",
                        "player", "customers", "stations", "buttons")},
                      ensure_ascii=False)


def run(url, evidence_dir):
    global EVIDENCE
    EVIDENCE = evidence_dir.resolve()
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    mobile.EVIDENCE = EVIDENCE
    errors = []
    driver = None
    result = {
        "passed": False,
        "scenario": "Day 1 O002 wash customer: assign to free haircut station, observe mistake, correct to wash",
        "viewport": VIEWPORT,
        "input": "Chromium CDP real touch events",
    }

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True,
            args=["--enable-webgl", "--disable-web-security"],
        )
        page = qa.new_mobile_page(browser)
        def record_error(value):
            # Chromium headless has no orientation sensor; this optional lock
            # capability warning is already filtered by the shared driver.
            if "screen.orientation.lock() is not available" not in value:
                errors.append(value)

        page.on("pageerror", lambda error: record_error(str(error)))
        page.on("console", lambda message: record_error(message.text)
                if message.type == "error" else None)
        driver = mobile.MobileDriver(page)
        try:
            driver.load(url.rstrip("/") + "/?mobileEvidence=1")
            driver.shot("01-opening")
            assert driver.state.get("day") == 1, \
                f"Expected Day 1, got {driver.state.get('day')}"

            driver.tap_button("开始营业")
            driver.until(lambda state: state.get("state") == "Business", timeout=20)
            driver.until(lambda state: customer(state, 1) is not None and
                         customer(state, 1).get("state") == "Waiting" and
                         customer(state, 1).get("arrived") and
                         customer(state, 1).get("need") == "Wash", timeout=45)

            # Day 1 spawn id 1 is deterministically O002. Verify its runtime
            # projection before sending any gameplay input to it.
            second = customer(driver.state, 1)
            assert second["need"] == "Wash", f"O002 must begin with Wash: {second}"
            assert len([station for station in driver.state["stations"]
                        if station.get("type") == "Haircut" and not station.get("occupied")]) >= 1, \
                "No free haircut station exists for the intended mistaken assignment."
            driver.shot("02-o002-waiting")

            assert driver.goto(second["position"], radius=1.0), \
                "Could not reach the second customer in the real touch run."
            driver.until(lambda state: state.get("targetCustomer") == 1 and
                         state.get("targetAction") == "Greet" and state.get("available"), timeout=8)
            assert_target(driver, 1, "Greet", "接待 2 号")
            greet_snapshot = {key: driver.state.get(key) for key in
                              ("targetCustomer", "targetAction", "action", "available", "player")}
            driver.shot("03-o002-greet-ready")
            greet_action_index = len(driver.actions)
            driver.action()
            greet_touch = driver.actions[greet_action_index]
            assert greet_touch.get("targetCustomer") == 1 and greet_touch.get("targetAction") == "Greet", \
                f"The real touch did not execute O002 Greet: {greet_touch}"
            driver.until(lambda state: state.get("guided") == 1 and
                         state.get("targetCustomer") == 1 and
                         state.get("targetAction") == "Assign", timeout=8)
            assert "洗发工位" in driver.state.get("action", ""), \
                f"The next step did not identify O002's wash need: {MobileDriverSnapshot(driver.state)}"

            haircut_stations = [station for station in driver.state["stations"]
                                if station.get("type") == "Haircut" and
                                not station.get("occupied")]
            haircut_stations.sort(key=lambda station: route_distance(driver, station))
            wrong_station = None
            for station in haircut_stations:
                if driver.goto(station["position"], radius=1.2):
                    player = driver.state["player"]
                    if math.dist((player["x"], player["z"]),
                                 (station["position"]["x"], station["position"]["z"])) <= 1.5:
                        if driver.state.get("guided") == 1 and \
                                driver.state.get("targetCustomer") == 1 and \
                                driver.state.get("targetAction") == "Assign" and \
                                driver.state.get("available") and \
                                driver.state.get("action") == "安排剪发工位":
                            wrong_station = station
                            break
            assert wrong_station is not None, \
                "The player could not reach a free haircut chair with O002 guided."
            assert_target(driver, 1, "Assign", "安排剪发工位")
            before_wrong = customer(driver.state, 1)
            before_wrong_satisfaction = before_wrong.get("satisfaction")
            assert before_wrong_satisfaction is not None, \
                f"Customer satisfaction is missing from telemetry: {before_wrong}"
            shop_satisfaction_before_wrong = driver.state.get("satisfaction")
            wrong_ready = {key: driver.state.get(key) for key in
                           ("targetCustomer", "targetAction", "action", "available", "player")}
            driver.shot("04-wrong-haircut-station-ready")

            wrong_action_index = len(driver.actions)
            driver.action()
            wrong_touch = driver.actions[wrong_action_index]
            assert wrong_touch.get("targetCustomer") == 1 and \
                wrong_touch.get("targetAction") == "Assign", \
                f"The real touch did not execute customer 2's wrong-station assignment: {wrong_touch}"
            driver.until(lambda state: customer(state, 1) is not None and
                         customer(state, 1).get("station") == wrong_station["id"] and
                         customer(state, 1).get("wrongStationCount") == 1 and
                         customer(state, 1).get("reactionKind") == "Confused", timeout=5)
            after_wrong = customer(driver.state, 1)
            after_wrong_satisfaction = after_wrong.get("satisfaction")
            shop_satisfaction_after_wrong = driver.state.get("satisfaction")
            assert after_wrong_satisfaction is not None and \
                after_wrong_satisfaction < before_wrong_satisfaction, \
                f"The wrong-station mistake did not lower O002 satisfaction: before={before_wrong_satisfaction}, after={after_wrong_satisfaction}"
            assert after_wrong.get("state") in ("MovingToStation", "Serving"), \
                f"The allowed mistaken handoff was cancelled: {after_wrong}"
            driver.shot("05-wrong-station-confused")

            driver.until(lambda state: customer(state, 1) is not None and
                         customer(state, 1).get("state") == "Serving" and
                         customer(state, 1).get("arrived") and
                         customer(state, 1).get("station") == wrong_station["id"], timeout=20)
            driver.until(lambda state: state.get("targetCustomer") == 1 and
                         state.get("targetAction") == "Guide" and state.get("available"), timeout=8)
            assert driver.state.get("action") == "转移顾客", \
                f"A wrong-station O002 must expose a correction action: {MobileDriverSnapshot(driver.state)}"
            guide_index = len(driver.actions)
            driver.action()
            guide_touch = driver.actions[guide_index]
            assert guide_touch.get("targetCustomer") == 1 and guide_touch.get("targetAction") == "Guide", \
                f"The real touch did not start O002 correction: {guide_touch}"
            driver.until(lambda state: state.get("guided") == 1 and
                         state.get("targetCustomer") == 1 and
                         state.get("targetAction") == "Assign", timeout=8)

            wash_stations = [station for station in driver.state["stations"]
                             if station.get("type") == "Wash" and
                             not station.get("occupied")]
            assert wash_stations, "No compatible free wash station is available for correction."
            wash_stations.sort(key=lambda station: route_distance(driver, station))
            corrected_station = None
            for station in wash_stations:
                if not driver.goto(station["position"], radius=1.2):
                    continue
                player = driver.state["player"]
                if math.dist((player["x"], player["z"]),
                             (station["position"]["x"], station["position"]["z"])) > 1.5:
                    continue
                if driver.state.get("guided") == 1 and \
                        driver.state.get("targetCustomer") == 1 and \
                        driver.state.get("targetAction") == "Assign" and \
                        driver.state.get("available") and \
                        driver.state.get("action") == "安排洗发":
                    corrected_station = station
                    break
            assert corrected_station is not None, \
                f"Could not reach a compatible wash chair to correct O002: {MobileDriverSnapshot(driver.state)}"
            assert_target(driver, 1, "Assign", "安排洗发")
            driver.shot("06-correct-wash-station-ready")

            correction_index = len(driver.actions)
            driver.action()
            correction_touch = driver.actions[correction_index]
            assert correction_touch.get("targetCustomer") == 1 and \
                correction_touch.get("targetAction") == "Assign", \
                f"The real touch did not execute the wash correction: {correction_touch}"
            driver.until(lambda state: customer(state, 1) is not None and
                         customer(state, 1).get("station") == corrected_station["id"] and
                         customer(state, 1).get("state") in ("MovingToStation", "Serving") and
                         customer(state, 1).get("wrongStationCount") == 1 and
                         customer(state, 1).get("reactionKind") == "None", timeout=8)
            after_correction = customer(driver.state, 1)
            assert after_correction.get("satisfaction") < before_wrong_satisfaction, \
                f"Correction unexpectedly erased the mistake penalty: {after_correction}"
            driver.shot("07-o002-corrected-to-wash")

            assert not errors, f"Chromium reported browser errors: {errors}"
            result.update({
                "passed": True,
                "day": 1,
                "customerId": 1,
                "orderId": "O002",
                "orderIdentity": "Day 1 spawn id 1 is fixed to O002; runtime need verified as Wash",
                "realTouchActions": [greet_touch, wrong_touch, guide_touch, correction_touch],
                "wrongStation": {
                    "stationId": wrong_station["id"],
                    "stationType": wrong_station["type"],
                    "readyTarget": wrong_ready,
                    "wrongStationCount": after_wrong.get("wrongStationCount"),
                    "reactionKind": after_wrong.get("reactionKind"),
                    "customerSatisfactionBefore": before_wrong_satisfaction,
                    "customerSatisfactionAfter": after_wrong_satisfaction,
                    "shopSatisfactionBefore": shop_satisfaction_before_wrong,
                    "shopSatisfactionAfter": shop_satisfaction_after_wrong,
                    "customerState": after_wrong.get("state"),
                },
                "correction": {
                    "stationId": corrected_station["id"],
                    "stationType": corrected_station["type"],
                    "customerState": after_correction.get("state"),
                    "wrongStationCountRetained": after_correction.get("wrongStationCount"),
                    "reactionKindAfterCorrection": after_correction.get("reactionKind"),
                    "customerSatisfactionAfterCorrection": after_correction.get("satisfaction"),
                },
                "screenshots": [
                    "01-opening.png", "02-o002-waiting.png", "03-o002-greet-ready.png",
                    "04-wrong-haircut-station-ready.png", "05-wrong-station-confused.png",
                    "06-correct-wash-station-ready.png", "07-o002-corrected-to-wash.png",
                ],
                "errors": errors,
                "framesObserved": len(driver.history),
            })
        except Exception as error:
            result["error"] = str(error)
            result["errors"] = errors
            if driver is not None:
                try:
                    driver.shot("failure")
                except Exception:
                    pass
            raise
        finally:
            if driver is not None:
                driver.result = result
                driver.save()
            browser.close()


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--url", help="URL of the freshly built WebGL Demo")
    parser.add_argument("--evidence-dir", type=Path, default=EVIDENCE)
    args = parser.parse_args()
    try:
        if args.url:
            run(args.url, args.evidence_dir)
        else:
            with qa.serve(BUILD_ROOT / "WebGLDemo") as base:
                run(base, args.evidence_dir)
    except Exception as error:
        print(f"FAIL: {error}", file=sys.stderr)
        return 1
    print(f"PASS: report saved to {args.evidence_dir.resolve() / 'report.json'}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
