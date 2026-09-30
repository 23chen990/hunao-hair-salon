#!/usr/bin/env python3
"""真实 Chromium 触控回归：验证洗发台被占用时的阻塞提示。"""
import json
import time
import importlib.util
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "unity-hair-salon" / "Builds" / "WebGLDemo"
EVIDENCE = ROOT / "unity-hair-salon" / "Builds" / "BlockedStationGuidance"
URL = "http://127.0.0.1:8910/WebGLDemo/?mobileEvidence=1"

spec = importlib.util.spec_from_file_location("mobile_salon", ROOT / "tools" / "check-mobile-salon.py")
mobile = importlib.util.module_from_spec(spec)
spec.loader.exec_module(mobile)


def station_usable(station):
    return station.get("usable", station.get("id") in (0, 1))


class GuidanceDriver(mobile.MobileDriver):
    def __init__(self, page):
        super().__init__(page)
        self.observations = []
        self.screenshots = []
        self.started_at = time.monotonic()

    def track_first_leaving(self, state):
        # 仅记录本检查的证据截图，避免复用驱动的整日流程截图回调。
        return

    def snapshot(self, name, expected=None):
        state = self.state
        button = self.visible_interaction_button(state)
        item = {
            "name": name,
            "button": None if button is None else button.get("label"),
            "available": None if button is None else button.get("available"),
            "action": state.get("action"),
            "targetCustomer": state.get("targetCustomer"),
            "targetAction": state.get("targetAction"),
            "guided": state.get("guided"),
            "player": state.get("player"),
            "readyToRinse": state.get("readyToRinse"),
            "customers": [{
                key: customer.get(key) for key in
                ("id", "state", "need", "station", "foamReady", "foamRunning", "needsTransfer")
            } for customer in state.get("customers", [])],
            "stations": [{
                key: station.get(key) for key in ("id", "type", "usable", "occupied")
            } for station in state.get("stations", [])],
        }
        if expected:
            item["expected"] = expected
        self.observations.append(item)
        print("OBS " + json.dumps(item, ensure_ascii=False), flush=True)
        return item

    def evidence_shot(self, name):
        self.release()
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        path = EVIDENCE / (name + ".png")
        self.page.screenshot(path=str(path))
        self.screenshots.append(path.name)
        return path

    def wait_customer(self, customer_id, predicate=None, timeout=50):
        def match(state):
            customer = next((c for c in state.get("customers", [])
                             if c.get("id") == customer_id), None)
            return customer is not None and (predicate is None or predicate(customer))
        return self.until(match, timeout)

    def find_station(self, station_id=None, station_type=None, free=None):
        candidates = [s for s in self.state.get("stations", [])
                      if station_usable(s)
                      and (station_id is None or s.get("id") == station_id)
                      and (station_type is None or s.get("type") == station_type)
                      and (free is None or bool(s.get("occupied")) != free)]
        if not candidates:
            raise AssertionError("找不到符合条件的可用工位: " + json.dumps(
                {"id": station_id, "type": station_type, "free": free,
                 "stations": self.state.get("stations")}, ensure_ascii=False))
        return candidates[0]

    def wait_button(self, label, available=None, timeout=12):
        def match(state):
            button = self.visible_interaction_button(state)
            return (button is not None and button.get("label") == label and
                    (available is None or button.get("available") is available))
        return self.until(match, timeout)

    def greet(self, customer_id):
        customer = next(c for c in self.state["customers"] if c["id"] == customer_id)
        self.goto(customer["position"], radius=1.0)
        self.until(lambda s: s.get("targetCustomer") == customer_id and
                   s.get("targetAction") == "Greet" and s.get("available"), 15)
        self.action()
        self.until(lambda s: s.get("guided") == customer_id and
                   s.get("targetAction") == "Assign", 10)

    def assign_guided(self, station):
        self.goto(station["position"], radius=0.7)
        self.until(lambda s: s.get("guided") >= 0 and
                   s.get("targetAction") == "Assign" and s.get("available"), 12)
        customer_id = self.state["guided"]
        self.action()
        self.until(lambda s: any(c.get("id") == customer_id and
                                 c.get("state") in ("MovingToStation", "Serving")
                                 and c.get("station") == station["id"]
                                 for c in s.get("customers", [])), 12)

    def settle_first_haircut(self):
        self.tap_button("开始营业")
        self.until(lambda s: s.get("state") == "Business", 12)
        self.until(lambda s: any(c.get("id") == 0 and c.get("state") == "Waiting"
                                 and c.get("arrived") for c in s.get("customers", [])), 25)
        self.greet(0)
        chair = self.find_station(station_type="Haircut", free=True)
        self.assign_guided(chair)
        self.until(lambda s: any(c.get("id") == 0 and c.get("state") == "Serving"
                                 and c.get("arrived") for c in s.get("customers", [])), 15)
        self.goto(chair["position"], radius=1.35)
        self.until(lambda s: s.get("targetCustomer") == 0 and
                   s.get("targetAction") == "Cut" and s.get("available"), 12)
        self.action()
        self.until(lambda s: any(c.get("id") == 0 and
                                 c.get("state") in ("Finished", "Leaving", "Exited")
                                 for c in s.get("customers", [])), 15)

    def build_blocked_state(self):
        # O002 is the deterministic first wash order after O001.
        self.until(lambda s: any(c.get("id") == 1 and c.get("need") == "Wash"
                                 and c.get("state") == "Waiting" and c.get("arrived")
                                 for c in s.get("customers", [])), 30)
        self.greet(1)
        wash = self.find_station(station_id=0, station_type="Wash", free=True)
        # 先补充用品，再把顾客安排到洗发台，避免补货路线把玩家带到家具碰撞边缘。
        if self.state.get("washRackWashKits", 0) <= 0:
            self.goto({"x": 3.15, "y": 0.2, "z": 0.7}, radius=0.7)
            self.wait(0.5)
            self.goto({"x": -1.25, "y": 0.2, "z": 4.6}, radius=0.7)
            self.wait(0.5)
        self.assign_guided(wash)
        self.until(lambda s: any(c.get("id") == 1 and c.get("state") == "Serving"
                                 and c.get("station") == 0 and c.get("arrived")
                                 for c in s.get("customers", [])), 15)
        self.goto(wash["position"], radius=1.35)
        self.until(lambda s: s.get("targetCustomer") == 1 and
                   s.get("targetAction") == "Wash" and s.get("available"), 12)
        self.action()  # 真正触控“洗发”，然后离开让泡沫在后台就绪。
        self.until(lambda s: any(c.get("id") == 1 and c.get("foamRunning")
                                 for c in s.get("customers", [])), 8)
        away = {"x": 2.8, "y": 0.2, "z": -0.8}
        self.goto(away, radius=1.0)
        self.until(lambda s: any(c.get("id") == 1 and c.get("foamReady")
                                 for c in s.get("customers", [])), 10)
        self.wait_customer(2, lambda c: c.get("need") == "Wash" and
                           c.get("state") == "Waiting" and c.get("arrived"), 35)
        self.greet(2)
        self.until(lambda s: s.get("guided") == 2 and s.get("targetAction") == "Assign", 8)

    def run_check(self):
        self.load(URL)
        self.settle_first_haircut()
        self.build_blocked_state()

        outside = self.snapshot("blocked-outside", {
            "button": "先为 2 号冲洗", "available": False,
            "hintContains": "洗发工位被 2 号占用",
        })
        self.evidence_shot("01-blocked-guidance")
        assert outside["button"] == "先为 2 号冲洗", outside
        assert outside["available"] is False, outside

        # 与正式整日脚本相同：按工位坐标寻路到 1.0 半径内，再由游戏决定当前可操作目标。
        wash = self.find_station(station_id=0, station_type="Wash")
        self.goto(wash["position"])
        self.wait_button("冲洗", True, 12)
        available = self.snapshot("rinse-available", {
            "button": "冲洗", "available": True, "targetCustomer": 1,
        })
        self.evidence_shot("02-rinse-available")
        assert available["button"] == "冲洗", available
        assert available["available"] is True, available
        self.action()
        self.until(lambda s: any(c.get("id") == 1 and c.get("needsTransfer")
                                 for c in s.get("customers", [])), 15)

        # O002 冲洗后还要吹发，仍占着唯一洗发台；离开后应提示先转移他。
        self.goto({"x": 2.8, "y": 0.2, "z": -0.8}, radius=1.0)
        self.wait_button("先转移 2 号", False, 12)
        transfer_hint = self.snapshot("transfer-guidance", {
            "button": "先转移 2 号", "available": False, "guided": 2,
        })
        self.evidence_shot("03-transfer-guidance")
        assert transfer_hint["guided"] == 2, transfer_hint

        self.goto(wash["position"])
        self.wait_button("转移顾客", True, 12)
        self.action()
        self.until(lambda s: s.get("guided") == 1 and s.get("targetAction") == "Assign", 8)
        chair = self.find_station(station_type="Haircut", free=True)
        self.goto(chair["position"])
        self.until(lambda s: s.get("guided") == 1 and s.get("targetAction") == "Assign"
                   and s.get("available"), 12)
        self.action()
        self.until(lambda s: any(c.get("id") == 1 and c.get("station") == chair["id"]
                                 for c in s.get("customers", [])) and s.get("guided") == 2, 12)
        resumed = self.snapshot("reception-resumed", {"guided": 2})
        assert resumed["guided"] == 2, resumed

        self.until(lambda s: any(st.get("id") == 0 and not st.get("occupied")
                                 for st in s.get("stations", [])), 15)
        self.goto(wash["position"])
        self.wait_button("安排洗发", True, 12)
        final = self.snapshot("guided-assigned", {
            "button": "安排洗发", "available": True, "guided": 2,
        })
        self.evidence_shot("04-guided-assigned")
        assert final["button"] == "安排洗发", final
        assert final["available"] is True, final
        self.action()
        self.until(lambda s: any(c.get("id") == 2 and c.get("station") == 0 and
                                 c.get("state") in ("MovingToStation", "Serving")
                                 for c in s.get("customers", [])), 12)


