# Continuous flight rendering

Flight view retains one scaled WGS84 Earth backdrop from pad to space, with the
local spacecraft/pad camera overlaid in real meters. There is no altitude-triggered
camera replacement. Authoritative simulation remains double precision; the camera
and streamed ground are expressed relative to the vehicle in the pad's ENU frame.

The original atmosphere shader integrates eight exponential-density samples on
ellipsoid-clipped rays. It is a visual approximation, not measured weather or a
calibrated scattering model. Screen-ray reconstruction respects the GPU projection
flip on Metal. Local imagery has additional distance/grazing-angle haze because
the real-meter overlay draws after the scaled atmosphere. Global imagery remains
the fallback when local tiles are unavailable; see `local-surface-imagery.md`.

Reset restores camera framing, throttle, time rate, vehicle and environment pose,
and camera-stack membership. The orbit camera cannot move below the reference
surface. Destructive impact freezes the terminal flight state but leaves pause,
map and restart available. See `impact-effects.md` for visual failure behavior.

The map shows two distinct paths: muted-blue observed flight history behind the
vehicle and cyan instantaneous coast prediction ahead. History retains at most
1,024 observed double-precision positions, including the original pad position
and latest endpoint. All points are transformed at the current Earth epoch; this
is an inertial trajectory display, not a time-stamped ground track.

## Verification

- VM-only pure C# `FlightTrail.Tests` and `SurfaceTiles.Tests` passed; both also
  run in GitHub CI.
- Packaged macOS `-espResetSmoke -espDaylightReview` passed a genuine failed hop,
  destructive impact, restart, assisted ascent to 130 km, repeated map/site resets,
  and relaunch. Camera-stack/finite-state assertions run throughout.
- Captures at 5, 30, 60, 110, 119 and 121 km support visual review around the old
  120 km transition. Screenshot telemetry can lag the capture request by a frame.
- `-espDaylightReview` is explicitly labeled in screenshots and affects only the
  lighting review clock. It does not change flight state or shipping default time.
- Starbase imagery review loaded all 34 desired tiles with no active requests.
- The separate packaged Starship explosion fixture passed destructive impact,
  pause, expiry, reset, disabled damage and gentle landing.

These checks are not Windows qualification, an FPS benchmark, a high-resolution
terrain/collision model, or proof of accurate local atmospheric scattering.
