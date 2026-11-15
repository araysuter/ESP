# Flight camera and space visuals

Drag the flight view outside the controls to orbit the vehicle. Scroll to zoom.
The camera follows the surviving stage's visible mesh bounds and ignores plume
geometry when framing. It keeps the chosen orbit angle across M view changes;
mouse capture ends on focus loss or window exit. WASD still commands the vehicle,
not the camera. A red triangle replaces the enlarged model in the orbital map.

The flight view shares the map's active ground imagery and cloud data. Surface
and Clouds use the bundled 8192 × 4096 NASA base (16× the former texel count).
The globe display mesh now has 512 × 256 segments. This improves orbital-view
detail and the horizon, not physical terrain accuracy; collision remains WGS84.
Satellite mode retains its dated 2048 × 1024 mosaic. See [imagery](earth-data.md).

Map and space flight use a deterministic original star field: 2,400 soft stars,
4,800 triangles per camera, no HDRI download or network dependency. These are
fictional directions, not an astronomical catalog. Stars follow camera position
without following its rotation and are depth-occluded by Earth. The field is a
readability choice; it is not a physically calibrated exposure simulation.

The scaled-space backdrop camera updates after the vehicle camera; its sunlight
is transformed into the same Earth frame. It uses layer 31, excluded from the
foreground camera. Backdrop objects and materials are released on view changes.

## VM qualification

The macOS ARM package compiled with Unity 6000.6.9f1. Cloud interpolation checks
passed for clear/missing distinction, constant fractions, longitude wrapping,
polar consistency and dated weighting. A packaged Starship launch reached
verified orbit at T+529.32 s and 239.03 km using standard-atmosphere fallback.
Native mouse input changed the view around the vehicle; stars stayed behind
Earth. The NASA cloud blend retrieved November 12–14 observations, with 99.978%
of spherical area having original observations and 3,466 missing raster pixels.
The missing-area figure describes data availability, not cloud coverage.

The plume responds to simulated pressure and actual engine flow. A real orbital
burn at 100% was captured near 237 km; see [plume limits](plume-presentation.md).
No local compilation or rendering was used. Windows/Intel macOS and calibrated
real-world plume accuracy are unverified.