def main():
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    report = {
        "passed": False,
        "url": URL,
        "viewport": {"width": 844, "height": 390},
        "input": "Chromium CDP Input.dispatchTouchEvent",
        "runs": [],
    }
    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True,
            args=["--enable-webgl", "--disable-web-security"])
        for run_number in (1, 2):
            page = mobile.qa.new_mobile_page(browser)
            driver = GuidanceDriver(page)
            try:
                driver.run_check()
                run = {
                    "run": run_number, "passed": True,
                    "observations": driver.observations,
                    "screenshots": driver.screenshots,
                    "consoleErrors": driver.errors,
                    "elapsedSeconds": round(time.monotonic() - driver.started_at, 2),
                }
            except Exception as error:
                driver.evidence_shot("failure-run-%d" % run_number)
                run = {
                    "run": run_number, "passed": False, "error": str(error),
                    "observations": driver.observations,
                    "screenshots": driver.screenshots,
                    "consoleErrors": driver.errors,
                    "lastState": driver.state,
                }
                report["runs"].append(run)
                page.close()
                browser.close()
                report["consoleErrors"] = driver.errors
                (EVIDENCE / "report.json").write_text(
                    json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
                raise
            report["runs"].append(run)
            page.close()
        browser.close()
    report["passed"] = all(run["passed"] for run in report["runs"])
    report["consoleErrors"] = sorted({
        error for run in report["runs"] for error in run.get("consoleErrors", [])
    })
    (EVIDENCE / "report.json").write_text(
        json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
    print(json.dumps({"passed": report["passed"], "runs": len(report["runs"]),
                      "screenshots": report["runs"][-1]["screenshots"]},
                     ensure_ascii=False), flush=True)
    if not report["passed"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
