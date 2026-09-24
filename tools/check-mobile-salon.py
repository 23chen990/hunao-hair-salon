#!/usr/bin/env python3
"""Exercise the mobile Demo with real Chromium touch events, never gameplay injection."""
import argparse
import heapq
import importlib.util
import json
import math
import time
from pathlib import Path
from playwright.sync_api import sync_playwright

ROOT = Path(__file__).resolve().parents[1]
EVIDENCE = ROOT / 'unity-hair-salon/Builds/MobileEvidence'
MAX_DAY_WALL_SECONDS = 8 * 60
MAX_GOTO_WALL_SECONDS = 45
spec = importlib.util.spec_from_file_location('salon_browser', ROOT/'tools/browser-check.py')
qa = importlib.util.module_from_spec(spec)
spec.loader.exec_module(qa)

class MobileDriver:
    def __init__(self, page):
        self.page = page
        self.cdp = page.context.new_cdp_session(page)
        self.state = None
        self.errors = []
        self.history = []
        self.actions = []
        self.first_leaving = None
        self.second_customer_greeted = None
        self.second_customer_reception_flow = None
        self.touch = None
        self.recorded = set()
        self.result = {"passed":False}
        self.satisfaction_persisted = None
        self.last_progress_log = None
        page.on('console', self.console)
        page.on('pageerror', lambda error: self.add_error(str(error)))

    def add_error(self, value):
        # Chromium headless has no orientation sensor; Unity's optional mobile
        # orientation lock reports this capability miss even though the canvas
        # and all gameplay resources are healthy.
        if 'screen.orientation.lock() is not available' in value:
            return
        self.errors.append(value)

    def console(self, message):
        value = message.text
        if '[MOBILE_STATE] ' in value:
            try:
                self.state = json.loads(value.split('[MOBILE_STATE] ', 1)[1])
                self.state["state"] = {"Starting":"PreOpen", "Shop":"ClosedManagement"}.get(self.state["state"],self.state["state"])
                self.history.append(self.state)
                self.track_first_leaving(self.state)
                progress_key = (self.state.get('day'), self.state['state'],
                                int((self.progress(self.state) or 0) * 5))
                if progress_key != self.last_progress_log:
                    self.last_progress_log = progress_key
                    print('MOBILE ' + json.dumps({key:self.state.get(key) for key in
                        ('day', 'state', 'progress', 'completed', 'target', 'balance', 'satisfaction')},
                        ensure_ascii=False), flush=True)
            except ValueError:
                pass
        if message.type == 'error' or 'Exception:' in value or "Coroutine couldn't" in value:
            self.add_error(value)

    def wait(self, seconds):
        self.page.wait_for_timeout(seconds * 1000)

    @staticmethod
    def progress(state):
        """Read the current business progress from old or extended telemetry."""
        if not state:
            return None
        candidates = [state.get('progress'), state.get('businessProgress'),
                      state.get('dayProgress')]
        telemetry = state.get('telemetry')
        if isinstance(telemetry, dict):
            candidates.extend((telemetry.get('progress'), telemetry.get('businessProgress'),
                               telemetry.get('dayProgress')))
        for value in candidates:
            if isinstance(value, (int, float)) and math.isfinite(value):
                return max(0.0, min(1.0, float(value)))
        return None

    @staticmethod
    def satisfaction(state):
        """Read the satisfaction value added by the current mobile telemetry."""
        if not state:
            return None
        for key in ('satisfaction', 'shopSatisfaction', 'satisfactionStars'):
            value = state.get(key)
            if isinstance(value, (int, float)) and math.isfinite(value):
                return value
        telemetry = state.get('telemetry')
        if isinstance(telemetry, dict):
            for key in ('satisfaction', 'shopSatisfaction', 'satisfactionStars'):
                value = telemetry.get(key)
                if isinstance(value, (int, float)) and math.isfinite(value):
                    return value
        return None

    def until(self, predicate, timeout=45):
        limit = time.monotonic() + timeout
        while time.monotonic() < limit:
            if self.state and predicate(self.state): return self.state
            self.wait(.1)
        raise AssertionError('State condition timed out: '+json.dumps(self.state, ensure_ascii=False))

    def load(self, url):
        """Synchronize navigation with the Unity canvas and first fresh telemetry."""
        self.state = None
        self.page.goto(url, wait_until='commit', timeout=60000)
        self.page.wait_for_selector('#unity-canvas', state='visible', timeout=60000)
        return self.until(lambda s: s.get('state') == 'PreOpen', timeout=60)

    def reload(self, url=None):
        """Reload and discard the previous state before accepting new telemetry."""
        self.release()
        self.state = None
        if url is None:
            self.page.reload(wait_until='domcontentloaded', timeout=60000)
        else:
            self.page.goto(url, wait_until='domcontentloaded', timeout=60000)
        # Navigation can deliver the old Unity instance's final console event
        # while Playwright is waiting for the new document. Clear it only after
        # the navigation has committed, then accept telemetry from the new canvas.
        self.state = None
        self.page.wait_for_selector('#unity-canvas', state='visible', timeout=60000)
        return self.until(lambda s: True, timeout=60)

    def tap_button(self, text):
        # The first Unity telemetry after reload can precede Canvas layout.
        # Wait for an on-screen target and hold through a rendered input frame.
        self.until(lambda s: any(text in b['label'] and b['available'] and
            0 < b['position']['x'] < 844 and 0 < b['position']['y'] < 390
            for b in s['buttons']))
        self.wait(.35)
        item = next(b for b in self.state['buttons'] if text in b['label'] and b['available'])
        p = item['position']
        self.tap(p['x'], 390-p['y'])
        self.actions.append({'tap': text,
                             'targetCustomer': self.state.get('targetCustomer', -1),
                             'targetAction': self.state.get('targetAction', 'None')})
        self.wait(.4)

    def tap(self, x, y):
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchStart', 'touchPoints':[
            {'id':2, 'x':x, 'y':y, 'radiusX':5, 'radiusY':5}]})
        self.wait(.12)
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchEnd', 'touchPoints':[]})

    def action(self):
        self.release()
        self.wait(.35)
        label = self.state['action']
        self.actions.append({'action': label, 'player':self.state['player'],
                             'targetCustomer':self.state.get('targetCustomer', -1),
                             'targetAction':self.state.get('targetAction', 'None'),
                             'progress':self.progress(self.state),
                             'completed':self.state.get('completed')})
        p = self.state['interaction']
        if self.state.get('targetAction') == 'Cut':
            # Mobile haircuts use the existing hold timing window. Development
            # telemetry exposes the required tool so this real-touch check can
            # exercise a clean pass for each configured timing range.
            hold_seconds = .95 if self.state.get('targetTool') == 'Clippers' else 1.8
            self.hold_action(hold_seconds)
        else:
            self.tap(p['x'], 390-p['y'])
            self.wait(.4)

    def hold_action(self, seconds):
        p = self.state['interaction']
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchStart','touchPoints':[
            {'id':2, 'x':p['x'], 'y':390-p['y'], 'radiusX':5, 'radiusY':5}]})
        self.wait(seconds)
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchEnd','touchPoints':[]})
        self.wait(.42)

    def haircut_checks(self):
        def first_haircut():
            self.tap_button('开始营业')
            self.until(lambda s:any(c['id']==0 and c['state']=='Waiting' and c['arrived'] for c in s['customers']))
            customer = next(c for c in self.state['customers'] if c['id']==0)
            assert self.goto(customer['position'])
            self.action()
            self.until(lambda s:s['guided']==0)
            station = next(p for p in self.state['stations'] if p['type']=='Haircut' and not p['occupied'])
            assert self.goto(station['position'])
            self.action()
            self.until(lambda s:s['targetCustomer']==0 and s['targetAction']=='Cut' and s['available'])

        first_haircut()
        self.hold_action(.15)
        self.until(lambda s:s['working']==-1)
        customer = next(c for c in self.state['customers'] if c['id']==0)
        assert customer['state']=='Serving' and customer['hairStage']=='Trimmed'
        assert self.state['completed']==0 and self.state['balance']==0
        self.shot('11-early-release-retry')

        p = self.state['interaction']
        finger = {'id':2,'x':p['x'],'y':390-p['y'],'radiusX':5,'radiusY':5}
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchStart','touchPoints':[finger]})
        self.wait(.75)
        settings = next(b for b in self.state['buttons'] if 'Settings' in b['label'])['position']
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchStart','touchPoints':[
            finger, {'id':3,'x':settings['x'],'y':390-settings['y'],'radiusX':5,'radiusY':5}]})
        self.wait(.12)
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchEnd','touchPoints':[]})
        self.until(lambda s:s['paused'])
        elapsed = self.state['workingElapsed']
        customer_satisfaction = next(c['satisfaction'] for c in self.state['customers'] if c['id']==0)
        self.wait(.5)
        assert self.state['workingElapsed']==elapsed
        self.tap_button('继续营业')
        self.wait(.5)
        assert self.state['working']==0 and self.state['workingSuspended']
        assert next(c['satisfaction'] for c in self.state['customers'] if c['id']==0)==customer_satisfaction
        self.hold_action(max(.15,1.8-elapsed))
        self.until(lambda s:s['completed']==1 and s['working']==-1)
        self.shot('12-resumed-haircut')

        self.reload()
        first_haircut()
        self.hold_action(2.8)
        self.until(lambda s:s['working']==-1)
        customer = next(c for c in self.state['customers'] if c['id']==0)
        assert customer['serviceResult']=='Failed'
        assert self.state['completed']==0 and self.state['balance']==0
        self.shot('13-overcut-no-goal-credit')
        self.actions.append({'haircutChecks':'early release allows retry; pause retains hold; overcut gives no goal credit'})
        self.reload()

    @staticmethod
    def service_ready(customer):
        return (customer['state']=='Serving' and customer['arrived']
            and customer.get('activeAction','None')=='None'
            and (not customer.get('foamRunning') or customer.get('foamReady'))
            and (not (customer['autoRunning'] or customer['autoStopped'])
                 or customer['autoElapsed']>=customer['autoReady']))

    def joystick(self, dx, dy):
        p = self.state['joystick']
        cx, cy = p['x'], 390-p['y']
        if not self.touch:
            self.touch = {'id':1, 'x':cx, 'y':cy, 'radiusX':5, 'radiusY':5}
            self.cdp.send('Input.dispatchTouchEvent', {'type':'touchStart','touchPoints':[self.touch]})
            self.wait(.07)
        # Convert desired analog strength through the control's .16 deadzone.
        # The 300 reference-pixel circle is 120 screen pixels at this viewport.
        strength = min(1.0, math.hypot(dx, dy))
        travel = 60 * (.16 + .84 * strength) / max(.001, math.hypot(dx, dy))
        self.touch = {'id':1, 'x':cx+dx*travel, 'y':cy-dy*travel, 'radiusX':5, 'radiusY':5}
        self.cdp.send('Input.dispatchTouchEvent', {'type':'touchMove','touchPoints':[self.touch]})

    def release(self):
        if self.touch:
            self.cdp.send('Input.dispatchTouchEvent', {'type':'touchEnd','touchPoints':[]})
            self.touch = None
            self.wait(.08)

    def shot(self, name):
        self.release()
        EVIDENCE.mkdir(parents=True, exist_ok=True)
        self.page.screenshot(path=str(EVIDENCE / (name+'.png')))
        self.recorded.add(name)

    def track_first_leaving(self, state):
        customer = next((c for c in state.get('customers', [])
                         if c.get('id') == 0 and c.get('state') in ('Leaving', 'Exited')), None)
        if customer is None:
            return
        screen = customer.get('screen', {})
        position = customer.get('position', {})
        if self.first_leaving is None:
            self.first_leaving = {
                'startScreen': {'x': screen.get('x', 0), 'y': screen.get('y', 0)},
                'maxScreenDisplacementPx': 0.0,
                'closestExitDistance': float('inf'),
                'exit': {'x': -10.8, 'z': 5.9},
                'screenshot': '14-first-customer-leaving.png'
            }
            if state.get('state') != 'Result':
                self.shot('14-first-customer-leaving')
        start = self.first_leaving['startScreen']
        self.first_leaving['maxScreenDisplacementPx'] = max(
            self.first_leaving['maxScreenDisplacementPx'],
            math.dist((start['x'], start['y']), (screen.get('x', 0), screen.get('y', 0))))
        exit_position = self.first_leaving['exit']
        exit_distance = math.dist((position.get('x', 0), position.get('z', 0)),
                                  (exit_position['x'], exit_position['z']))
        self.first_leaving['closestExitDistance'] = min(
            self.first_leaving['closestExitDistance'], exit_distance)

    @staticmethod
    def visible_interaction_button(state):
        label = state.get('action', '')
        return next((button for button in state.get('buttons', [])
                     if button.get('label') == label), None)

    @staticmethod
    def reception_target_snapshot(state):
        snapshot = {key:state.get(key) for key in
                    ('targetCustomer', 'targetAction', 'available', 'action', 'guided', 'player')}
        button = MobileDriver.visible_interaction_button(state)
        snapshot['interactionButton'] = None if button is None else {
            'label': button.get('label'), 'available': button.get('available')}
        return snapshot

    def receive_second_customer_during_first_exit(self, second):
        """Use real touch input to greet O002 and assign its first wash step."""
        first_before_greet = next((c for c in self.state['customers'] if c.get('id') == 0), None)
        assert first_before_greet is not None and first_before_greet.get('state') == 'Leaving', \
            'The O002 touch sequence must overlap the first customer\'s Leaving state.'
        assert second['need'] == 'Wash', \
            'Day 1 customer 2 must be the deterministic O002 wash order: '+str(second)

        self.until(lambda state: state.get('targetCustomer') == 1 and
                   state.get('targetAction') == 'Greet' and state.get('available'), timeout=20)
        greet_before = self.reception_target_snapshot(self.state)
        assert greet_before['action'] == '接待 2 号', \
            'The available Greet target must identify customer 2: '+str(greet_before)
        assert greet_before['interactionButton'] == {'label': '接待 2 号', 'available': True}, \
            'The visible interaction button must expose the enabled Greet action: '+str(greet_before)
        self.shot('15-second-customer-greet-ready')

        actions_before_greet = len(self.actions)
        self.action()
        greet_touch = self.actions[actions_before_greet]
        assert greet_touch.get('targetCustomer') == 1 and greet_touch.get('targetAction') == 'Greet', \
            'The real interaction touch was not sent to customer 2 Greet: '+str(greet_touch)

        self.until(lambda state: state.get('guided') == 1 and
                   state.get('targetCustomer') == 1 and
                   state.get('targetAction') == 'Assign' and
                   not state.get('available') and
                   '前往洗发工位' in state.get('action', ''), timeout=8)
        guided_button = self.visible_interaction_button(self.state)
        assert guided_button is not None and guided_button.get('label') == '前往洗发工位' and \
               guided_button.get('available') is False, \
            'The visible disabled button must keep the O002 wash destination visible: '+str(self.state)
        second_after_greet = next((c for c in self.state['customers'] if c.get('id') == 1), None)
        assert second_after_greet is not None and second_after_greet.get('state') == 'Waiting', \
            'After Greet, O002 must remain selected and be guided to its wash station: '+str(self.state)
        guided_prompt = self.reception_target_snapshot(self.state)
        self.shot('16-second-customer-guided-to-wash')

        wash_stations = [station for station in self.state['stations']
                         if station.get('type') == 'Wash' and not station.get('occupied')]
        assert wash_stations, 'O002 cannot continue because every compatible wash station is occupied.'

        def route_distance(station):
            route = [(self.state['player']['x'], self.state['player']['z'])] + \
                self.route(station['position'], radius=1.45)
            return sum(math.dist(a, b) for a, b in zip(route, route[1:]))

        wash_stations.sort(key=route_distance)
        travel_history_index = len(self.history)
        station = None
        station_arrival = None
        for candidate in wash_stations:
            assert self.goto(candidate['position'], radius=1.35), \
                'The player could not reach a compatible wash station while guiding customer 2.'
            player = self.state['player']
            distance = math.dist((player['x'], player['z']),
                                 (candidate['position']['x'], candidate['position']['z']))
            if distance > 1.5:
                continue
            self.until(lambda state: state.get('targetCustomer') == 1 and
                       state.get('targetAction') == 'Assign' and state.get('available') and
                       '安排洗发' in state.get('action', ''), timeout=5)
            station = candidate
            station_arrival = self.reception_target_snapshot(self.state)
            break
        assert station is not None, \
            'No free wash anchor was reachable within the 1.5 unit assignment radius.'
        travel_samples = [sample for sample in self.history[travel_history_index:]
                          if sample.get('guided') == 1]
        assert travel_samples, 'No live telemetry was observed while guiding customer 2 to the wash station.'
        assert all(sample.get('targetCustomer') == 1 and
                   sample.get('targetAction') == 'Assign' and
                   any(label in sample.get('action', '')
                       for label in ('前往洗发工位', '安排洗发'))
                   for sample in travel_samples), \
            'The visible target stopped identifying O002 during the walk: '+str(
                [self.reception_target_snapshot(sample) for sample in travel_samples])
        assert all((button := self.visible_interaction_button(sample)) is not None and
                   button.get('label') == sample.get('action') and
                   button.get('available') == sample.get('available')
                   for sample in travel_samples), \
            'The actual interaction button diverged from O002 guided target telemetry.'
        assert any(not sample.get('available') and '前往洗发工位' in sample.get('action', '')
                   for sample in travel_samples), \
            'The real touch run did not expose the guided O002 wash destination before arrival.'

        assign_before = station_arrival
        assert assign_before['interactionButton'] == {'label': '安排洗发', 'available': True}, \
            'The visible interaction button must enable O002 wash assignment at the anchor: '+str(assign_before)
        self.shot('17-second-customer-wash-assign-ready')
        actions_before_assign = len(self.actions)
        self.action()
        assign_touch = self.actions[actions_before_assign]
        assert assign_touch.get('targetCustomer') == 1 and assign_touch.get('targetAction') == 'Assign', \
            'The real interaction touch was not sent to customer 2 wash Assign: '+str(assign_touch)

        self.until(lambda state: any(c.get('id') == 1 and
                     c.get('state') in ('MovingToStation', 'Serving') and
                     c.get('station') == station['id'] for c in state['customers']), timeout=8)
        second_after_assign = next(c for c in self.state['customers'] if c.get('id') == 1)
        self.shot('18-second-customer-assigned-to-wash')
        self.second_customer_reception_flow = {
            'passed': True,
            'whileFirstLeaving': True,
            'customerId': 1,
            'order': 'O002',
            'initialNeed': 'Wash',
            'greetBeforeTouch': greet_before,
            'greetTouch': greet_touch,
            'guidedTarget': guided_prompt,
            'guidedTelemetryFrames': len(travel_samples),
            'assignBeforeTouch': assign_before,
            'assignTouch': assign_touch,
            'assignedCustomer': {key:second_after_assign.get(key) for key in
                                 ('id', 'state', 'station', 'need', 'position', 'arrived')},
            'washStationId': station['id'],
            'screenshots': {
                'greetReady': '15-second-customer-greet-ready.png',
                'guidedToWash': '16-second-customer-guided-to-wash.png',
                'assignReady': '17-second-customer-wash-assign-ready.png',
                'assigned': '18-second-customer-assigned-to-wash.png',
            },
        }
        self.second_customer_greeted = {
            'passed': True,
            'whileFirstLeaving': True,
            'targetCustomer': 1,
            'targetAction': 'Greet',
            'guided': self.state.get('guided'),
            'customerState': second_after_greet.get('state'),
            'order': 'O002',
            'washAssigned': second_after_assign.get('state') in ('MovingToStation', 'Serving'),
        }

    @staticmethod
    def rect(r):
        x = r.get('x',r.get('m_XMin',0)); y=r.get('y',r.get('m_YMin',0))
        return x,y,x+r.get('width',r.get('m_Width',0)),y+r.get('height',r.get('m_Height',0))

    def walkable(self, p):
        x,z=p; lo,bot,hi,top=self.rect(self.state['floor'])
        if not lo<=x<=hi or not bot<=z<=top: return False
        return not any(a-.025<x<b+.025 and c-.025<z<d+.025
            for a,c,b,d in map(self.rect,self.state['obstacles']))

    def route(self, target, radius=.6):
        start=(self.state['player']['x'],self.state['player']['z'])
        goal=(target['x'],target['z']); size=.24
        key=lambda p:(round(p[0]/size),round(p[1]/size))
        pos=lambda k:(k[0]*size,k[1]*size)
        origin=key(start); queue=[(0,origin)]; costs={origin:0}; came={}; found=None
        while queue:
            _,node=heapq.heappop(queue); p=pos(node)
            if math.dist(p,goal)<=radius and self.walkable(p):found=node;break
            for dx,dz in ((1,0),(-1,0),(0,1),(0,-1),(1,1),(1,-1),(-1,1),(-1,-1)):
                nxt=(node[0]+dx,node[1]+dz); q=pos(nxt)
                if not self.walkable(q):continue
                if dx and dz and (not self.walkable((p[0],q[1])) or not self.walkable((q[0],p[1]))):continue
                cost=costs[node]+math.hypot(dx,dz)
                if cost>=costs.get(nxt,1e9):continue
                costs[nxt]=cost;came[nxt]=node
                heapq.heappush(queue,(cost+math.dist(q,goal)/size,nxt))
        if found is None:raise AssertionError('No walkable route to '+str(goal))
        path=[]
        while found!=origin:path.append(pos(found));found=came[found]
        path.reverse()
        simplified=[]
        current=start
        while path:
            far=0
            for i,q in enumerate(path):
                steps=max(1,math.ceil(math.dist(current,q)/.07))
                if all(self.walkable((current[0]+(q[0]-current[0])*j/steps,current[1]+(q[1]-current[1])*j/steps)) for j in range(1,steps+1)):
                    far=i
                else:break
            current=path[far];simplified.append(current);path=path[far+1:]
        return simplified

    def goto(self, target, radius=1.0, timeout=MAX_GOTO_WALL_SECONDS):
        path=self.route(target,radius)
        deadline=time.monotonic()+min(timeout, MAX_GOTO_WALL_SECONDS)
        while path and time.monotonic()<deadline:
            self.wait(.05)
            if self.state['state'] not in ('Business', 'ClosingGrace'):break
            p=self.state['player']; point=(p['x'],p['z'])
            if math.dist(point, (target['x'], target['z'])) <= radius:break
            while path and math.dist(point,path[0])<.42:path.pop(0)
            if not path:break
            x,z=path[0]; dx=x-point[0]; dz=z-point[1]; length=math.hypot(dx,dz)
            right=self.state['cameraRight'];forward=self.state['cameraForward']
            rl=math.hypot(right['x'],right['z']);fl=math.hypot(forward['x'],forward['z'])
            # HairSalonDemo's approved 2D mode moves in world +Z while the
            # rendered camera looks vertically down, so its telemetry has no
            # useful horizontal forward projection. Keep the browser driver
            # aligned with the runtime's actual movement basis.
            if fl < .01:
                forward={'x':0.0,'z':1.0}
                fl=1.0
            # Smaller analog input near corners prevents stale telemetry from overshooting.
            speed=max(.18, min(.85, length / 4))
            self.joystick(speed*(dx*right['x']+dz*right['z'])/(length*rl),
                          speed*(dx*forward['x']+dz*forward['z'])/(length*fl))
            self.wait(.12)
        self.release();self.wait(.35)
        if time.monotonic() >= deadline and path and self.state['state'] in ('Business', 'ClosingGrace'):
            raise AssertionError('Movement got stuck: '+str((self.state['player'],target,path[:4],
                                                             'progress='+str(self.progress(self.state)))))
        return self.state['state'] in ('Business', 'ClosingGrace')

    def input_checks(self):
        self.until(lambda s:len(s['customers'])>0)
        initial=dict(self.state['player'])
        # A tap on a distant person cannot assign a chair or move the player.
        customer=self.state['customers'][0]
        point=customer['screen']
        self.page.touchscreen.tap(point['x'],390-point['y']);self.wait(.4)
        assert self.state['guided']==-1
        assert self.state['player']==initial
        self.joystick(.7,0);self.wait(.45)
        first=dict(self.state['player'])
        button=self.state['interaction']
        action={'id':2,'x':button['x'],'y':390-button['y'],'radiusX':5,'radiusY':5}
        self.cdp.send('Input.dispatchTouchEvent',{'type':'touchStart','touchPoints':[self.touch,action]})
        self.wait(.12)
        self.cdp.send('Input.dispatchTouchEvent',{'type':'touchEnd','touchPoints':[self.touch]})
        self.wait(.4)
        second=dict(self.state['player'])
        assert math.dist((initial['x'],initial['z']),(first['x'],first['z']))>.25
        assert math.dist((first['x'],first['z']),(second['x'],second['z']))>.25
        self.release();self.wait(.4);still=dict(self.state['player']);self.wait(.4)
        assert self.state['player']==still
        self.tap_button('Settings');self.until(lambda s:s['paused'])
        progress=self.state['progress'];position=dict(self.state['player']);self.wait(.6)
        assert self.state['progress']==progress and self.state['player']==position
        self.tap_button('继续营业');self.until(lambda s:not s['paused'])
        self.wait(.4);assert self.state['player']==position
        self.shot('02-touch-controls')
        self.actions.append({'inputChecks':'remote tap blocked, multitouch independent, release stopped, pause froze and reset'})

    def play_day(self):
        deadline=time.monotonic()+MAX_DAY_WALL_SECONDS
        while time.monotonic()<deadline and self.state['state'] in ('Business','ClosingGrace'):
            self.wait(.15);s=self.state
            if any(c['autoRunning'] for c in s['customers']) and '03-background' not in self.recorded:self.shot('03-background')
            if s['working']>=0 and any(c['autoRunning'] and c['id']!=s['working'] for c in s['customers']) and '03b-interleaved' not in self.recorded:
                self.shot('03b-interleaved')
            if len([c for c in s['customers'] if c['state']=='Waiting'])>=3 and '04-pressure' not in self.recorded:self.shot('04-pressure')
            first = next((c for c in s['customers'] if c['id']==0), None)
            second = next((c for c in s['customers'] if c['id']==1), None)
            if (self.second_customer_greeted is None and first is not None and
                    first['state'] in ('Finished', 'Leaving') and second is not None and
                    second['state']=='Waiting' and second['arrived'] and s['guided'] < 0 and
                    s['working'] < 0):
                if not self.goto(second['position'], radius=1.0):
                    break
                self.until(lambda state:
                    any(c.get('id') == 0 and c.get('state') == 'Leaving'
                        for c in state.get('customers', [])) and
                    any(c.get('id') == 1 and c.get('state') == 'Waiting' and c.get('arrived')
                        for c in state.get('customers', [])), timeout=8)
                second = next(c for c in self.state['customers'] if c.get('id') == 1)
                self.receive_second_customer_during_first_exit(second)
                continue
            if s['working']>=0:continue
            if s['guided']>=0:
                customer=next((c for c in s['customers'] if c['id']==s['guided']),None)
                if not customer:continue
                kind='Wash' if customer['need']=='Wash' else 'Haircut'
                stations=[p for p in s['stations'] if p['type']==kind and not p['occupied']]
                if not stations:
                    # Free a chair while keeping the guided customer. Only
                    # cancel when another customer must first be transferred.
                    finishing=[c for c in s['customers'] if self.service_ready(c) and not c.get('needsTransfer')]
                    if finishing:
                        c=min(finishing,key=lambda c:c['patience'])
                        station=next(p for p in s['stations'] if p['id']==c['station'])
                        if not self.goto(station['position']): break
                        self.action()
                    else:
                        self.tap_button('取消接待')
                    continue
                def walking_distance(station):
                    route = [(s['player']['x'], s['player']['z'])] + self.route(station['position'], 1)
                    return sum(math.dist(a,b) for a,b in zip(route, route[1:]))
                station=min(stations,key=walking_distance)
                if not self.goto(station['position']): break
                self.action();continue
            # Start the newly seated background dryer before committing to
            # the other chair's haircut. This explicitly exercises the
            # intended overlap within the day's finite order quota.
            if any(c['need']=='Dry' and (c['state']=='MovingToStation' or
                (c['state']=='Serving' and not c['arrived'])) for c in s['customers']):
                continue
            ready=[c for c in s['customers'] if self.service_ready(c)]
            if ready:
                # Finish a waiting foreground service while the dryer still
                # provides a safe background window. A stopped dryer is urgent.
                c=min(ready,key=lambda c:(not c['autoStopped'],not c.get('foamReady'),
                    not (c['need']=='Dry' and not c['autoRunning']),c['autoRunning'],c['patience']))
                station=next(p for p in s['stations'] if p['id']==c['station'])
                if not self.goto(station['position']): break
                self.action();continue
            # Let a newly assigned NPC finish its short walk before sending
            # the stylist across the room for another queue entry. This keeps
            # the real-touch run focused on one active handoff at a time.
            if any(c['state']=='MovingToStation' or (c['state']=='Serving' and not c['arrived']) for c in s['customers']):
                continue
            waiting=[c for c in s['customers'] if c['state']=='Waiting' and c['arrived'] and
                any(p['type']==('Wash' if c['need']=='Wash' else 'Haircut') and not p['occupied'] for p in s['stations'])]
            if waiting:
                c=min(waiting,key=lambda c:c['patience'])
                if not self.goto(c['position'],radius=1.0): break
                self.action()
        self.release()
        if self.state['state'] == 'Result':
            return
        remaining=deadline-time.monotonic()
        if remaining <= 0:
            raise AssertionError('Day did not reach Result before wall timeout: '+json.dumps({
                'state':self.state.get('state'), 'progress':self.progress(self.state),
                'completed':self.state.get('completed'), 'target':self.state.get('target')},
                ensure_ascii=False))
        self.until(lambda s:s['state']=='Result',timeout=remaining)

    def save(self):
        EVIDENCE.mkdir(parents=True,exist_ok=True)
        (EVIDENCE/'states.json').write_text(json.dumps(self.history,ensure_ascii=False))
        (EVIDENCE/'actions.json').write_text(json.dumps(self.actions,ensure_ascii=False,indent=2))
        (EVIDENCE/'report.json').write_text(json.dumps(self.result,ensure_ascii=False,indent=2))
        (EVIDENCE/'errors.json').write_text(json.dumps(self.errors,ensure_ascii=False,indent=2))


