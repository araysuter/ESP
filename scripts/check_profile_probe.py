"""Check the historical NASA fixture actually reaches the C# force path."""
import json
import math
import sys
from pathlib import Path

result = json.loads(Path(sys.argv[1]).read_text())
assert result['mode'] == 'historical'
assert result['usesWeather'] is True, result.get('fallbackReason')
assert result['sourceValidTimeUtc'] == '2026-11-14T09:00:00Z'
assert len(result['points']) == 4
point = result['points'][1]
assert point['altitudeMeters'] == 1000
assert math.isclose(point['densityKgPerCubicMeter'], 1.1130901140481677, rel_tol=1e-8)
assert math.isclose(point['dragForceNewtons'][0], 940.0365318790012, rel_tol=1e-8)
assert math.isclose(point['dragForceNewtons'][1], -2172.6410745452795, rel_tol=1e-8)
assert point['dragForceNewtons'][2] < 0
print('NASA historical profile reached density and crosswind drag checks.')
