#!/usr/bin/env python3
"""Fetch public NASA DONKI data with a private key; optionally publish a key-free feed."""
import argparse, datetime as dt, json, math, os, urllib.error, urllib.request
UTC = dt.timezone.utc
MAX_BYTES = 2_000_000

def utc(value):
    result = dt.datetime.fromisoformat(value.replace('Z', '+00:00'))
    if result.tzinfo is None: raise ValueError('Timestamp must include timezone')
    return result.astimezone(UTC)

def feed(items, now):
    if not isinstance(items, list) or len(items) > 1000: raise ValueError('Invalid storm list')
    clean = []
    for item in items:
        if not isinstance(item, dict): raise ValueError('Invalid storm')
        start = utc(item['startTime'])
        if start > now + dt.timedelta(minutes=5): raise ValueError('Future storm timestamp')
        observations = item.get('allKpIndex') or []
        if not isinstance(observations, list): raise ValueError('Invalid observations')
        kp = []
        for observation in observations:
            value = observation['kpIndex']
            if isinstance(value, bool) or not isinstance(value, (float, int)) or not math.isfinite(value) or not 0 <= value <= 9:
                raise ValueError('Invalid Kp')
            observed = utc(observation['observedTime'])
            kp.append({'kpIndex': value, 'observedTime': observed.isoformat().replace('+00:00','Z')})
        # Allow-list fields: no upstream URLs, error bodies, credentials or arbitrary metadata.
        clean.append({'startTime': start.isoformat().replace('+00:00','Z'), 'allKpIndex': kp})
    return dict(schemaVersion=1, generatedUtc=now.isoformat().replace('+00:00','Z'),
                rangeStart=(now.date()-dt.timedelta(days=7)).isoformat(), rangeEnd=now.date().isoformat(),
                source='NASA DONKI GST', items=clean)

def request(url, headers, data=None, method=None):
    payload = None if data is None else json.dumps(data, allow_nan=False).encode()
    req = urllib.request.Request(url, data=payload, headers=headers, method=method)
    with urllib.request.urlopen(req, timeout=30) as response:
        raw = response.read(MAX_BYTES + 1)
    if len(raw) > MAX_BYTES: raise ValueError('Response too large')
    return json.loads(raw)

def publish(document):
    repo = os.environ['GITHUB_REPOSITORY']
    headers = {'Authorization': 'Bearer '+os.environ['GITHUB_TOKEN'], 'Accept': 'application/vnd.github+json', 'Content-Type':'application/json'}
    base = 'https://api.github.com/repos/'+repo+'/'
    def api(path, data=None, method=None): return request(base+path, headers, data, method)
    try: parent = api('git/ref/heads/nasa-data')['object']['sha']
    except urllib.error.HTTPError as error:
        if error.code != 404: raise
        parent = None
    # The data branch contains only public, allow-listed NASA observations.
    tree = api('git/trees', {'tree':[{'path':'donki.json','mode':'100644','type':'blob', 'content':json.dumps(document, allow_nan=False, separators=(',',':'))+'\n'}]})
    commit = api('git/commits', {'message':'Refresh public NASA DONKI observations', 'tree':tree['sha'], 'parents':[parent] if parent else []})
    if parent: api('git/refs/heads/nasa-data', {'sha':commit['sha'], 'force':False}, 'PATCH')
    else: api('git/refs', {'ref':'refs/heads/nasa-data','sha':commit['sha']})
    print('Published key-free NASA feed')

def main():
    parser=argparse.ArgumentParser(); parser.add_argument('--publish', action='store_true'); args=parser.parse_args()
    key=os.environ.get('WEATHER_API_KEY') or os.environ.get('NASA_API_KEY')
    if not key or key=='DEMO_KEY': raise ValueError('Configure WEATHER_API_KEY secret')
    now=dt.datetime.now(UTC).replace(microsecond=0)
    url='https://api.nasa.gov/DONKI/GST?startDate='+(now.date()-dt.timedelta(days=7)).isoformat()+'&endDate='+now.date().isoformat()
    document=feed(request(url, {'X-Api-Key':key}), now)
    print('Validated NASA GST observations:',len(document['items']))
    if args.publish: publish(document)

if __name__=='__main__':
    try: main()
    except Exception as error:
        # Never print request headers, upstream bodies, or exception text.
        print('NASA feed failed:',type(error).__name__)
        raise SystemExit(1)
