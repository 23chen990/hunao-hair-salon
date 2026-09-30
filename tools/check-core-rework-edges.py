#!/usr/bin/env python3
"""Touch-only checks for service failures, held foam, restock and unpaid departures."""
import argparse,importlib.util,json,time
from pathlib import Path
from playwright.sync_api import sync_playwright
ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('mobile',ROOT/'tools/check-mobile-salon.py')
m=importlib.util.module_from_spec(spec);spec.loader.exec_module(m)
m.EVIDENCE=ROOT/'artifacts/core-rework/final-edges';m.EVIDENCE.mkdir(parents=True,exist_ok=True)

def seat(d, need='Cut'):
    d.until(lambda s:any(c['state']=='Waiting' and c['arrived'] and c['need']==need for c in s['customers']))
    c=next(c for c in d.state['customers'] if c['state']=='Waiting' and c['arrived'] and c['need']==need)
    # Approach just to the left of this customer so the nearby customer to
    # their right cannot steal the touch target. Use a reachable grid point.
    point=dict(c['position'])
    point['x']=round((point['x']-.4)/.24)*.24
    point['z']=round(point['z']/.24)*.24
    assert d.goto(point,.2)
    assert d.state['targetCustomer']==c['id'] and d.state['targetAction']=='Greet', d.state
    d.action()
    d.until(lambda s:s['guided']==c['id'])
    p=d.transfer_stations(d.state,c)[0]
    assert d.goto(p['position']);d.action()
    d.until(lambda s:any(t['id']==c['id'] and t['state']=='Serving' and t['arrived'] for t in s['customers']))
    return c['id']

def find(d,id):return next((c for c in d.state['customers'] if c['id']==id),None)

scenarios=('overcut','checkout-abandon','wash-and-restock','blow-timeout')
parser=argparse.ArgumentParser()
parser.add_argument('--scenario',choices=scenarios)
args=parser.parse_args()

with m.qa.serve(ROOT/'unity-hair-salon/Builds/WebGLDemo') as base,sync_playwright() as pw:
    browser=pw.chromium.launch(channel='chromium',headless=True)
    report_path=m.EVIDENCE/'report.json'
    report=json.loads(report_path.read_text()) if args.scenario and report_path.exists() else {}
    for scenario in ((args.scenario,) if args.scenario else scenarios):
        context=browser.new_context(user_agent=m.qa.MOBILE_USER_AGENT,viewport={'width':844,'height':390},device_scale_factor=1,is_mobile=True,has_touch=True)
        d=m.MobileDriver(context.new_page())
        try:
            seed='&mobileSeed=core-wash' if scenario in ('wash-and-restock','blow-timeout') else ''
            d.load(base+'/?mobileEvidence=1'+seed)
            d.tap_button('开始营业');d.until(lambda s:s['state']=='Business')
            if scenario in ('overcut','checkout-abandon'):
                id=seat(d)
                if scenario=='overcut':
                    d.hold_action(2.8)
                    d.until(lambda s:find(d,id) and find(d,id)['serviceResult']=='Failed')
                    assert find(d,id)['comedy']=='Overcut',find(d,id)
                    assert d.state['balance']==100 and d.state['completed']==0
                    d.shot('01-overcut-reaction')
                else:
                    d.action()
                    d.until(lambda s:s.get('checkoutWaiting',0)>0)
                    assert d.state['balance']==100
                    d.shot('02-waiting-to-pay')
                    d.until(lambda s:s.get('checkoutAbandoned',0)==1,timeout=35)
                    assert d.state['balance']==100
                    assert find(d,id)['comedy']=='CheckoutTooLong'
                    d.shot('03-unpaid-storm-out')
            else:
                id=seat(d,'Wash')
                d.hold_action(.3)
                d.wait(.6)
                assert d.state['working']==id and not find(d,id)['foamRunning']
                d.shot('04-foam-paused')
                d.hold_action(1.5)
                d.until(lambda s:find(d,id)['foamRunning'])
                assert d.state['supplyIntroduced'] and d.state['washRackWashKits']==1
                d.shot('05-foam-countdown')
                if scenario=='wash-and-restock':
                    assert d.goto(m.SUPPLY_SOURCE,.7);d.wait(1)
                    assert d.state['carriedWashKits']>0
                    d.shot('06-carrying-shampoo')
                    assert d.goto(m.SUPPLY_RACK,.7);d.wait(1)
                    assert d.state['washRackWashKits']>=2 and d.state['carriedWashKits']==0
                    d.shot('07-restock-delivered')
                    d.until(lambda s:find(d,id) and find(d,id)['comedy']=='FoamTooLong',timeout=18)
                    d.shot('08-foam-too-late')
                else:
                    d.until(lambda s:find(d,id)['foamReady'],timeout=10)
                    d.action()
                    d.until(lambda s:find(d,id)['need']=='Dry')
                    d.action()
                    d.until(lambda s:s['guided']==id)
                    p=d.transfer_stations(d.state,find(d,id))[0]
                    assert d.goto(p['position']);d.action();d.wait(.3);d.action()
                    d.until(lambda s:find(d,id)['autoRunning'])
                    d.until(lambda s:find(d,id)['autoStopped'],timeout=26)
                    assert find(d,id)['comedy']=='BlowTooLong'
                    d.shot('09-blow-timeout-reaction')
            assert not d.errors,d.errors
            report[scenario]={'passed':True,'errors':d.errors}
        except Exception as e:
            report[scenario]={'passed':False,'error':str(e),'errors':d.errors,'state':d.state}
            d.shot('failure-'+scenario)
            raise
        finally:
            (m.EVIDENCE/(scenario+'-states.json')).write_text(json.dumps(d.history,ensure_ascii=False))
            (m.EVIDENCE/'report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
            print(scenario,json.dumps(report[scenario],ensure_ascii=False),flush=True)
            context.close()
    browser.close()
