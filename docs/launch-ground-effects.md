# Launch exhaust, gases and shock-stirred dust

`RocketPlume` extends the existing exhaust envelope by 2.3× at sea level,
tapering to 1.2× the previous length in vacuum. The final length feeds the
existing pressure spread and bounds calculation. All vehicle presets share
this response; engine output still controls cutoff and throttle.

`LaunchGroundEffects` adds a separate pool of 384 depth-sorted billboards using
the original lit SRB smoke shader. White gases roll outward and upward; tan dust
stays low. The first-stage engine must be producing actual thrust within 220 m
altitude and 300 m of the pad, pointed more than 45 degrees above horizontal.
Ground emission fades with height. It is available for all three presets and
does not change SLS trail emission or its counters.

The cloud stays at the launch pad: its anchor is double-precision WGS84 ECEF,
subtracted from the current vehicle position before rendering in pad ENU axes.
Small local puff offsets are cosmetic. Deterministic outward pressure pulses
travel at an artistic 85 m/s, recur every 1.15 simulated seconds while the pad
is powered, and shake/lift dust with a decaying oscillatory wake. Launched
pulses finish after engine cutoff; they do not apply forces to the rocket.
These effects approximate steam/exhaust gas, dust and acoustic disturbance;
they are not combustion, fluid, acoustic or terrain erosion simulations.

Emission is capped at 32 puffs per rendered frame, without a queued backlog.
Dust expires after 18 simulated seconds and gas after 32, or earlier when the
bounded pool is reused. Menu pause freezes emission and movement. Time warp
uses true mission time. After impact, existing puffs finish on unpaused real
time. Mission reset, replacement and time rewind clear the pool. The active
flight camera draws the effect, including the flight inset in map view.

`BuildMission` serializes the component and shader reference into regenerated
Mission scenes. Rebuild the scene/player to include it. No external art was
added; the reference image informed the direction only.

## Verification

The scene builder runs `LaunchGroundEffectsChecks` for pressure-front travel
time, outward push, post-cutoff propagation, wake decay and displacement bounds.
These checks passed in Unity 6000.6.9f1 on macOS ARM, followed by a successful
packaged development build.

The packaged `-espLaunchCloudSmoke` rehearsal passed on November 15, 2026:
actual offline Starship flight at 1×, bounded ground-cloud emission (47 puffs at
2.04 s, 117 at 5.04 s, 170 at 10.02 s), menu pause and restart clearing the pool.
Inspected daylight captures show white billowing gas and tan dust at the pad,
with the longer blue-white exhaust visible above the cloud. The opt-in rehearsal
logs a daylight UTC override and wider camera framing; neither changes flight
state. Its snapshots are visual checks, not a measured acoustic simulation or
proof of reference-image fidelity.

This test used the shared integration project with an existing, unmerged
re-entry source overlay preserved. It validates the launch effects in that
combined macOS build, not the re-entry mission. Windows, night launch visuals,
all-preset acceptance and frame-time/overdraw measurements remain unverified.
