import importlib.util
import time
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools" / "check-mobile-salon.py"
SPEC = importlib.util.spec_from_file_location("check_mobile_salon", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class MobileDriverNavigationRegressionTests(unittest.TestCase):
    def test_telemetry_staleness_has_wall_clock_guard(self):
        driver = MODULE.MobileDriver.__new__(MODULE.MobileDriver)
        driver.last_telemetry_wall = time.monotonic() - 0.61
        self.assertTrue(driver.telemetry_is_stale())
        driver.last_telemetry_wall = time.monotonic()
        self.assertFalse(driver.telemetry_is_stale())

    def test_explicit_usable_flag_excludes_locked_station(self):
        station = {"id": 4, "type": "Wash", "occupied": False, "usable": False}
        self.assertFalse(MODULE.MobileDriver.station_is_usable(station))

    def test_legacy_telemetry_excludes_known_locked_opening_stations(self):
        """Old packages omit ``usable``; do not route into their locked anchors."""
        self.assertTrue(MODULE.MobileDriver.station_is_usable(
            {"id": 0, "type": "Wash", "occupied": False}))
        self.assertTrue(MODULE.MobileDriver.station_is_usable(
            {"id": 1, "type": "Haircut", "occupied": False}))
        self.assertFalse(MODULE.MobileDriver.station_is_usable(
            {"id": 2, "type": "Haircut", "occupied": False}))
        self.assertFalse(MODULE.MobileDriver.station_is_usable(
            {"id": 4, "type": "Wash", "occupied": False}))

    def test_ready_service_selection_skips_customer_waiting_for_transfer(self):
        """A blocked Dry transfer must not steal the touch target from a ready Cut."""
        state = {
            "customers": [
                {
                    "id": 1, "need": "Dry", "state": "Serving", "arrived": True,
                    "activeAction": "None", "foamRunning": False, "foamReady": False,
                    "autoRunning": False, "autoStopped": False, "autoElapsed": 0,
                    "autoReady": 0, "needsTransfer": True, "patience": 0.1,
                },
                {
                    "id": 4, "need": "Cut", "state": "Serving", "arrived": True,
                    "activeAction": "None", "foamRunning": False, "foamReady": False,
                    "autoRunning": False, "autoStopped": False, "autoElapsed": 0,
                    "autoReady": 0, "needsTransfer": False, "patience": 0.2,
                },
            ],
        }

        selected = MODULE.MobileDriver.choose_ready_service(state)

        self.assertIsNotNone(selected)
        self.assertEqual(selected["id"], 4)

    def test_transfer_station_selection_requires_free_usable_compatible_chair(self):
        state = {
            "stations": [
                {"id": 0, "type": "Wash", "occupied": True, "usable": True},
                {"id": 1, "type": "Haircut", "occupied": False, "usable": True},
                {"id": 2, "type": "Haircut", "occupied": False, "usable": False},
            ],
        }

        stations = MODULE.MobileDriver.transfer_stations(
            state, {"id": 1, "need": "Dry", "needsTransfer": True})

        self.assertEqual([station["id"] for station in stations], [1])

    def test_transfer_anchor_starts_at_customer_current_station(self):
        """Guide is touched at the occupied old chair before moving to its next chair."""
        state = {
            "stations": [
                {"id": 0, "type": "Wash", "occupied": True, "usable": True},
                {"id": 1, "type": "Haircut", "occupied": False, "usable": True},
            ],
        }

        anchor = MODULE.MobileDriver.transfer_anchor(
            state, {"id": 1, "station": 0, "need": "Dry", "needsTransfer": True})

        self.assertIsNotNone(anchor)
        self.assertEqual(anchor["id"], 0)


class FakeConsoleMessage:
    def __init__(self, text, message_type="log"):
        self.text = text
        self.type = message_type


class MobileDriverTelemetryTests(unittest.TestCase):
    @staticmethod
    def listening_driver():
        driver = MODULE.MobileDriver.__new__(MODULE.MobileDriver)
        driver.history, driver.errors, driver.warnings = [], [], []
        driver.state, driver.layout = None, None
        driver.last_progress_log, driver.last_telemetry_wall = None, None
        driver.first_leaving = None
        driver.pending_customers = None
        return driver

    def test_state_receives_the_separately_logged_navigation_layout(self):
        driver = self.listening_driver()
        driver.console(FakeConsoleMessage(
            '[MOBILE_LAYOUT] {"floor":{"x":-10.2,"y":-5.0,"width":25.75,"height":12.15},'
            '"obstacles":[{"x":1.0,"y":2.0,"width":3.0,"height":4.0}]}'))
        driver.console(FakeConsoleMessage(
            '[MOBILE_STATE] {"day":2,"state":"Business","progress":0.3,"customers":[]}'))

        self.assertEqual(driver.state["floor"]["width"], 25.75)
        self.assertEqual(len(driver.state["obstacles"]), 1)
        self.assertEqual(driver.errors, [])

    def test_truncated_state_is_an_error_instead_of_silently_stale(self):
        driver = self.listening_driver()
        driver.console(FakeConsoleMessage('[MOBILE_STATE] {"day":2,"state":"Business"}'))
        driver.console(FakeConsoleMessage('[MOBILE_STATE] {"day":2,"state":"Busi'))

        self.assertEqual(len(driver.history), 1)
        self.assertEqual(len(driver.errors), 1)
        self.assertIn("unparseable", driver.errors[0])

    def test_customers_line_is_merged_into_next_state(self):
        driver = self.listening_driver()
        driver.console(FakeConsoleMessage(
            '[MOBILE_CUSTOMERS] {"customers":[{"id":7,"state":"Waiting"}]}'))
        driver.console(FakeConsoleMessage(
            '[MOBILE_STATE] {"day":3,"state":"Business","balance":10}'))

        self.assertEqual(driver.state["customers"], [{"id": 7, "state": "Waiting"}])
        self.assertEqual(driver.errors, [])

    def test_state_embedded_customers_win_over_split_line(self):
        driver = self.listening_driver()
        driver.console(FakeConsoleMessage(
            '[MOBILE_CUSTOMERS] {"customers":[{"id":7,"state":"Waiting"}]}'))
        driver.console(FakeConsoleMessage(
            '[MOBILE_STATE] {"day":3,"state":"Business","customers":[{"id":2,"state":"Serving"}]}'))

        self.assertEqual(driver.state["customers"], [{"id": 2, "state": "Serving"}])
        self.assertEqual(driver.errors, [])

    def test_truncated_customers_line_is_an_error(self):
        driver = self.listening_driver()
        driver.console(FakeConsoleMessage('[MOBILE_CUSTOMERS] {"customers":[{"id":7}'))

        self.assertEqual(len(driver.errors), 1)
        self.assertIn("[MOBILE_CUSTOMERS] unparseable", driver.errors[0])


class MobileDriverRunOptionTests(unittest.TestCase):
    def test_two_day_cli_option_is_explicit(self):
        args = MODULE.parse_driver_args([
            "--url", "http://127.0.0.1:8910/WebGLDemo/",
            "--evidence-dir", "/tmp/first-session",
            "--days", "2",
            "--record-video-dir", "/tmp/first-session/video",
        ])

        self.assertEqual(args.days, 2)
        self.assertEqual(args.record_video_dir, Path("/tmp/first-session/video"))

    def test_recording_context_uses_mobile_viewport_and_video_directory(self):
        options = MODULE.mobile_context_options(Path("/tmp/first-session/video"))

        self.assertEqual(options["viewport"], {"width": 844, "height": 390})
        self.assertTrue(options["is_mobile"])
        self.assertTrue(options["has_touch"])
        self.assertEqual(options["record_video_dir"], "/tmp/first-session/video")


if __name__ == "__main__":
    unittest.main()