def run(base, interactive=False, failure=False, haircut_checks=True):
    EVIDENCE.mkdir(parents=True,exist_ok=True)
    with sync_playwright() as p:
        # Full Chromium's new headless mode uses the normal GPU path on macOS.
        # headless_shell defaults to software WebGL, which can starve this
        # real-time touch driver when other builds or browsers are running.
        browser=p.chromium.launch(channel='chromium',headless=True,args=['--enable-webgl','--disable-web-security'])
        page=qa.new_mobile_page(browser);driver=MobileDriver(page)
        try:
            driver.load(base.rstrip('/')+'/?mobileEvidence=1');driver.shot('01-opening')
            if interactive:
                import sys
                print('READY '+json.dumps(driver.state,ensure_ascii=False),flush=True)
                for line in sys.stdin:
                    try:
                        command=json.loads(line);action=command['cmd']
                        if action=='quit':break
                        if action=='start':driver.tap_button('开始营业')
                        elif action=='wait':driver.wait(command.get('seconds',1))
                        elif action=='goto':driver.goto(command['target'],command.get('radius',1))
                        elif action=='action':driver.action()
                        elif action=='button':driver.tap_button(command['text'])
                        elif action=='shot':driver.shot(command['name'])
                        elif action=='play':driver.play_day()
                        elif action=='reload':driver.reload()
                        print('STATE '+json.dumps(driver.state,ensure_ascii=False),flush=True)
                    except Exception as e:print('ERROR '+str(e),flush=True)
            elif failure:
                opening_satisfaction=driver.satisfaction(driver.state)
                driver.tap_button('开始营业')
                driver.until(lambda s:s['state']=='Result',timeout=MAX_DAY_WALL_SECONDS)
                assert driver.state['completed']==0
                failed_satisfaction=driver.satisfaction(driver.state)
                driver.shot('09-failed-day')
                driver.tap_button('再试一次');driver.until(lambda s:s['state']=='PreOpen')
                assert driver.state['day']==1 and driver.state['balance']==0
                retry_satisfaction=driver.satisfaction(driver.state)
                if opening_satisfaction is not None and retry_satisfaction is not None:
                    assert retry_satisfaction == opening_satisfaction, \
                        'Failed-day retry did not restore the opening satisfaction.'
                    driver.satisfaction_persisted = True
                driver.shot('10-retry')
                driver.tap_button('开始营业');driver.until(lambda s:len(s['customers'])>0)
                assert driver.state['customers'][0]['id']==0 and driver.state['customers'][0]['need']=='Cut'
            else:
                if haircut_checks: driver.haircut_checks()
                driver.tap_button('开始营业');driver.until(lambda s:s['state']=='Business')
                driver.input_checks();driver.play_day();driver.shot('05-result')
                assert driver.state['completed']>=driver.state['target'], 'This touch run did not reach the day target'
                driver.tap_button('进入闭店经营');driver.shot('06-management')
                if any('购买' in b['label'] and b['available'] for b in driver.state['buttons']):
                    driver.tap_button('购买')
                    assert driver.state['purchased']
                balance=driver.state['balance'];day=driver.state['day'];purchased=driver.state['purchased']
                satisfaction=driver.satisfaction(driver.state)
                driver.reload();driver.until(lambda s:s['state']=='ClosedManagement')
                assert driver.state['balance']==balance and driver.state['day']==day and driver.state['purchased']==purchased
                reloaded_satisfaction=driver.satisfaction(driver.state)
                if satisfaction is not None and reloaded_satisfaction is not None:
                    assert reloaded_satisfaction == satisfaction, \
                        'Closed-management reload did not preserve satisfaction.'
                    driver.satisfaction_persisted = True
                driver.tap_button('准备下一天')
                driver.until(lambda s:s['state']=='PreOpen' and s['day']==day+1)
                driver.shot('07-next-day')
                driver.tap_button('开始营业');driver.until(lambda s:s['state']=='Business')
                driver.wait(4)
                driver.reload();driver.until(lambda s:s['state']=='PreOpen')
                assert driver.state['day']==day+1 and driver.state['balance']==balance
                driver.shot('08-midday-safe-restart')
            assert not driver.errors, 'Browser errors: '+str(driver.errors)
            for sample in driver.history:
                unfinished=sum(c['state'] in ('Entering','Waiting','MovingToStation','Serving')
                               for c in sample['customers'])
                assert sample['completed']+unfinished<=sample['target'], \
                    'Admission exceeded the remaining day goal: '+str((sample['completed'],unfinished,sample['target']))
            interleaved = any(item['working']>=0 and any(c['autoRunning'] and
                c['id']!=item['working'] for c in item['customers']) for item in driver.history)
            if not failure and not interactive:
                assert driver.first_leaving is not None, 'The first customer never entered Leaving during the touch run.'
                assert driver.first_leaving['maxScreenDisplacementPx'] >= 80, \
                    'Leaving route moved less than 80 screen pixels: '+str(driver.first_leaving)
                assert driver.first_leaving['closestExitDistance'] <= .12, \
                    'Leaving customer did not reach the configured exit: '+str(driver.first_leaving)
                assert driver.second_customer_greeted is not None, \
                    ('The second customer did not complete the real O002 Greet-to-Wash touch flow '
                     'while the first customer was Leaving.')
                driver.first_leaving['passed'] = True
                assert interleaved, 'Touch run did not demonstrate a foreground service during background blow-drying'
            results = {}
            for item in driver.history:
                if item['state'] == 'Result':
                    results[item['day']] = {key:item.get(key) for key in
                        ('completed', 'target', 'balance', 'satisfaction')}
            driver.result={
                'passed':True,'viewport':qa.VIEWPORT,'input':'Chromium real touch events',
                'failureRetry':failure,'finalDay':driver.state['day'],
                'admissionStayedWithinGoal':True,
                'haircutGestureChecks':haircut_checks and not failure and not interactive,
                'balance':driver.state['balance'],'upgraded':driver.state['purchased'],
                'satisfaction':driver.satisfaction(driver.state),
                'satisfactionPersisted':driver.satisfaction_persisted,
                'maxWaiting':max(sum(c['state']=='Waiting' for c in item['customers']) for item in driver.history),
                'manualCoinPickups':0,
                'paymentsAutoSettled':True,
                'firstCustomerLeavingTrajectory':driver.first_leaving,
                'secondCustomerGreetedWhileFirstLeaving':driver.second_customer_greeted,
                'secondCustomerReceptionFlow':driver.second_customer_reception_flow,
                'backgroundWhileOtherService':interleaved, 'dayResults':results,
                'framesObserved':len(driver.history),'actions':len(driver.actions),'errors':driver.errors
            }
        except Exception as error:
            driver.result={"passed":False,"error":str(error)}
            driver.shot('failure')
            raise
        finally:
            driver.save();browser.close()

if __name__=='__main__':
    parser=argparse.ArgumentParser();parser.add_argument('--url');parser.add_argument('--interactive',action='store_true');parser.add_argument('--failure',action='store_true');parser.add_argument('--evidence-dir',type=Path)
    parser.add_argument('--skip-haircut-checks',action='store_false',dest='haircut_checks',help='Reuse separate gesture evidence while checking the full day')
    args=parser.parse_args()
    if args.evidence_dir:EVIDENCE=args.evidence_dir.resolve()
    if args.url:run(args.url,args.interactive,args.failure,args.haircut_checks)
    else:
        with qa.serve(qa.BUILD_ROOT/'WebGLDemo') as base:run(base,args.interactive,args.failure,args.haircut_checks)
