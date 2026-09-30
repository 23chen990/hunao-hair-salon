import importlib.util
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "check_pacing_acceptance", ROOT / "tools" / "check-pacing-rebalance.py")
MODULE = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(MODULE)


def purchase_frame(pad_id, unlocked, paid=10, limit=2, until=0):
    return {
        "day": 3, "state": "Business", "progress": .2,
        "paidCustomers": paid, "softCustomerLimit": limit,
        "capacityPracticeUntilPaid": until,
        "pads": [{"id": pad_id, "unlocked": unlocked}],
    }


class CapacityBufferAcceptanceTests(unittest.TestCase):
    def test_waiting_seats_do_not_require_a_service_capacity_buffer(self):
        frames = [purchase_frame("WaitingSeats", False),
                  purchase_frame("WaitingSeats", True)]
        _, violations = MODULE.capacity_buffer_metrics(frames)
        self.assertEqual(violations, [])

    def test_equipment_without_capacity_growth_preserves_an_existing_buffer(self):
        frames = [purchase_frame("BlowStand", False, until=12),
                  purchase_frame("BlowStand", True, paid=11, until=12)]
        _, violations = MODULE.capacity_buffer_metrics(frames)
        self.assertEqual(violations, [])

    def test_capacity_buffer_releases_after_two_actual_payments(self):
        frames = [purchase_frame("HaircutChair", False),
                  purchase_frame("HaircutChair", True, until=12),
                  purchase_frame("HaircutChair", True, paid=12, limit=3, until=12)]
        summary, violations = MODULE.capacity_buffer_metrics(frames)
        self.assertEqual(violations, [])
        self.assertEqual(summary["observations"][0]["release"]["paidCustomers"], 12)

    def test_immediate_capacity_growth_without_a_buffer_is_still_rejected(self):
        frames = [purchase_frame("HaircutChair", False),
                  purchase_frame("HaircutChair", True, limit=3)]
        _, violations = MODULE.capacity_buffer_metrics(frames)
        self.assertIn("limit-raised-before-buffer", {item["reason"] for item in violations})

    def test_capacity_release_before_the_second_payment_is_rejected(self):
        frames = [purchase_frame("HaircutChair", False),
                  purchase_frame("HaircutChair", True, until=12),
                  purchase_frame("HaircutChair", True, paid=11, limit=3, until=12)]
        _, violations = MODULE.capacity_buffer_metrics(frames)
        self.assertIn("limit-released-before-two-paid", {item["reason"] for item in violations})


class ArrivalAcceptanceTests(unittest.TestCase):
    @staticmethod
    def frame(progress, ids):
        return {
            "day": 3, "state": "Business", "progress": progress,
            "checkoutWaiting": 1, "restRemaining": 6,
            "hasUsefulManagementWork": True,
            "customers": [{"id": customer_id, "state": "Checkout", "need": "Cut"}
                          for customer_id in ids],
        }

    def test_customer_already_in_first_frame_is_not_a_new_arrival(self):
        metrics = MODULE.analyze_day([self.frame(.2, [7]), self.frame(.21, [7])], 3)
        self.assertEqual(metrics["violations"]["newArrivalDuringRest"], [])
        self.assertEqual(metrics["violations"]["newArrivalWhileCheckoutBacklogged"], [])

    def test_a_real_new_customer_is_still_detected_during_backlog_and_rest(self):
        metrics = MODULE.analyze_day([self.frame(.2, [7]), self.frame(.21, [7, 8])], 3)
        self.assertEqual(metrics["violations"]["newArrivalDuringRest"][0]["customerIds"], [8])
        self.assertEqual(metrics["violations"]["newArrivalWhileCheckoutBacklogged"][0]["customerIds"], [8])


if __name__ == "__main__":
    unittest.main()
