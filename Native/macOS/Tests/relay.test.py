#!/usr/bin/env python3
"""Isolated localhost integration test. Selects a nonexistent test app only."""
import json, os, subprocess, tempfile, time, urllib.request, urllib.error, sys
from pathlib import Path
binary = Path(__file__).resolve().parents[1] / ('Build/RelayHarness' if '--headless' in sys.argv else 'Build/FocusParkHelper.app/Contents/MacOS/FocusParkHelper')
with tempfile.TemporaryDirectory(prefix='focuspark-relay-') as temporary:
    token = 'isolated-test-token'
    Path(temporary, 'configuration.json').write_text(json.dumps({'apps':['com.focuspark.test.nonexistent'],'domains':[],'pairingToken':token}))
    environment = {**os.environ, 'FOCUSPARK_STATE_DIR':temporary}
    process = subprocess.Popen([str(binary), '--agent'], env=environment, stdout=subprocess.DEVNULL, stderr=subprocess.PIPE)
    def request(method, route, body=None, auth=token):
        data = None if body is None else json.dumps(body).encode()
        req = urllib.request.Request('http://127.0.0.1:43821'+route, data=data, method=method, headers={'Authorization':'Bearer '+auth,'Content-Type':'application/json'})
        try:
            with urllib.request.urlopen(req, timeout=2) as response: return response.status, json.load(response)
        except urllib.error.HTTPError as error: return error.code, json.load(error)
    try:
        for attempt in range(30):
            if process.poll() is not None: raise RuntimeError('Agent exited with code '+str(process.returncode)+': '+process.stderr.read().decode())
            try:
                status, payload=request('GET','/status')
                if status==200: break
            except (urllib.error.URLError, TimeoutError): time.sleep(.1)
        else: raise RuntimeError('Relay did not become available on 127.0.0.1:43821')
        assert 'pairingToken' not in payload
        assert request('GET','/status',auth='wrong')[0]==401
        ending=time.time()+1500
        assert request('POST','/start',{'sessionId':'test-one','endUnix':ending})[0]==200
        status,payload=request('GET','/status');assert payload['active'] and payload['sessionId']=='test-one' and payload['ready']
        assert request('POST','/start',{'sessionId':'test-one','endUnix':ending})[0]==200
        assert request('POST','/start',{'sessionId':'test-two','endUnix':ending})[0]==409
        assert request('POST','/stop',{'sessionId':'stale-test'})[0]==200
        assert request('GET','/status')[1]['active'], 'Stale stop must not clear a newer session'
        assert request('POST','/stop',{'sessionId':'test-one'})[0]==200
        assert not request('GET','/status')[1]['active']
        print('PASS: authenticated relay, no token leak, start/readiness, retry idempotency, conflict rejection, stop')
    finally:
        process.terminate()
        try: process.wait(timeout=3)
        except subprocess.TimeoutExpired: process.kill()
