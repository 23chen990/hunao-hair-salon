#!/usr/bin/env python3
"""Actual Chromium evidence for the authored slice, inside HairSalonDemo.
Uses the real wash/cut/dry model and existing customer views; automation is development-only.
"""
import importlib.util, json, pathlib, argparse, time, re
from playwright.sync_api import sync_playwright
ROOT=pathlib.Path(__file__).resolve().parents[1]
spec=importlib.util.spec_from_file_location('salon_browser', ROOT/'tools/browser-check.py')
b=importlib.util.module_from_spec(spec);spec.loader.exec_module(b)
OUT=ROOT/'unity-hair-salon/Builds/WashCraft/evidence'
OUT.mkdir(parents=True,exist_ok=True)

def page_for(browser,base,query,record=False):
 context=browser.new_context(viewport=b.VIEWPORT,device_scale_factor=1,is_mobile=True,has_touch=True,user_agent=b.MOBILE_USER_AGENT,
  record_video_dir=str(OUT/'video') if record else None,
  record_video_size=b.VIEWPORT if record else None)
 page=context.new_page();messages=[];errors=[];failed=[]
 page.on('console',lambda msg:(messages.append(msg.text),errors.append(msg.text) if msg.type=='error' else None))
 page.on('pageerror',lambda err:errors.append(str(err)))
 page.on('requestfailed',lambda req:failed.append(req.url))
 page.goto(base+'/'+query,wait_until='domcontentloaded',timeout=60000)
 b.wait_for_canvas(page,0)
 return context,page,messages,errors,failed

def shot(page,name):
 path=OUT/(name+'.png');page.screenshot(path=str(path));b.assert_screenshot(path);b.assert_no_magenta_shader_fallback(path)
 return str(path.relative_to(ROOT))

def finish(context,page,messages,errors,failed,captures):
 data={'screenshots':captures,'canvas':page.evaluate('''() => {const c=document.querySelector('#unity-canvas');return {width:c.width,height:c.height,devicePixelRatio:devicePixelRatio}}'''),
  'errors':errors,'failedRequests':failed,'markers':[m for m in messages if 'WASH_CRAFT_' in m or 'BROWSER_CORE_FLOW_' in m]}
 video=page.video
 context.close()
 if video:data['video']=str(pathlib.Path(video.path()).relative_to(ROOT))
 if errors or failed:raise AssertionError(json.dumps(data,ensure_ascii=False))
 return data

def main():
 parser=argparse.ArgumentParser();parser.add_argument('--quick',action='store_true');args=parser.parse_args()
 report={'reference':'unity-hair-salon/Docs/VisualReferences/salon-overview-operations-reference.png','viewport':b.VIEWPORT,'runs':{}}
 with sync_playwright() as p:
  browser=p.chromium.launch(headless=True,args=['--use-gl=angle','--use-angle=swiftshader','--enable-webgl','--ignore-gpu-blocklist'])
  with b.serve(b.BUILD_ROOT/'WebGLDemo') as base:
   for mode,query in [('overview','?browserSmoke=1'),('detail','?washCraft=detail')]:
    ctx,page,messages,errors,failed=page_for(browser,base,query)
    b.wait_for_marker(page,messages,'WASH_CRAFT_READY',90)
    b.wait_for_marker(page,messages,'BROWSER_CORE_FLOW_PASS' if mode=='overview' else 'WASH_CRAFT_DETAIL_READY',90)
    page.wait_for_timeout(12000 if mode=='detail' else 1500)
    report['runs'][mode]=finish(ctx,page,messages,errors,failed,[shot(page,mode+'-844x390')])
   if not args.quick:
    for station in (0,4):
     ctx,page,messages,errors,failed=page_for(browser,base,f'?washCraft=service&station={station}',True)
     captures=[]
     for marker,name in [('WASH_CRAFT_SEATED','seated'),('WASH_CRAFT_SERVICE_ACTIVE','washing'),('WASH_CRAFT_WASH_DONE','towel'),('WASH_CRAFT_FLOW_PASS','completed')]:
      try:
       ready=b.wait_for_marker(page,messages,marker,90)
      except Exception:
       failure={'station':station,'waitingFor':marker,'errors':errors,'failedRequests':failed,'messages':messages,
                'screenshot':shot(page,f'station-{station}-failure')}
       (OUT/'service-failure.json').write_text(json.dumps(failure,ensure_ascii=False,indent=2))
       ctx.close()
       raise AssertionError(json.dumps({'station':station,'waitingFor':marker,'errors':errors},ensure_ascii=False))
      if marker=='WASH_CRAFT_SEATED':
       arrival=re.search(r'stylistArrivalError=([0-9.]+)',ready)
       assert arrival and float(arrival.group(1))<=.15, 'Stylist did not arrive at the service anchor: '+ready
      captures.append(shot(page,f'station-{station}-{name}-844x390'))
     report['runs'][f'station-{station}']=finish(ctx,page,messages,errors,failed,captures)
  if not args.quick:
   with b.serve(b.BUILD_ROOT/'WebGLAssetLab') as base:
    ctx,page,messages,errors,failed=page_for(browser,base,'?assetId=furniture-wash-station-crafted&view=inspect&diagnostics=preview&ui=0')
    b.wait_for_marker(page,messages,'ASSET_LAB_READY',90);page.wait_for_timeout(12000)
    report['runs']['asset-lab']=finish(ctx,page,messages,errors,failed,[shot(page,'asset-lab-model-844x390')])
  browser.close()
 (OUT/'browser-report.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
 print(json.dumps({'status':'passed','runs':list(report['runs']),'report':str(OUT/'browser-report.json')},ensure_ascii=False))
if __name__=='__main__':main()
