#!/usr/bin/env python3
"""Generate offline geocentric ICRF state vectors from NASA/JPL Horizons (UTC)."""
import csv, io, struct, urllib.parse, urllib.request
from pathlib import Path
START=2020
END=2037
ROOT=Path(__file__).resolve().parents[1]

def get(body, start, stop, step):
    q=dict(format='text', COMMAND="'%s'"%body, EPHEM_TYPE="'VECTORS'", CENTER="'500@399'",
        START_TIME="'%04d-01-01'"%start, STOP_TIME="'%04d-01-01'"%stop,
        STEP_SIZE="'%s'"%step, REF_PLANE="'FRAME'", OUT_UNITS="'KM-S'",
        VEC_TABLE="'2'", CSV_FORMAT="'YES'", TIME_TYPE="'UT'")
    url='https://ssd.jpl.nasa.gov/api/horizons.api?'+urllib.parse.urlencode(q)
    with urllib.request.urlopen(url,timeout=180) as response: text=response.read().decode('utf-8')
    if '$$SOE' not in text or '$$EOE' not in text: raise RuntimeError(text[:1500])
    lines=text.split('$$SOE',1)[1].split('$$EOE',1)[0].strip()
    for row in csv.reader(io.StringIO(lines)):
        if row: yield tuple(float(v)*1000 for v in row[2:8])

def write(body, step, hours, filename):
    records=[]
    for start in range(START,END,4):
        stop=min(start+4,END)
        print('Downloading',body,start,stop,flush=True)
        chunk=list(get(body,start,stop,step))
        if records: chunk=chunk[1:]
        records.extend(chunk)
    out=ROOT/'UnityProject'/'Assets'/'ESP'/'Resources'/filename
    with out.open('wb') as f:
        f.write(struct.pack('<4siii',b'ESP1',START,hours,len(records)))
        for row in records: f.write(struct.pack('<6d',*row))
    print(out.name,len(records),out.stat().st_size,flush=True)

if __name__=='__main__':
    write(301,'6 h',6,'MoonEphemeris.bytes')
    write(10,'1 d',24,'SunEphemeris.bytes')
