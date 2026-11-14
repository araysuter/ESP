#!/usr/bin/env python3
"""Fetch a checked NASA GEOS-FP native-level analysis at one grid point."""
import argparse, concurrent.futures, datetime as dt, json, math, re, sys, urllib.parse, urllib.request
BASE='https://opendap.nccs.nasa.gov/dods/GEOS-5/fp/0.25_deg/assim/inst3_3d_asm_Nv'
UTC=dt.timezone.utc
VARS=('h','pl','t','u','v')

def get(url):
    with urllib.request.urlopen(url, timeout=15) as r: data=r.read(500001)
    if len(data)>500000: raise ValueError('response exceeds limit')
    return data.decode('utf-8')

def attr(block,name):
    m=re.search(r'\b'+re.escape(name)+r'\s+"([^"]+)"',block)
    if not m: raise ValueError('missing metadata '+name)
    return m.group(1)

def select_time(start,end,step,now):
    if step.total_seconds()<=0 or end<start or now<start: raise ValueError('invalid time metadata')
    when=min(end,start+((now-start)//step)*step)
    if (end-start)%step or now-when>dt.timedelta(hours=12): raise ValueError('stale or misaligned NASA analysis time')
    return when,(when-start)//step

def parse_response(text,name,when,lat,lon):
    # The GrADS ASCII representation lists one grid then its coordinate maps.
    if not text.startswith(name+', '+('[1][1][1]' if name=='ps' else '[1][72][1][1]')): raise ValueError('unexpected response variable or shape')
    sections=re.split(r'(?m)^(?=(?:time|lev|lat|lon), \[)',text)
    rows=re.findall(r'(?m)^\[([^\n]*?)\],\s*([-+0-9.eE]+)',sections[0])
    expected=['0][0'] if name=='ps' else [f'0][{i}][0' for i in range(72)]
    if [i for i,_ in rows]!=expected: raise ValueError('invalid data indices')
    vals=[float(x) for _,x in rows]
    maps={}
    for section in sections[1:]:
        m=re.match(r'(time|lev|lat|lon), \[(\d+)\]\s*\n([^\n]+)',section)
        if not m or m.group(1) in maps: raise ValueError('invalid coordinate map')
        values=[float(x.strip()) for x in m.group(3).split(',')]
        if len(values)!=int(m.group(2)) or not all(math.isfinite(x) for x in values): raise ValueError('invalid coordinate values')
        maps[m.group(1)]=values
    expected_time=when.toordinal()+1+((when.hour*3600+when.minute*60+when.second)/86400)
    def check(key,value):
        if len(maps.get(key,[]))!=1 or abs(maps[key][0]-value)>1e-5: raise ValueError('mismatched '+key)
    check('time',expected_time);check('lat',lat);check('lon',lon)
    if name=='ps':
        if len(vals)!=1 or 'lev' in maps: raise ValueError('invalid surface shape')
    else:
        if len(vals)!=72 or maps.get('lev')!=list(range(1,73)): raise ValueError('invalid vertical shape or level coordinates')
    return vals

def build_levels(data, minimum_levels=2):
    ps=data['ps'][0]
    if not math.isfinite(ps) or not 10000<ps<120000: raise ValueError('invalid surface pressure')
    levels=[]
    for h,p,t,u,v in zip(*(data[x] for x in VARS)):
        if not all(math.isfinite(x) and abs(x)<1e14 for x in (h,p,t,u,v)) or p<=0 or p>ps or not 100<t<400 or h< -1000 or h>120000: continue
        z=6371000*h/(6371000-h)
        levels.append(dict(geometricAltitudeMeters=z,temperatureKelvin=t,pressurePascals=p,windEastMetersPerSecond=u,windNorthMetersPerSecond=v))
    levels.sort(key=lambda x:x['geometricAltitudeMeters'])
    if len(levels)<minimum_levels or any(a['geometricAltitudeMeters']>=b['geometricAltitudeMeters'] or a['pressurePascals']<=b['pressurePascals'] for a,b in zip(levels,levels[1:])): raise ValueError('invalid or nonmonotonic profile')
    return levels

def run(lat,lon):
    if not (math.isfinite(lat) and math.isfinite(lon) and -90<=lat<=90 and -180<=lon<=180): raise ValueError('coordinates outside valid range')
    das=get(BASE+'.das'); block=re.search(r'\btime\s*\{([^}]*)\}',das).group(1)
    start=dt.datetime.strptime(attr(block,'minimum'),'%Hz%d%b%Y').replace(tzinfo=UTC)
    end=dt.datetime.strptime(attr(block,'maximum'),'%Hz%d%b%Y').replace(tzinfo=UTC)
    step=dt.timedelta(minutes=int(re.fullmatch(r'(\d+)mn',attr(block,'grads_step')).group(1)))
    when,idx=select_time(start,end,step,dt.datetime.now(UTC))
    yi=max(0,min(720,round((lat+90)/.25))); xi=round(((lon+180)%360)/.3125)%1152
    glat=-90+yi*.25;glon=-180+xi*.3125
    point=f'[{idx}][0:1:71][{yi}][{xi}]'
    queries=[f'{v}{point}' for v in VARS]+[f'ps[{idx}][{yi}][{xi}]']
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool:
        responses=list(pool.map(lambda q:get(BASE+'.ascii?'+urllib.parse.quote(q,safe=',:[]')),queries))
    data={name:parse_response(response,name,when,glat,glon) for name,response in zip((*VARS,'ps'),responses)}
    levels=build_levels(data, minimum_levels=36)
    return dict(nativeLevels=72,retainedLevels=len(levels),droppedLevels=72-len(levels),provider='NASA GEOS-FP native-level assimilation',validTimeUtc=when.isoformat().replace('+00:00','Z'),latitudeDegrees=lat,longitudeDegrees=lon,gridLatitudeDegrees=glat,gridLongitudeDegrees=glon,levels=levels)

def main():
    p=argparse.ArgumentParser();p.add_argument('latitude',type=float);p.add_argument('longitude',type=float);a=p.parse_args()
    try: print(json.dumps(run(a.latitude,a.longitude),allow_nan=False))
    except Exception as e: print('NASA weather fetch failed: '+str(e),file=sys.stderr);return 1
    return 0
if __name__=='__main__': sys.exit(main())
