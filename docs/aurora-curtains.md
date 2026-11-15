# Aurora curtains

The graphics settings expose **simulated Kp (0–9)**, **color boost (50–500%)**
and **intensity (50–500%)**. Defaults are Kp 5, 150% color and 300% intensity,
matching the enhanced photographic direction. They persist separately from the
removed aurora settings. These presentation settings do not modify flight physics
or mark a mission as cheated.

This is original procedural artwork, not live NOAA activity or a calibrated
emission model. The supplied photographs guided folded shapes, thin green lower
edges, field-aligned rays, translucent veils and red/violet upper regions; they
are not bundled textures. The same Earth-fixed effect appears in map, inset and
camera-relative flight views, with Earth depth occlusion and a night-side fade.

## Implementation

`AuroraCurtains` builds one immutable mesh per Earth representation: three
sheets in each hemisphere, 1,536 longitudinal segments and 12 height segments.
One material and one draw per Earth view; no per-frame mesh upload, particle
systems, scene-color copy, raymarch, bloom or render texture allocation. Vertex
waves fold the continuous sheets; derivative-filtered fragment waves produce
fine vertical striation. Additive emission leaves the background visible.
The flight atmosphere composites afterward.

The base is approximately 94–124 km above the ellipsoid and upper edges extend
roughly 240–395 km, varying along each sheet and with activity. The shader uses
an approximate fixed tilted dipole. Kp expands the oval equatorward and increases
emission, rather than merely scaling brightness. Color boost independently
widens the red/violet contribution and increases saturation; intensity scales
emitted light. This is an artistic approximation of the behavior described by
[NOAA's aurora tutorial](https://www.spaceweather.gov/content/aurora-tutorial)
and [University of Alaska Fairbanks viewing guidance](https://www.gi.alaska.edu/monitors/aurora-forecast/).

Animation follows actual integrated mission seconds during flight, including
pause and rate-limited time warp. Before launch it uses a shared elapsed
presentation clock, paused by menus. No aurora state feeds the flight core.

## Reproducible visual review

Build a development player with the normal `BuildMission.Build` entry point,
then launch with `-espAuroraReview -screen-width 1920 -screen-height 1080`.
The opt-in route exercises the actual UI slider ranges/callbacks, restores saved
preferences, and writes `aurora-*.png` into the player's persistent data folder.
It captures enhanced ground/orbit views, extremes, motion, Kp 0/9 and daylight
suppression. These are **presentation review cameras**, not orbital checkpoints
or simulated flight evidence. The route finishes with paired effect-off/on
frame-time samples and an `ESP_AURORA_OK` log marker.

## Qualification (November 15, 2026)

Unity 6000.6.9f1, macOS ARM development player, Metal on an Apple Paravirtual
GPU, 1920×1080. The custom shader compiled without errors. The full mission
player completed the review with `ESP_AURORA_OK`: real UI slider range/callback
checks, nonfinite input/clamping checks, and 13 screenshots. Ground and orbital
captures were visually inspected against the supplied reference direction;
iterations softened lower edges, added red/violet veils, varied ray heights and
softened edge-on folds. Color-boost and brightness extremes are separate captures.
The day-side test suppresses the aurora. The images are generated game renders,
not photographs; thin emission sheets remain an approximation of a volume.

Two alternating 180-frame samples per condition, after 40 warmup frames, VSync
and application cap off:

| Effect | p50 wall-frame ms | p95 wall-frame ms |
| --- | ---: | ---: |
| Off, first | 1.131 | 1.534 |
| On, first | 1.315 | 1.892 |
| Off, repeat | 1.147 | 1.480 |
| On, repeat | 1.284 | 1.807 |

This is `Time.unscaledDeltaTime` in a fixed orbital review camera, **not GPU
profiler timing or a whole-mission performance guarantee**. Other applications
were preserved during testing. Windows/D3D, integrated-GPU laptops and long
flight/warp visual rehearsals have not been qualified by this change.
