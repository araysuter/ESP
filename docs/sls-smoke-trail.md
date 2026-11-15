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
remaining smoke finishes using unpaused real seconds. Map view hides the renderer
while maintaining the same emission, aging and capacity behavior.

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

## Verification status

Not compiled, rendered, or tested during authoring. The owning VM coordinator
must run `ESP/Verify smoke emission scheduling` (or its static `Run` entry point),
then verify an actual SLS launch, time rates 1/5/20, map toggling, menu pause,
booster burnout/jettison, mission reset and Starship's lack of SRB smoke. Inspect
`ActiveParticleCount <= 256`, fading after cutoff and camera-relative anchoring.
No presentation-only sample should be labeled a verified flight checkpoint.

The development-player flag `-espSrbSmoke` bootstraps
`RocketSmokeTrailSmoke` without scene wiring. It has a 110-second real-time
timeout and performs a real manual SLS climb at 20×, liquid engines commanded
off and pitch commanded to 60 degrees through the existing finite-rate controls.
It observes the final seconds before burnout at 1× to retain a useful inspection
window even with slow rendering, then restores 20×. It checks prelaunch, the 256-puff cap, menu freeze (including `VisualTimeSeconds`),
map aging, natural SRB fuel exhaustion, jettison persistence, expiration after
26 mission seconds, reset, and a Starship launch producing no SRB smoke.
Screenshots are `srb-smoke-pad.png` and `srb-smoke-climb.png` in persistent data.
This rehearsal is implemented but has not been executed during authoring.

Add `-espDaylightReview` only for labeled daylight screenshots. This sets
`MissionSunlight.ReviewUtc` to 18:00 UTC on the current UTC date and displays an
on-screen DEVELOPMENT / DAYLIGHT UTC OVERRIDE label. The flight remains the
real simulated flight; this option does not claim the lighting matches the
current real-world time. The previous review override is restored on teardown.
