#!/usr/bin/env python3
"""Real touch regression for escorting, manual drying and wash-side interaction."""
import importlib.util
import argparse
import json
import math
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
spec = importlib.util.spec_from_file_location('core', ROOT / 'tools/check-core-rework.py')
core = importlib.util.module_from_spec(spec)
spec.loader.exec_module(core)
mobile = core.mobile
EVIDENCE = ROOT / 'artifacts/current-bug-fix/browser'
mobile.EVIDENCE = EVIDENCE

def distance(a, b):
    return math.hypot(a['x'] - b['x'], a['z'] - b['z'])

def customer(driver, ident):
    return next(c for c in driver.state['customers'] if c['id'] == ident)

def check_follow(browser, base):
    driver = mobile.MobileDriver(mobile.qa.new_mobile_page(browser))
    try:
        driver.load(base + '/?mobileEvidence=1')
        driver.tap_button('开始营业')
        driver.until(lambda s: any(c['state'] == 'Waiting' and c['arrived'] for c in s['customers']))
        first = next(c for c in driver.state['customers'] if c['state'] == 'Waiting' and c['arrived'])
        driver.goto(first['position'], .5)
        driver.action()
        driver.until(lambda s: s['guided'] >= 0)
        ident = driver.state['guided']
        driver.shot('01-received-customer')
        stops = []
        for index, point in enumerate(({'x': -6, 'z': -3.2}, {'x': -4, 'z': -1.5}, {'x': -4, 'z': 1.5})):
            start = len(driver.history)
            assert driver.goto(point, .55)
            driver.wait(1.5)
            frames = [s for s in driver.history[start:] if s['guided'] == ident]
            assert frames
            gaps = [distance(s['player'], next(c for c in s['customers'] if c['id'] == ident)['position']) for s in frames]
            assert max(gaps) < 2.8, ('customer lost the leader', gaps)
            settled = dict(customer(driver, ident)['position'])
            driver.wait(.8)
            drift = distance(settled, customer(driver, ident)['position'])
            gap = distance(driver.state['player'], customer(driver, ident)['position'])
            assert drift < .06 and .9 < gap < 1.4, (drift, gap)
            stops.append({'maxMovingGap': max(gaps), 'stoppedGap': gap, 'drift': drift})
            driver.shot('02-follow-turn-%d' % index)
        driver.tap_button('取消接待')
        driver.until(lambda s: s['guided'] < 0)
        driver.wait(3)
        driver.goto(customer(driver, ident)['position'], .35)
        driver.until(lambda s: s['targetCustomer'] == ident and s['targetAction'] == 'Greet')
        driver.action()
        driver.until(lambda s: s['guided'] == ident)
        driver.goto({'x': -5, 'z': -3.5}, .5)
        driver.wait(1.8)
        assert distance(driver.state['player'], customer(driver, ident)['position']) < 1.4
        driver.shot('03-reselected-customer')
        assert not driver.errors, driver.errors
        (EVIDENCE / 'follow-states.json').write_text(json.dumps(driver.history, ensure_ascii=False))
        return {'passed': True, 'stops': stops, 'cancelAndReselect': True, 'errors': driver.errors}
    except Exception:
        driver.shot('follow-failure')
        (EVIDENCE / 'follow-states.json').write_text(json.dumps(driver.history, ensure_ascii=False))
        raise
    finally:
        driver.page.close()

