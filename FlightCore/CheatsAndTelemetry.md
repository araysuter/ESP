# Flight sandbox options and telemetry

All options belong to `RocketSimulation` and default to the scientific mission:
`GravityMultiplier = 1`, `InfiniteFuel = false`, `CrashDestructionEnabled = true`.
Gravity accepts finite values from 0 through 5 inclusive. Nondefault assignments
set `HasUsedCheats` immediately; restoring the option does not erase the flag.
`Reset()` restores all three defaults, clears the flag and impact state, and
resets the normal mission. Consumers must visibly label modified missions.

Gravity multiplies the normal inverse-square acceleration in both RK4 flight
forces and the liftoff check. It does not change Earth rotation, atmosphere,
geodesy or reference-frame conventions. Orbit diagnostics that assume the
standard Earth gravitational parameter must not certify a modified-gravity run.

Infinite fuel freezes remaining active-stage and attached-SRB propellant,
including on the pad. Thrust still requires positive remaining fuel and the
normal engine/solid ignition state. It does not refill depleted tanks, remove
mass, or allow burning SRBs to be jettisoned. Turning it off resumes normal
consumption and event-split cutoff. Normal staging still discards the current
stage and activates the next stage's original remaining supply.

## Impact model

The baseline is a point mass with a WGS84 pad-altitude collision surface, not
individual parts, joints, thermal failure or detailed structural damage.
Collision always occurs and ends the flight. At impact the existing linear
within-step position/time interpolation is retained; velocity is interpolated
over that same interval to measure speed relative to the rotating surface.

With damage enabled, speed greater than
`RocketSimulation.DestructiveImpactSpeedMetersPerSecond` (10 m/s) sets
`IsDestroyed` on both simulation and snapshot, with status `Destroyed: ground
impact`. The threshold is a gameplay approximation, not a safe landing limit.
With damage disabled, the same collision produces an intact `Landed` state.
Both outcomes retain `IsLanded = true` and `IsComplete = true`; neither passes
through the Earth. `ImpactSpeedMetersPerSecond` retains the pre-stop speed.

## Physics telemetry

`RocketSimulation.Physics` returns immutable `RocketPhysicsTelemetry` evaluated
at the current state using the exact force path shared with RK4. Units are SI;
gravity, drag and acceleration are magnitudes. Thrust includes burning SRBs and
the active liquid stage. Airspeed is relative to rotating air plus the actual
frozen-profile wind, not ground speed. Temperature, pressure and density come
from that same atmosphere sample. Dynamic pressure matches the snapshot.

**NetAccelerationMetersPerSecondSquared is free-flight acceleration**: the
vector sum of gravity, thrust/mass and drag/mass. Pad hold-down and landed
ground support are excluded. Label it “Free-flight acceleration” when showing
constrained states; a stationary rocket can report nonzero gravity/acceleration.
`TotalPropellantKg` includes remaining active-stage fuel, attached SRB fuel and
all future stages. It excludes fuel discarded by stage separation. Combine it
with snapshot `MassKg` for remaining-fuel displays; active-stage-only fuel is
still available as `State.PropellantKg`.

## Verification

The standalone deterministic harness is `Cheats.Tests/Cheats.Tests.csproj`:

```sh
dotnet run --project Cheats.Tests/Cheats.Tests.csproj -c Release
```

Run it with the existing Rocket, Booster, Mission and Vehicle suites on the
qualified VM. This change was not built or tested on the contributor's host.
