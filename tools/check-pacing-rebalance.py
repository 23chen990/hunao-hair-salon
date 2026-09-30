#!/usr/bin/env python3
"""Real-touch acceptance for the first-session pacing rebalance.

This runner deliberately reuses the existing core-rework play loop and the
MobileDriver.  It never injects gameplay state; all interaction is joystick,
touch, and visible buttons in a fresh Chromium context.
"""
import argparse
import importlib.util
import json
import math
import time
from collections import Counter, defaultdict
from pathlib import Path

from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
BUILD = ROOT / "unity-hair-salon" / "Builds" / "WebGLDemo"
EVIDENCE = ROOT / "artifacts" / "pacing-rebalance" / "browser"
ACTIVE_STATES = {"Entering", "Waiting", "MovingToStation", "Serving", "Finished", "Checkout"}
BUSINESS_STATES = {"Business", "ClosingGrace"}
BUSINESS_DURATION_SECONDS = 180.0
REST_ENFORCEMENT_EPSILON_SECONDS = 0.5


def load_module(name, relative_path):
    spec = importlib.util.spec_from_file_location(name, ROOT / relative_path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


# Importing core-rework gives us its tested real-touch play loop and the exact
# MobileDriver implementation used by the existing acceptance suite.
core = load_module("core_rework_for_pacing", "tools/check-core-rework.py")
mobile = core.mobile
mobile.EVIDENCE = EVIDENCE
qa = mobile.qa


def progress_seconds(state):
    progress = mobile.MobileDriver.progress(state)
    if progress is None:
        return None
    return float(progress) * BUSINESS_DURATION_SECONDS


def customer_state_count(state, allowed=ACTIVE_STATES):
    return sum(1 for customer in state.get("customers", [])
               if customer.get("state") in allowed)


def day_frames(history, day):
    return [state for state in history
            if state.get("day") == day and state.get("state") in BUSINESS_STATES]


def first_customers(frames):
    first = {}
    needs = defaultdict(set)
    steps = defaultdict(list)
    state_history = defaultdict(list)
    for state in frames:
        now = progress_seconds(state)
        for customer in state.get("customers", []):
            customer_id = customer.get("id")
            if customer_id is None:
                continue
            need = customer.get("need") or "Unknown"
            needs[customer_id].add(need)
            steps[customer_id].append(customer.get("step", 0))
            state_history[customer_id].append({
                "timeSeconds": None if now is None else round(now, 2),
                "state": customer.get("state"),
                "need": need,
                "step": customer.get("step", 0),
                "serviceCount": customer.get("serviceCount"),
                "complete": bool(customer.get("complete")),
                "serviceResult": customer.get("serviceResult"),
                "needsTransfer": bool(customer.get("needsTransfer")),
                "paidDryOrders": state.get("paidDryOrders", 0),
                "paidWashOrders": state.get("paidWashOrders", 0),
                "effectiveActiveCustomers": customer_state_count(state),
            })
            if customer_id not in first:
                first[customer_id] = {
                    "id": customer_id,
                    "timeSeconds": None if now is None else round(now, 2),
                    "state": customer.get("state"),
                    "need": need,
                    "step": customer.get("step", 0),
                    "serviceCount": customer.get("serviceCount"),
                    "complete": bool(customer.get("complete")),
                    "paidCustomers": state.get("paidCustomers", 0),
                    "paidDryOrders": state.get("paidDryOrders", 0),
                    "paidWashOrders": state.get("paidWashOrders", 0),
                }
    return first, needs, steps, state_history


def new_ids(previous_ids, state):
    current = {customer.get("id") for customer in state.get("customers", [])
               if customer.get("id") is not None}
    return current - previous_ids, current


def pad_unlock_times(frames):
    result = {}
    previous = {}
    for state in frames:
        now = progress_seconds(state)
        for pad in state.get("pads", []) or []:
            pad_id = pad.get("id") or pad.get("padId")
            if not pad_id:
                continue
            before = previous.get(pad_id, {})
            unlocked = bool(pad.get("unlocked"))
            # A day can open with equipment purchased on a prior day. Only
            # record a purchase after observing the false -> true transition
            # inside this day; never treat opening state as a new purchase.
            if (pad_id in previous and unlocked and not before.get("unlocked")
                    and pad_id not in result):
                result[pad_id] = {
                    "timeSeconds": None if now is None else round(now, 2),
                    "cost": pad.get("cost"),
                    "paid": pad.get("paid"),
                    "paidCustomers": state.get("paidCustomers"),
                    "softCustomerLimit": state.get("softCustomerLimit"),
                    "capacityPracticeUntilPaid": state.get("capacityPracticeUntilPaid"),
                }
            previous[pad_id] = dict(pad)
    return result


def service_learning_metrics(frames, needs, state_history, purchases):
    """Find the first Dry/Wash lesson and enforce one active customer."""
    result = {}
    violations = []
    for pad_id, service, mastery_key in (
        ("BlowDryer", "Dry", "paidDryOrders"),
        ("WashStation", "Wash", "paidWashOrders"),
    ):
        purchase = purchases.get(pad_id)
        if not purchase:
            continue
        candidates = []
        for customer_id, history_items in state_history.items():
            for item in history_items:
                if item.get("need") != service:
                    continue
                if item.get("timeSeconds") is None or item["timeSeconds"] < purchase.get("timeSeconds", 0):
                    continue
                candidates.append((item["timeSeconds"], customer_id, item))
                break
        if not candidates:
            result[pad_id] = {"purchase": purchase, "firstLearningOrder": None}
            continue
        _, customer_id, item = min(candidates, key=lambda value: value[0])
        service_count = item.get("serviceCount")
        entry = {
            "purchase": purchase,
            "firstLearningOrder": {
                "customerId": customer_id,
                "timeSeconds": item.get("timeSeconds"),
                "serviceCount": service_count,
                "need": service,
                "masteryAtObservation": item.get(mastery_key, 0),
            },
        }
        result[pad_id] = entry
        learning_window = []
        for state in frames:
            state_time = progress_seconds(state)
            if state_time is None or state_time < item.get("timeSeconds", 0):
                continue
            learning_window.append(state)
            if state.get(mastery_key, 0) >= 1:
                break
        max_active = max((customer_state_count(state) for state in learning_window), default=0)
        entry["firstLearningOrder"]["maxEffectiveActiveDuringLesson"] = max_active
        if max_active > 1 and item.get(mastery_key, 0) < 1:
            violations.append({
                "padId": pad_id,
                "customerId": customer_id,
                "serviceCount": service_count,
                "masteryAtObservation": item.get(mastery_key, 0),
                "maxEffectiveActiveDuringLesson": max_active,
            })
    return result, violations


def capacity_buffer_metrics(frames):
    """Check real purchases that start a buffer or immediately raise capacity."""
    capacity_pad_ids = {"HaircutChair", "WaitingSeats", "BlowStand", "WashAnnex", "ExtraSeats"}
    observations = []
    violations = []
    previous_pads = {}
    previous_state = None
    for index, state in enumerate(frames):
        for pad in state.get("pads", []) or []:
            pad_id = pad.get("id") or pad.get("padId")
            if not pad_id:
                continue
            before = previous_pads.get(pad_id)
            unlocked = bool(pad.get("unlocked"))
            purchased_now = before is not None and not before.get("unlocked") and unlocked
            if purchased_now and (pad_id in capacity_pad_ids or
                                  (previous_state is not None and
                                   state.get("softCustomerLimit", 0) > previous_state.get("softCustomerLimit", 0))):
                old_limit = None if previous_state is None else previous_state.get("softCustomerLimit")
                new_limit = state.get("softCustomerLimit")
                paid_at = state.get("paidCustomers")
                observed_until = state.get("capacityPracticeUntilPaid")
                previous_until = (previous_state or {}).get("capacityPracticeUntilPaid", 0)
                buffer_started = (observed_until is not None and previous_until is not None
                                  and observed_until > previous_until)
                requires_buffer = buffer_started or (
                    old_limit is not None and new_limit is not None and new_limit > old_limit)
                expected_until = paid_at + 2 if requires_buffer and paid_at is not None else None
                observation = {
                    "padId": pad_id,
                    "timeSeconds": progress_seconds(state),
                    "paidCustomersAtPurchase": paid_at,
                    "previousSoftCustomerLimit": old_limit,
                    "softCustomerLimitAtPurchase": new_limit,
                    "expectedCapacityPracticeUntilPaid": expected_until,
                    "capacityPracticeUntilPaidAtPurchase": observed_until,
                    "bufferStarted": buffer_started,
                    "requiresBuffer": requires_buffer,
                }
                release = None
                for later in frames[index + 1:] if requires_buffer else []:
                    later_limit = later.get("softCustomerLimit")
                    if old_limit is not None and later_limit is not None and later_limit > old_limit:
                        release = {
                            "timeSeconds": progress_seconds(later),
                            "paidCustomers": later.get("paidCustomers"),
                            "softCustomerLimit": later_limit,
                            "capacityPracticeUntilPaid": later.get("capacityPracticeUntilPaid"),
                        }
                        break
                observation["release"] = release
                observations.append(observation)
                if requires_buffer and old_limit is not None and new_limit is not None and new_limit != old_limit:
                    violations.append({**observation, "reason": "limit-raised-before-buffer"})
                if expected_until is not None and observed_until != expected_until:
                    violations.append({**observation, "reason": "wrong-buffer-target"})
                if release is not None and expected_until is not None and release.get("paidCustomers", 0) < expected_until:
                    violations.append({**observation, "reason": "limit-released-before-two-paid", "release": release})
        previous_pads = {
            (item.get("id") or item.get("padId")): dict(item)
            for item in state.get("pads", []) or []
            if item.get("id") or item.get("padId")
        }
        previous_state = state
    return {"observations": observations, "violations": violations}, violations


def duration_for_predicate(frames, predicate):
    total = 0.0
    for previous, current in zip(frames, frames[1:]):
        start = progress_seconds(previous)
        end = progress_seconds(current)
        if start is None or end is None:
            continue
        delta = max(0.0, min(5.0, end - start))
        if predicate(previous) and predicate(current):
            total += delta
    return round(total, 2)


def contiguous_windows(frames, predicate):
    windows = []
    active = None
    for state in frames:
        now = progress_seconds(state)
        if now is None:
            continue
        matches = bool(predicate(state))
        if matches and active is None:
            active = [now, now]
        elif matches and active is not None:
            active[1] = now
        elif not matches and active is not None:
            windows.append(tuple(active))
            active = None
    if active is not None:
        windows.append(tuple(active))
    return [{"startSeconds": round(start, 2),
             "endSeconds": round(end, 2),
             "durationSeconds": round(max(0.0, end - start), 2)}
            for start, end in windows]


def is_ineffective_idle_state(state):
    now = progress_seconds(state)
    work_count = state.get("activeWorkCustomers", state.get("working", 0))
    return (now is not None and 2.0 < now < BUSINESS_DURATION_SECONDS * .82 and
            customer_state_count(state) == 0 and work_count == 0 and
            not bool(state.get("hasUsefulManagementWork", False)))


def wash_wait_metrics(frames, first, state_history):
    waits = []
    for customer_id, history in state_history.items():
        customer = first.get(customer_id, {})
        if customer.get("need") != "Wash":
            continue
        waiting_at = next((item["timeSeconds"] for item in history
                           if item["state"] in ("Entering", "Waiting")
                           and item["timeSeconds"] is not None), None)
        service_at = next((item["timeSeconds"] for item in history
                           if item["state"] in ("MovingToStation", "Serving")
                           and item["timeSeconds"] is not None), None)
        if waiting_at is None:
            continue
        waits.append({
            "customerId": customer_id,
            "waitSeconds": None if service_at is None else round(max(0.0, service_at - waiting_at), 2),
            "served": service_at is not None,
        })
    numeric = [item["waitSeconds"] for item in waits if item["waitSeconds"] is not None]
    return {
        "orders": waits,
        "servedCount": len(numeric),
        "maxSeconds": max(numeric, default=0.0),
        "averageSeconds": round(sum(numeric) / len(numeric), 2) if numeric else 0.0,
    }


def analyze_day(history, day):
    frames = day_frames(history, day)
    assert frames, f"No business telemetry for day {day}"
    first, needs, steps, state_history = first_customers(frames)

    effective_counts = [customer_state_count(state) for state in frames]
    work_counts = [state.get("activeWorkCustomers", state.get("working", -1))
                   for state in frames]
    work_counts = [count for count in work_counts if isinstance(count, (int, float)) and count >= 0]
    max_effective = max(effective_counts, default=0)
    max_working = max(work_counts, default=0)

    rest_window_predicate = lambda state: state.get("restRemaining", 0.0) > 0.0
    rest_windows = contiguous_windows(frames, rest_window_predicate)
    empty_rest_windows = contiguous_windows(
        frames, lambda state: customer_state_count(state) == 0 and rest_window_predicate(state))
    rest_seconds = sum(item["durationSeconds"] for item in rest_windows)
    max_rest = max((item["durationSeconds"] for item in rest_windows), default=0.0)
    rest_checkout_violations = [{
        "timeSeconds": progress_seconds(state),
        "restRemaining": state.get("restRemaining"),
        "checkoutWaiting": state.get("checkoutWaiting"),
    } for state in frames
        if state.get("restRemaining", 0.0) > 0.0 and state.get("checkoutWaiting", 0) > 0]

    def has_useful_management_work(state):
        return bool(state.get("hasUsefulManagementWork", False))

    invalid_idle_predicate = is_ineffective_idle_state

    invalid_idle_windows = contiguous_windows(frames, invalid_idle_predicate)
    ineffective_idle_seconds = duration_for_predicate(frames, invalid_idle_predicate)
    max_ineffective_idle = max(
        (item["durationSeconds"] for item in invalid_idle_windows), default=0.0)
    rest_zero_idle_seconds = duration_for_predicate(
        frames, lambda state: is_ineffective_idle_state(state) and
        state.get("restRemaining", 0.0) <= 0.0)
    productive_buffer_seconds = duration_for_predicate(
        frames, lambda state: customer_state_count(state) == 0 and has_useful_management_work(state))

    idle_seconds = duration_for_predicate(
        frames, lambda state: state.get("activeWorkCustomers",
                                         state.get("working", -1)) == 0)
    breather_seconds = duration_for_predicate(
        frames, lambda state: state.get("restRemaining", 0.0) > 0.0)
    tracked_seconds = max(0.0, (progress_seconds(frames[-1]) or 0.0) -
                          (progress_seconds(frames[0]) or 0.0))

    order_mix = Counter(item.get("need", "Unknown") for item in first.values())
    multi_step = []
    for customer_id, customer_steps in steps.items():
        first_customer = first[customer_id]
        first_need = first_customer.get("need")
        service_set = needs.get(customer_id, set())
        history_items = state_history.get(customer_id, [])
        three_step_seen = (
            any((item.get("serviceCount") or 0) >= 3 for item in history_items) or
            any(not item.get("complete") and item.get("step", 0) >= 2
                for item in history_items) or
            len(service_set) >= 3)
        relevant_unlearned = any(
            ((item.get("serviceCount") or 0) >= 3 or
             (not item.get("complete") and item.get("step", 0) >= 2) or
             len(service_set) >= 3) and
            (("Dry" in service_set and item.get("paidDryOrders", 0) < 3) or
             ("Wash" in service_set and item.get("paidWashOrders", 0) < 3))
            for item in history_items)
        # Customer.Step is a zero-based completed-step index. A two-step
        # Cut+Dry or Cut+Wash order therefore reaches maxStep=2; a forbidden
        # three-step order reaches maxStep=3 (or exposes three distinct needs).
        if three_step_seen:
            multi_step.append({
                "customerId": customer_id,
                "firstNeed": first_need,
                "firstStep": first_customer.get("step", 0),
                "maxStep": max(customer_steps or [0]),
                "firstPaidDryOrders": first_customer.get("paidDryOrders", 0),
                "firstPaidWashOrders": first_customer.get("paidWashOrders", 0),
                "beforeRelevantServiceMastery": relevant_unlearned,
            })

    checkout_arrival_violations = []
    rest_arrival_violations = []
    wash_stock_violations = []
    previous_ids = {customer.get("id") for customer in frames[0].get("customers", [])
                    if customer.get("id") is not None}
    for previous, current in zip(frames, frames[1:]):
        arrived, current_ids = new_ids(previous_ids, current)
        if previous.get("checkoutWaiting", 0) > 0 and arrived:
            checkout_arrival_violations.append({
                "timeSeconds": progress_seconds(current),
                "checkoutWaitingBefore": previous.get("checkoutWaiting", 0),
                "customerIds": sorted(arrived),
            })
        if previous.get("restRemaining", 0.0) > REST_ENFORCEMENT_EPSILON_SECONDS and arrived:
            rest_arrival_violations.append({
                "timeSeconds": progress_seconds(current),
                "restRemainingBefore": previous.get("restRemaining", 0.0),
                "customerIds": sorted(arrived),
            })
        for customer in current.get("customers", []):
            if customer.get("id") not in arrived:
                continue
            if customer.get("state") not in ("Entering", "Waiting"):
                continue
            if customer.get("need") == "Wash" and current.get("washRackWashKits", -1) == 0 and current.get("carriedWashKits", -1) == 0:
                wash_stock_violations.append({
                    "timeSeconds": progress_seconds(current),
                    "customerId": customer.get("id"),
                    "washRackWashKits": current.get("washRackWashKits"),
                    "carriedWashKits": current.get("carriedWashKits"),
                })
        previous_ids = current_ids

    first_two_limit_violations = []
    for state in frames:
        if state.get("paidCustomers", 0) < 2 and customer_state_count(state) > 1:
            first_two_limit_violations.append({
                "timeSeconds": progress_seconds(state),
                "paidCustomers": state.get("paidCustomers", 0),
                "effectiveActiveCustomers": customer_state_count(state),
            })

    purchase_times = pad_unlock_times(frames)
    service_learning, service_learning_violations = service_learning_metrics(
        frames, needs, state_history, purchase_times)
    capacity_buffer, capacity_buffer_violations = capacity_buffer_metrics(frames)

    haircut_demand_predicate = lambda state: (
        any(customer.get("need") in ("Cut", "Haircut") and
            customer.get("state") in ("Entering", "Waiting")
            for customer in state.get("customers", [])) and
        any(station.get("type") == "Haircut" and
            station.get("usable", True) and not station.get("occupied")
            for station in state.get("stations", [])))
    haircut_idle_demand_seconds = duration_for_predicate(frames, haircut_demand_predicate)

    terminal = {}
    for customer_id, history_items in state_history.items():
        for item in reversed(history_items):
            if item.get("state") in ("Leaving", "Exited", "Finished", "Checkout"):
                terminal[customer_id] = item
                break
    failed_ids = sorted(customer_id for customer_id, item in terminal.items()
                        if item.get("serviceResult") == "Failed")

    result = {
        "day": day,
        "frameCount": len(frames),
        "completed": frames[-1].get("completed", 0),
        "target": frames[-1].get("target", 0),
        "balance": frames[-1].get("balance"),
        "lost": {
            "failedCustomerIds": failed_ids,
            "failedCount": len(failed_ids),
            "checkoutAbandoned": frames[-1].get("checkoutAbandoned", 0),
        },
        "mastery": {
            "paidCustomers": frames[-1].get("paidCustomers"),
            "paidDryOrders": frames[-1].get("paidDryOrders"),
            "paidWashOrders": frames[-1].get("paidWashOrders"),
        },
        "pacingTelemetry": {
            "activeWorkCustomers": frames[-1].get("activeWorkCustomers"),
            "softCustomerLimit": frames[-1].get("softCustomerLimit"),
            "capacityPracticeUntilPaid": frames[-1].get("capacityPracticeUntilPaid"),
            "hasUsefulManagementWork": frames[-1].get("hasUsefulManagementWork"),
            "restRemaining": frames[-1].get("restRemaining"),
            "batchArrivals": frames[-1].get("batchArrivals"),
            "pacingReason": frames[-1].get("pacingReason"),
        },
        "purchaseTimes": purchase_times,
        "serviceLearning": service_learning,
        "capacityBuffer": capacity_buffer,
        "orderMix": dict(order_mix),
        "customerFirstSeen": sorted(first.values(), key=lambda item: (item.get("timeSeconds") is None, item.get("timeSeconds") or 0)),
        "multiStepOrders": multi_step,
        "effectiveActiveCustomers": {
            "max": max_effective,
            "average": round(sum(effective_counts) / len(effective_counts), 2),
        },
        "pressureMetrics": {
            "effectivePendingPeak": max_effective,
            "ineffectiveIdleWaitSeconds": ineffective_idle_seconds,
            "maxIneffectiveIdleWindowSeconds": round(max_ineffective_idle, 2),
            "productiveManagementBufferSeconds": productive_buffer_seconds,
            "restZeroIneffectiveIdleWaitSeconds": rest_zero_idle_seconds,
        },
        "activeWorkCustomers": {
            "max": max_working,
            "average": round(sum(work_counts) / len(work_counts), 2) if work_counts else None,
        },
        "idleAndBreather": {
            "trackedSeconds": round(tracked_seconds, 2),
            "idleSeconds": idle_seconds,
            "idleRatio": round(idle_seconds / tracked_seconds, 4) if tracked_seconds else None,
            "breatherSeconds": breather_seconds,
            "breatherRatio": round(breather_seconds / tracked_seconds, 4) if tracked_seconds else None,
            "restWindows": rest_windows,
            "totalRestWindowSeconds": round(rest_seconds, 2),
            "maxRestWindowSeconds": round(max_rest, 2),
            "emptyRestWindows": empty_rest_windows,
            "ineffectiveIdleWindows": invalid_idle_windows,
            "ineffectiveIdleWaitSeconds": ineffective_idle_seconds,
            "productiveManagementBufferSeconds": productive_buffer_seconds,
            "restZeroIneffectiveIdleWaitSeconds": rest_zero_idle_seconds,
        },
        "washWaiting": wash_wait_metrics(frames, first, state_history),
        "haircutIdleWithDemandSeconds": haircut_idle_demand_seconds,
        "violations": {
            "firstTwoLimit": first_two_limit_violations,
            "newArrivalWhileCheckoutBacklogged": checkout_arrival_violations,
            "newArrivalDuringRest": rest_arrival_violations,
            "newWashArrivalWithoutStock": wash_stock_violations,
            "restWhileCheckoutBacklogged": rest_checkout_violations,
            "newServiceLearning": service_learning_violations,
            "capacityBuffer": capacity_buffer_violations,
            "ineffectiveIdle": invalid_idle_windows,
        },
    }
    return result


def assert_day_metrics(metrics):
    day = metrics["day"]
    violations = metrics["violations"]
    if day == 1:
        assert metrics["effectiveActiveCustomers"]["max"] <= 2, metrics
        assert not violations["firstTwoLimit"], violations["firstTwoLimit"]
    assert not violations["newArrivalWhileCheckoutBacklogged"], violations["newArrivalWhileCheckoutBacklogged"]
    assert not violations["newArrivalDuringRest"], violations["newArrivalDuringRest"]
    assert not violations["newWashArrivalWithoutStock"], violations["newWashArrivalWithoutStock"]
    assert not violations["restWhileCheckoutBacklogged"], violations["restWhileCheckoutBacklogged"]
    assert not violations["newServiceLearning"], violations["newServiceLearning"]
    assert not violations["capacityBuffer"], violations["capacityBuffer"]
    assert metrics["pressureMetrics"]["maxIneffectiveIdleWindowSeconds"] <= 4.0, metrics["pressureMetrics"]
    for order in metrics["multiStepOrders"]:
        if order["beforeRelevantServiceMastery"]:
            raise AssertionError("Three-step order appeared before service mastery: " + json.dumps(order, ensure_ascii=False))
    # Rest/recovery may be shorter when there is no useful management work.
    # Do not force a wash purchase, third service purchase, or fixed rest window.


def context_metadata(page, console_events, not_found, failed_requests):
    viewport = dict(qa.VIEWPORT)
    canvas = page.evaluate("""() => {
        const canvas = document.querySelector('canvas');
        if (!canvas) return null;
        const rect = canvas.getBoundingClientRect();
        return {width: canvas.width, height: canvas.height,
                cssWidth: rect.width, cssHeight: rect.height,
                left: rect.left, top: rect.top};
    }""")
    dpr = page.evaluate("() => window.devicePixelRatio")
    response_counts = Counter(item["status"] for item in console_events if item.get("kind") == "response")
    return {
        "targetViewport": viewport,
        "canvas": canvas,
        "devicePixelRatio": dpr,
        "console": {
            "count": sum(1 for item in console_events if item.get("kind") == "console"),
            "errors": [item for item in console_events if item.get("kind") == "console" and item.get("type") in ("error", "warning")],
            "samples": [item for item in console_events if item.get("kind") == "console"][-60:],
        },
        "http404": list(not_found),
        "failedRequests": list(failed_requests),
        "responseStatusCounts": dict(response_counts),
    }


def build_hash_record(build_dir=BUILD):
    files = []
    for path in sorted((build_dir / "Build").iterdir()):
        if not path.is_file():
            continue
        digest = __import__("hashlib").sha256(path.read_bytes()).hexdigest()
        files.append({"file": str(path), "sha256": digest, "bytes": path.stat().st_size})
    aggregate = __import__("hashlib").sha256()
    for item in files:
        aggregate.update((item["file"] + " " + item["sha256"] + "\n").encode())
    return {
        "buildDirectory": str(build_dir),
        "aggregateSha256": aggregate.hexdigest(),
        "files": files,
    }


def run(base, days=2, evidence_dir=EVIDENCE):
    evidence_dir.mkdir(parents=True, exist_ok=True)
    mobile.EVIDENCE = evidence_dir
    console_events = []
    not_found = []
    failed_requests = []
    report = {
        "passed": False,
        "input": "Chromium real touch events; fresh browser context",
        "build": str(BUILD),
        "buildHash": build_hash_record(),
        "daysRequested": days,
        "days": [],
        "screenshotsDirectory": str(evidence_dir),
    }

    with sync_playwright() as playwright:
        browser = playwright.chromium.launch(
            channel="chromium", headless=True,
            args=["--enable-webgl", "--disable-web-security"])
        page = qa.new_mobile_page(browser)
        page.on("console", lambda message: console_events.append({
            "kind": "console", "type": message.type, "text": message.text}))
        page.on("response", lambda response: (
            not_found.append({"url": response.url, "status": response.status})
            if response.status == 404 else None,
            console_events.append({"kind": "response", "status": response.status, "url": response.url})
        ))
        page.on("requestfailed", lambda request: failed_requests.append({
            "url": request.url, "failure": request.failure}))
        driver = mobile.MobileDriver(page)
        try:
            driver.load(base.rstrip("/") + "/?mobileEvidence=1")
            driver.until(lambda state: state.get("state") == "PreOpen", timeout=45)
            assert driver.state.get("balance") == 100, driver.state
            driver.shot("00-fresh-opening")
            report["opening"] = {
                "balance": driver.state.get("balance"),
                "day": driver.state.get("day"),
                "usableStations": [station.get("id") for station in driver.state.get("stations", []) if station.get("usable")],
                "visiblePads": [pad.get("id") for pad in driver.state.get("pads", []) if pad.get("visible")],
            }

            for day in range(1, days + 1):
                action_start = len(driver.actions)
                driver.tap_button("开始营业")
                driver.until(lambda state: state.get("state") == "Business", timeout=45)
                driver.shot(f"day{day}-business-start")
                try:
                    core.play(driver)
                except core.DayEnded:
                    driver.until(lambda state: state.get("state") == "Result", timeout=30)
                else:
                    driver.until(lambda state: state.get("state") == "Result", timeout=30)
                metrics = analyze_day(driver.history, day)
                assert metrics["completed"] >= metrics["target"], metrics
                report["days"].append(metrics)
                assert_day_metrics(metrics)
                (evidence_dir / f"day{day}-states.json").write_text(
                    json.dumps([state for state in driver.history if state.get("day") == day], ensure_ascii=False),
                    encoding="utf-8")
                (evidence_dir / f"day{day}-actions.json").write_text(
                    json.dumps(driver.actions[action_start:], ensure_ascii=False, indent=2),
                    encoding="utf-8")

                driver.tap_button("进入闭店经营")
                driver.until(lambda state: state.get("state") == "ClosedManagement", timeout=20)
                driver.shot(f"day{day}-closed-management")
                checkpoint = {
                    "balance": driver.state.get("balance"),
                    "washRackWashKits": driver.state.get("washRackWashKits"),
                    "paidCustomers": driver.state.get("paidCustomers"),
                    "paidDryOrders": driver.state.get("paidDryOrders"),
                    "paidWashOrders": driver.state.get("paidWashOrders"),
                }
                driver.reload()
                driver.until(lambda state: state.get("state") == "ClosedManagement", timeout=45)
                refresh = {
                    "balance": driver.state.get("balance"),
                    "washRackWashKits": driver.state.get("washRackWashKits"),
                    "paidCustomers": driver.state.get("paidCustomers"),
                    "paidDryOrders": driver.state.get("paidDryOrders"),
                    "paidWashOrders": driver.state.get("paidWashOrders"),
                }
                assert refresh == checkpoint, {"checkpoint": checkpoint, "refresh": refresh}
                report["days"][-1]["saveRefresh"] = {
                    "checkpoint": checkpoint, "refresh": refresh, "passed": True
                }
                if day < days:
                    driver.tap_button("准备下一天")
                    driver.until(lambda state: state.get("state") == "PreOpen", timeout=30)
                    driver.shot(f"day{day}-next-day-preopen")

            assert not driver.errors, driver.errors
            assert not not_found, not_found
            report["passed"] = True
        except Exception as error:
            report["error"] = str(error)
            try:
                driver.shot("failure")
            except Exception:
                pass
            raise
        finally:
            report["driverErrors"] = list(driver.errors)
            report["warnings"] = list(driver.warnings)
            report["context"] = context_metadata(page, console_events, not_found, failed_requests)
            report["historyCount"] = len(driver.history)
            report["actionCount"] = len(driver.actions)
            driver.save()
            (evidence_dir / "console.json").write_text(
                json.dumps(console_events, ensure_ascii=False, indent=2), encoding="utf-8")
            (evidence_dir / "build-hash.json").write_text(
                json.dumps(report["buildHash"], ensure_ascii=False, indent=2), encoding="utf-8")
            (evidence_dir / "report.json").write_text(
                json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
            browser.close()


def main(argv=None):
    parser = argparse.ArgumentParser(description="Pacing rebalance real-touch acceptance")
    parser.add_argument("--url", help="Existing WebGL base URL; otherwise serve the local build")
    parser.add_argument("--days", type=int, choices=(2, 3), default=2)
    parser.add_argument("--evidence-dir", type=Path, default=EVIDENCE)
    args = parser.parse_args(argv)

    args.evidence_dir.mkdir(parents=True, exist_ok=True)
    if args.url:
        run(args.url, days=args.days, evidence_dir=args.evidence_dir)
    else:
        with qa.serve(BUILD) as base:
            run(base, days=args.days, evidence_dir=args.evidence_dir)


if __name__ == "__main__":
    main()
