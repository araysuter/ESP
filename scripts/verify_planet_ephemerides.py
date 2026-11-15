#!/usr/bin/env python3
"""Validate bundled ESP1 tables; optionally compare independent Horizons half-day samples.
Run on the build host: python3 scripts/verify_planet_ephemerides.py --online
Online results include request URLs and measured errors, not regenerated reference data.
"""
import argparse, csv, datetime, io, json, math, struct, subprocess, urllib.parse
from pathlib import Path
from generate_planet_ephemerides import BODIES
ROOT = Path(__file__).resolve().parents[1]
UTC = datetime.timezone.utc
WHEN = datetime.datetime(2026, 11, 15, 12, tzinfo=UTC)

def sample(path):
    data = path.read_bytes()
    magic, year, hours, count = struct.unpack_from('<4siii', data)
    assert magic == b'ESP1' and hours == 24 and count > 6000, str(path)
    assert len(data) == 16 + 48 * count, str(path)
    rows = list(struct.iter_unpack('<6d', data[16:]))
    assert all(math.isfinite(v) for r in rows for v in r), str(path)
    dt = hours * 3600
    index = (WHEN - datetime.datetime(year, 1, 1, tzinfo=UTC)).total_seconds() / dt
    k = math.floor(index); u = index - k; a, b = rows[k:k+2]
    p = [(2*u**3-3*u*u+1)*a[j] + (u**3-2*u*u+u)*dt*a[j+3] + (-2*u**3+3*u*u)*b[j] + (u**3-u*u)*dt*b[j+3] for j in range(3)]
    v = [((6*u*u-6*u)*a[j]+(3*u*u-4*u+1)*dt*a[j+3]+(-6*u*u+6*u)*b[j]+(3*u*u-2*u)*dt*b[j+3])/dt for j in range(3)]
    return p, v, count

def reference(target):
    params = dict(format='text', COMMAND=target, EPHEM_TYPE='VECTORS', CENTER='500@399', START_TIME='2026-11-15 12:00', STOP_TIME='2026-11-15 12:01', STEP_SIZE='1 m', REF_PLANE='FRAME', OUT_UNITS='KM-S', VEC_TABLE='2', CSV_FORMAT='YES', TIME_TYPE='UT', VEC_CORR='NONE')
    url = 'https://ssd.jpl.nasa.gov/api/horizons.api?' + urllib.parse.urlencode({k: v if k == 'format' else "'"+v+"'" for k,v in params.items()})
    # curl uses the build host trust store; never disable certificate verification.
    text = subprocess.check_output(['curl', '--fail', '--silent', '--show-error', '--max-time', '45', url], text=True)
    if '$$SOE' not in text: raise RuntimeError(text[:500])
    row = next(csv.reader(io.StringIO(text.split('$$SOE')[1].split('$$EOE')[0].strip())))
    return [float(v)*1000 for v in row[2:8]], url

def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--online', action='store_true'); parser.add_argument('--output', type=Path); args=parser.parse_args()
    report = []
    for name, target in BODIES.items():
        p,v,count = sample(ROOT / 'UnityProject/Assets/ESP/Resources/SolarSystem' / (name+'Ephemeris.bytes'))
        result = dict(body=name, samples=count, utc=WHEN.isoformat())
        if args.online:
            ref,url=reference(target)
            pe=math.dist(p,ref[:3]); ve=math.dist(v,ref[3:])
            result.update(positionErrorMeters=pe, velocityErrorMetersPerSecond=ve, source=url, referenceStateSI=ref)
            assert pe < 25000 and ve < 1, result
        report.append(result); print(json.dumps(result), flush=True)
    if args.output: args.output.write_text(json.dumps(report, indent=2)+'\n')
    print('PASS: all 12 planetary tables')
if __name__ == '__main__': main()
