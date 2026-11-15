# SLS solid-booster smoke trail

`RocketSmokeTrail` is a cosmetic presentation component. Add it to the persistent
mission GameObject; assign `driver`, `director`, and `smokeShader` using the
serialized `Assets/ESP/Presentation/RocketSmokeTrail.shader` asset. Optionally
assign `sunlight` to the existing `MissionSunlight` (otherwise found on the same
GameObject). Smoke shading uses its solar elevation and Earth shadow, plus the
actual main light, with a dim 0.04 night ambient floor. Call
`Arm(simulation)` immediately after construction/configuration, before the first
fixed step. Call `Clear()` on mission reset; automatic simulation replacement and
mission-time rewind detection also clear the pool. `ActiveParticleCount` reports
the current count; `TotalEmittedCount` is cumulative for the component lifetime.

Emission requires the SLS preset and actual `simulation.BoosterBurning`. Both
existing solid-booster nozzle anchors are used. The cap is 256 billboards with at
most 32 paired emission events per rendered frame. Excess old emission is
discarded immediately, retaining fractional phase without building a backlog.
At sea-level density the pair emits four events per mission second; the visual
rate falls with density and becomes zero in vacuum. Existing puffs survive
burnout/jettison. The live exhaust mesh and flight core are unchanged.

Each puff has a 26-second mission-time lifetime. Time acceleration advances that
same clock; menu pause freezes it. After terminal impact freezes the core clock,
remaining smoke finishes using unpaused real seconds. Drawing follows the active flight camera, including the live inset when the
main view is the map, while emission, aging and capacity behavior stay identical.

Birth positions interpolate previous/current nozzle samples in double ECEF.
Every draw subtracts the current vehicle ECEF in double precision, then maps to
the launch pad's ENU axes. No scene float becomes authoritative flight state.
Exhaust deceleration, a small fixed drift, growth and local ground-plane clamping
are artistic approximations; these are not sampled wind, terrain collision,
combustion dynamics, or a physical smoke simulation. No KSP artwork is bundled.
The procedural shader and billboard presentation are original ESP artwork.

## Adapted source and license

The pure `SmokeEmissionSchedule` helper adapts fractional emission accumulation
and sub-frame staggering from Sarbian's SmokeScreen:

- Repository: <https://github.com/sarbian/SmokeScreen>
- Pinned revision: `245afb49063f48a8beb4289986aaccc0446700ad`, version 2.9.1.0,
  November 9, 2026.
- File: [PersistentKSPShurikenEmitter.cs](https://github.com/sarbian/SmokeScreen/blob/245afb49063f48a8beb4289986aaccc0446700ad/PersistentKSPShurikenEmitter.cs)
- Relevant portions: fractional budget lines 440–455; sub-frame position and age
  staggering lines 332–340, 364–390 and 472–479; trajectory correction 638–644.
- License: BSD-2-Clause. The exact source-file notice is retained in the helper.
  `Assets/StreamingAssets/SmokeScreen-NOTICE.txt` bundles both upstream root and
  source-file notices with the player. Preserve it in distributed builds.

ESP uses explicit mission time, deterministic rate, bounded newest-emission
retention, and ECEF endpoint interpolation. It does not import KSP assemblies,
`KSPParticleEmitter`, `FlightGlobals`, `Krakensbane`, stock models or textures.

## Packaged verification

The macOS VM player passed `-espSrbSmoke -espDaylightReview`: actual SLS flight,
bounded pool, pause, map switching, natural SRB exhaustion, jettison retaining
the trail, 26-second expiration, reset, and Starship exclusion. At the recorded
6.58 s / 48.8 m capture there were 52 active puffs; the 50.98 s / 2391 m capture
had 196, below the 256 cap. Scheduling checks passed during the Unity build.

The development flag runs the real manual SLS climb at 20× with liquid engines
off and pitch commanded to 60 degrees through the existing controls. It slows
to 1× near burnout, then accelerates through the remaining tail. The optional
`-espDaylightReview` label identifies a lighting-only UTC override. No synthetic
flight checkpoint or altered flight state is used. Windows and performance
benchmarking remain unverified.