class BugDriver(mobile.MobileDriver):
    def __init__(self, page):
        super().__init__(page)
        self.manual_check = None
        self.wash_assign_sides = []
        self.wash_service_sides = []

    def check_wash_sides(self, expected_action, results):
        ident = self.state['targetCustomer']
        # Primary wash-bed approaches already covered by the runtime fixture.
        for name, point in (('front', {'x': -7.2, 'z': 2.6}),
                            ('right', {'x': -5.6, 'z': 4.1}),
                            ('left', {'x': -8.8, 'z': 4.1})):
            assert self.goto(point, .35)
            self.until(lambda s: s['targetCustomer'] == ident and s['targetAction'] == expected_action and s['available'], 4)
            results.append({'side': name, 'action': self.state['action'], 'available': self.state['available'], 'player': self.state['player']})
            self.shot('wash-%s-%s' % (expected_action, name))

    def action(self):
        if self.state['targetAction'] == 'Assign' and not self.wash_assign_sides:
            c = customer(self, self.state['guided'])
            if c['need'] == 'Wash':
                self.check_wash_sides('Assign', self.wash_assign_sides)
        if self.state['targetAction'] != 'StartDry' or self.state['purchased'] or self.manual_check:
            return super().action()
        ident = self.state['targetCustomer']
        self.release()
        self.wait(.35)
        point = self.state['interaction']
        def press():
            self.cdp.send('Input.dispatchTouchEvent', {'type': 'touchStart', 'touchPoints': [
                {'id': 2, 'x': point['x'], 'y': 390 - point['y'], 'radiusX': 5, 'radiusY': 5}]})
        def release():
            self.cdp.send('Input.dispatchTouchEvent', {'type': 'touchEnd', 'touchPoints': []})
        press()
        self.wait(1.25)
        c = customer(self, ident)
        assert c['activeAction'] == 'ManualBlow' and not c['autoRunning'] and self.state['working'] == ident
        self.shot('manual-dry-holding')
        held_elapsed = self.state['workingElapsed']
        release()
        self.wait(.9)
        c = customer(self, ident)
        assert c['state'] == 'Serving' and not c['autoRunning'] and self.state['working'] < 0
        self.shot('manual-dry-released-early')
        press()
        self.until(lambda s: s['working'] == ident and s['workingElapsed'] >= 7.3, 12)
        self.shot('manual-dry-ready')
        release()
        self.wait(.8)
        c = customer(self, ident)
        assert not c['autoRunning'] and (c['need'] != 'Dry' or c['complete'])
        self.manual_check = {'passed': True, 'day': self.state['day'], 'hasAutoStand': self.state['purchased'],
                             'manualWhileHeld': True, 'heldElapsed': held_elapsed, 'releaseAndResume': True}

    def hold_action(self, seconds):
        if self.state['targetAction'] == 'Wash' and not self.wash_service_sides:
            self.check_wash_sides('Wash', self.wash_service_sides)
        return super().hold_action(seconds)

def main():
    global EVIDENCE
    parser = argparse.ArgumentParser()
    parser.add_argument('--follow-only', action='store_true')
    parser.add_argument('--evidence-dir', type=Path)
    args = parser.parse_args()
    if args.evidence_dir:
        EVIDENCE = args.evidence_dir.resolve()
        mobile.EVIDENCE = EVIDENCE
    EVIDENCE.mkdir(parents=True, exist_ok=True)
    with mobile.qa.serve(ROOT / 'unity-hair-salon/Builds/WebGLDemo') as base, sync_playwright() as pw:
        browser = pw.chromium.launch(channel='chromium', headless=True, args=['--enable-webgl'])
        driver = None
        report = {'passed': False}
        try:
            report['follow'] = check_follow(browser, base)
            if args.follow_only:
                report['passed'] = True
                return
            driver = BugDriver(mobile.qa.new_mobile_page(browser))
            driver.load(base + '/?mobileEvidence=1')
            report['days'] = []
            for day in (1, 2):
                driver.tap_button('开始营业')
                try:
                    core.play(driver)
                except core.DayEnded:
                    driver.until(lambda s: s['state'] == 'Result')
                assert driver.state['completed'] >= driver.state['target'], driver.state
                report['days'].append({k: driver.state[k] for k in ('day', 'completed', 'target', 'balance', 'checkoutAbandoned')})
                driver.shot('result-day-%d' % day)
                driver.tap_button('进入闭店经营')
                balance, stock = driver.state['balance'], driver.state['washRackWashKits']
                driver.reload()
                driver.until(lambda s: s['state'] == 'ClosedManagement')
                assert driver.state['balance'] == balance and driver.state['washRackWashKits'] == stock
                if day == 1:
                    driver.tap_button('准备下一天')
                    driver.until(lambda s: s['state'] == 'PreOpen')
            assert driver.manual_check and driver.manual_check['day'] == 1
            assert len(driver.wash_assign_sides) == len(driver.wash_service_sides) == 3
            assert not driver.errors, driver.errors
            report.update(passed=True, manualDry=driver.manual_check, washAssign=driver.wash_assign_sides,
                          washService=driver.wash_service_sides, progressPersisted=True, errors=driver.errors)
        except Exception as error:
            report['error'] = str(error)
            if driver:
                driver.shot('failure')
            raise
        finally:
            if driver:
                driver.result = report
                driver.save()
            (EVIDENCE / 'report.json').write_text(json.dumps(report, ensure_ascii=False, indent=2))
            print(json.dumps(report, ensure_ascii=False), flush=True)
            browser.close()

if __name__ == '__main__':
    main()
