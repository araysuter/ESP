# Mission integration acceptance checklist

Record the build revision, platform, test time, site, weather source and result
for each run. This is a proposed checklist; unchecked items are not verified.
Use a packaged build as well as the editor when available.

## Map, site and launch

- [ ] Select each launch-site marker from the visible globe. Confirm the chosen
  name and coordinates match the marker and launch scene. Rotate the globe:
  a marker on the far side must not be visible or clickable through Earth.
  Click empty space and overlapping markers to check hit-testing.
- [ ] Switch map → launch → map repeatedly, change site and launch again.
  Confirm the pad, camera and vehicle correspond to the latest choice; no old
  detached booster, marker, camera or weather data survives reset. Watch object
  counts or memory over repeated rebuilds for unbounded growth.
- [ ] With a text field focused and with the pointer over each panel, try drag,
  scroll, site selection and launch controls. UI interaction must not also move
  the globe or trigger flight. Check labels and controls at target resolution.

## Weather handoff

- [ ] At a known site, inspect the actual data handed to flight: requested and
  returned grid coordinates, provider, valid UTC time, measurement height and
  units. Identify whether it is a frozen launch snapshot or refreshed in flight;
  UI and physics must agree. Stale, missing and offline data must be explicit.
- [ ] Inject a 10 m/s wind **from north**: local east/up/north velocity must be
  (0, 0, -10) m/s. A wind **from east** must be (-10, 0, 0) m/s. Verify 36 km/h
  converts to 10 m/s if that unit is accepted, and that drag uses air-relative
  velocity with the correct frame and sign.
- [ ] Inject 101325 Pa and 15 °C at sea level: for dry air, density should be
  about 1.225 kg/m³ using ρ = p/(287.05 T), T in kelvin. Check a non-sea-level
  site uses local pressure or an explicitly documented atmospheric model; mean
  sea-level pressure is not local station pressure. Verify altitude variation
  and any humidity approximation are labeled.
- [ ] Disable network and inject missing, malformed and expired responses.
  Launch must use the documented deterministic fallback or be explicitly
  blocked; show which values are used and never silently substitute zeros.
  Switch sites while a request is pending and confirm late results cannot be
  applied to the new site.

## Flight truth

- [ ] Confirm WGS84 site placement in meters and Earth rotation are converted
  into the flight core's stated inertial frame. Physics state remains double
  precision at a fixed step; rendering subtracts the camera origin in double
  precision before conversion to Unity floats. Verify axis/attitude conventions
  across ECEF, inertial and local frames at more than one site.
- [ ] Run gravity/orbit and frame round-trip checks, atmosphere samples, fuel
  cutoff, staging continuity and step convergence. Verify a successful ascent
  and a failed suborbital run against numerical results, not visuals alone.
  Display true mission elapsed time and time rate. Claim orbit only when the
  calculated trajectory meets the project's defined orbital criteria; label
  any checkpoint with the verified run from which it came.
