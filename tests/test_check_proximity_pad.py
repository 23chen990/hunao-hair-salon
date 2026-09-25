import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
MODULE_PATH = ROOT / "tools" / "check-proximity-pad.py"
SPEC = importlib.util.spec_from_file_location("check_proximity_pad", MODULE_PATH)
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


class FakeDriver:
    def __init__(self, state):
        self.state = state


class CheckProximityPadSnapshotTests(unittest.TestCase):
    def test_failure_snapshot_keeps_pre_retry_values(self):
        driver = FakeDriver(
            {
                "completed": 1,
                "target": 3,
                "balance": 42,
                "padPaid": 75,
                "padUnlocked": False,
                "sourceWashKits": 10,
                "carriedWashKits": 1,
                "washRackWashKits": 1,
            }
        )

        snapshot = MODULE.capture_failure_snapshot(driver)
        driver.state.update(
            {
                "completed": 0,
                "balance": 12,
                "padPaid": 180,
                "padUnlocked": True,
            }
        )

        self.assertEqual(snapshot["failedCompleted"], 1)
        self.assertEqual(snapshot["failedTarget"], 3)
        self.assertEqual(snapshot["failedBalance"], 42)
        self.assertEqual(snapshot["failedPaid"], 75)
        self.assertFalse(snapshot["failedUnlocked"])
        self.assertEqual(snapshot["failedSupply"], {
            "sourceWashKits": 10,
            "carriedWashKits": 1,
            "washRackWashKits": 1,
        })


if __name__ == "__main__":
    unittest.main()
