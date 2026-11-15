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
These checks have been added but have not run in Unity in this workspace.

Unity compilation, target-player rendering, overdraw/frame timing, and visual
comparison with the reference remain pending. Rehearse each preset: prelaunch
is clear; powered liftoff produces a longer flame and rolling gas/dust; nearby
dust shakes as the pressure fronts arrive; cutoff stops new emissions; cloud
stays at the pad while the rocket climbs; pause/resume and map inset retain
the same clock; restart clears it. Check a daytime and nighttime launch.
