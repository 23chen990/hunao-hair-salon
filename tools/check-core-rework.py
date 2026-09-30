#!/usr/bin/env python3
"""Real Chromium touch coverage for the money-unlock / checkout continuation."""
import importlib.util
import json
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT=Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('mobile', ROOT/'tools/check-mobile-salon.py')
mobile=importlib.util.module_from_spec(spec);spec.loader.exec_module(mobile)
mobile.EVIDENCE=ROOT/'artifacts/core-rework/final-mobile'
mobile.EVIDENCE.mkdir(parents=True,exist_ok=True)

class DayEnded(Exception): pass

def reach(driver, target, radius=1):
    reached = driver.goto(target, radius)
    if not reached and driver.state['state'] == 'Result': raise DayEnded()
    assert reached, 'Could not reach target during business'
    return True

def pacing(history, day):
    frames=[s for s in history if s['day']==day and s['state']=='Business']
    total=low=0
    for a,b in zip(frames,frames[1:]):
        dt=max(0,(b['progress']-a['progress'])*180)
        total+=dt
        if a['activeCustomers']<=1: low+=dt
    arrivals={c['id'] for s in frames if s['progress']<=30/180 for c in s['customers']}
    built={}
    for name in ('BlowDryer','WashStation','HaircutChair'):
        first=next((s for s in frames if any(p['id']==name and p['unlocked'] for p in s['pads'])),None)
        if first:
            built[name]={'seconds':round(first['progress']*180,1),'completedOrders':first['completed']}
    return {'arrivalsWithin30Seconds':len(arrivals),'atMostOneActiveCustomerPercent':round(100*low/total,2),'built':built}

def play(driver):
    deadline=time.monotonic()+420
    while driver.state['state'] in ('Business','ClosingGrace') and time.monotonic()<deadline:
        driver.wait(.15);s=driver.state
        if s['working']>=0:continue
        if s['guided']>=0:
            c=next((c for c in s['customers'] if c['id']==s['guided']),None)
            if c:
                stations=driver.transfer_stations(s,c)
                if stations:
                    assert reach(driver, stations[0]['position'])
                    driver.action()
                    driver.shot('day%d-seated'%s['day'])
                    continue
        c=driver.choose_ready_service(s)
        if c:
            if c['need']=='Wash' and not c.get('foamRunning') and s['washRackWashKits']==0:
                point=mobile.SUPPLY_RACK if s['carriedWashKits'] else mobile.SUPPLY_SOURCE
                assert reach(driver, point,.7);driver.wait(1.1)
                driver.shot('restock');continue
            station=next(p for p in s['stations'] if p['id']==c['station'])
            assert reach(driver, station['position'])
            if driver.state.get('targetCustomer')==c['id'] and driver.state['available']:
                if driver.state.get("targetAction")=="Wash": driver.hold_action(1.5)
                else: driver.action()
                if c['need']=='Wash':driver.shot('washing')
                if c['need']=='Dry':driver.shot('drying')
            continue
        if s.get('checkoutWaiting',0):
            assert reach(driver, s['cashier'],.65)
            driver.until(lambda t:not t.get('checkoutWaiting') or t['state']=='Result',timeout=12)
            driver.shot('day%d-checkout-%d'%(s['day'],s['completed']))
            continue
        for c in s['customers']:
            if c.get('needsTransfer') and c['state']=='Serving' and driver.transfer_stations(s,c):
                point=driver.transfer_anchor(s,c)
                assert reach(driver, point['position'])
                driver.action();break
        else:
            pad=next((p for p in s.get('pads',[]) if p['visible'] and p['id'] in ('BlowDryer','WashStation','HaircutChair') and s['balance']>=p['cost']-p['paid']),None)
            if pad:
                assert reach(driver, pad['position'],.7)
                driver.until(lambda t:next(p for p in t['pads'] if p['id']==pad['id'])['unlocked'],timeout=8)
                driver.shot('unlocked-'+pad['id']);continue
            waiting=[c for c in s['customers'] if c['state']=='Waiting' and c['arrived'] and driver.transfer_stations(s,c)]
            if waiting and s['guided']<0:
                c=min(waiting,key=lambda c:c['patience'])
                assert reach(driver, c['position'],1)
                driver.action()
                driver.shot('day%d-greet'%s['day'])
    driver.until(lambda s:s['state']=='Result',timeout=15)
    driver.shot('day%d-result'%driver.state['day'])

def run(base, pw):
    browser=pw.chromium.launch(channel='chromium',headless=True)
    driver=mobile.MobileDriver(mobile.qa.new_mobile_page(browser))
    try:
        driver.load(base+'/?mobileEvidence=1')
        assert driver.state['balance']==100
        assert [p['id'] for p in driver.state['stations'] if p['usable']]==[1]
        assert [p['id'] for p in driver.state['pads'] if p['visible']]==['BlowDryer']
        driver.shot('01-cut-only-opening')
        results=[]
        for day in (1,2):
            driver.tap_button('开始营业');driver.until(lambda s:s['state']=='Business')
            try: play(driver)
            except DayEnded:
                driver.until(lambda s:s['state']=='Result')
                driver.shot('day%d-result'%driver.state['day'])
            assert driver.state['completed']>=driver.state['target']
            results.append({k:driver.state[k] for k in ('day','completed','balance','checkoutAbandoned','satisfaction')})
            driver.until(lambda s:any('进入闭店经营' in b['label'] for b in s['buttons']))
            driver.shot('day%d-result'%day)
            driver.tap_button('进入闭店经营')
            balance=driver.state['balance'];stock=driver.state['washRackWashKits']
            driver.reload();driver.until(lambda s:s['state']=='ClosedManagement')
            assert driver.state['balance']==balance and driver.state['washRackWashKits']==stock
            if day==1:
                driver.tap_button('准备下一天');driver.until(lambda s:s['state']=='PreOpen')
        assert next(p for p in driver.state['pads'] if p['id']=='BlowDryer')['unlocked']
        assert next(p for p in driver.state['pads'] if p['id']=='WashStation')['unlocked']
        assert not driver.errors,driver.errors
        rhythm=[pacing(driver.history,day) for day in (1,2)]
        assert all(r['arrivalsWithin30Seconds']>=3 and r['atMostOneActiveCustomerPercent']<10 for r in rhythm),rhythm
        driver.result={'passed':True,'days':results,'pacing':rhythm,'balanceAndStockPersistedAfterReload':True,'errors':driver.errors}
        print(json.dumps(driver.result,ensure_ascii=False),flush=True)
    except Exception as error:
        driver.result={'passed':False,'error':str(error),'errors':driver.errors,'state':driver.state}
        driver.shot('failure')
        print(json.dumps(driver.result,ensure_ascii=False),flush=True)
        raise
    finally:
        driver.save();browser.close()

if __name__ == '__main__':
    with mobile.qa.serve(ROOT/'unity-hair-salon/Builds/WebGLDemo') as base, sync_playwright() as pw:
        run(base, pw)
