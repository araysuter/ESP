# FlightCore Gravity Library Documentation

## Project Structure

```
FlightCore/
├── Gravity.cs              # Main implementation
├── Vector3d.cs             # 3D vector struct
├── README.md               # Usage documentation
└── Documentation.md        # This file
```

## Core Components

### Gravity Class

The `Gravity` class is the main entry point for gravitational calculations.

**Static Method:**
- `Acceleration(Vector3d position)` - Calculates gravitational acceleration at given position

**Constants:**
- `EarthGravitationalParameter` - Standard gravitational parameter for Earth (3.986004418 × 10¹⁴ m³/s²)

### Vector3d Struct

An immutable double-precision 3D vector struct with:
- X, Y, Z components
- Magnitude() method
- Normalize(), addition, subtraction, and scalar multiplication
- Constructor validation rejecting NaN and infinity components

## Mathematical Implementation

The library implements Newton's law of universal gravitation:

**a** = -μ × **r** / |**r**|³

Where:
- **a** is the acceleration vector (m/s²)
- μ is Earth's gravitational parameter (3.986004418 × 10¹⁴ m³/s²)  
- **r** is the position vector from Earth's center (m)
- |**r**| is the magnitude of the position vector

## Validation and Testing

The implementation includes focused console tests for:

1. **Surface Position Test**: 
   - Input: (6378137, 0, 0) meters
   - Expected Output: (-9.79828548, 0, 0) m/s²

2. **Off-Axis Test**:
   - Verifies that acceleration vectors point directly toward Earth center
   - Confirms antiparallel relationship between position and acceleration vectors

3. **Distance-Doubling Rule**:
   - Validates inverse square law behavior
   - When distance doubles, acceleration decreases by factor of 4

4. **Invalid Input Handling**:
   - Zero vector inputs are rejected with ArgumentException
   - Vector construction rejects NaN and both infinities in each component

5. **Additional Known Values and Symmetry**:
   - The 100 km example and all six signed coordinate axes are checked
   - Numerical assertions explicitly reject nonfinite results

## Compatibility

- Target Framework: .NET Standard 2.1
- Uses only standard library types
- No external dependencies
- Unity integration and packaged execution require separate verification; URP is independent of the flight library

## Performance Considerations

- All calculations use double precision for accuracy
- Minimal memory allocation during computation
- No performance measurements are established by these tests
- Vector operations return value types; compiler inlining is not guaranteed
## Experimental Starship return

`StarshipReturnController(targetLatitudeRadians, targetLongitudeRadians, catchAltitudeMeters)` works through the same `RocketSimulation` force/fuel integrator. Call `TryStart(sim, out reason)` only for the Starship preset's upper stage in a bound orbit with minimum WGS84 altitude >=120 km. `StageDefinition.SupportsStarshipReturn` explicitly opts into this supported aerodynamic/engine approximation; other stages and empty tanks are rejected by the core API.

During `Planning`, call `Update` in bounded slices without calling `simulation.Step()` and visibly label the planning pause. Each update performs at most 48 prediction steps: 4 s during vacuum coast, 0.2 s for initial burn/entry searches, and 0.02 s for final refinement. Search evaluates 600 finite-burn candidates over one osculating elliptic orbital period, varying coast time, retrograde impulse and normal-plane impulse. A 125-candidate refinement, four 25-candidate refinements and bounded finite-difference Newton targeting minimize the footprint at the start of landing flip. One complete predicted return then qualifies `PredictedCatchFeasible`. `PredictedMissMeters` is the approach footprint miss, rather than post-impact miss. After planning, call `Update` then `Step` for each normal fixed step. Planning clones never replace the authoritative state. Predictions are estimates, and capture can fail.

The return-only inertial thrust/body axis slews at an estimated 10 degrees/second. The atmospheric body command stays broadside to relative airflow and banks toward the rotating target; bank is limited to 60 degrees and slewed at 5 degrees/second. Projected body area varies between 63.62 and 450 m² with estimated Cd=1.2 and L/D=0.25. Lift uses the commanded bank angle while body orientation follows its finite slew: this is a point-mass approximation, not a rigid-body moment/flap model. Rarefied flow, structural failure and TPS temperature/damage are omitted. Heating telemetry is the accepted-state Sutton-Graves Earth-entry proxy `1.74153e-4 sqrt(rho / 4.5) v³` W/m², with a 4.5 m effective nose radius. Force evaluations are pure, and discarded RK samples never update heating peaks.

Landing uses actual thrust, mass and propellant, an energy-based descent schedule, limited lateral steering, and rotating-frame centripetal/Coriolis feedforward. Engine pressure interpolation and fuel consumption are shared with ascent. Continuous throttle down to zero, all upper-stage engines, instant ignition, and no engine restart limits are disclosed approximations. A burn that runs out of propellant without obtaining an atmospheric intercept fails explicitly while the vehicle physically continues coasting.

Activation preserves the existing authoritative position anchor, treated as a point-mass center of mass for return. The presentation places the capture lug 21 m along the actual body axis. For the authored Starbase tower, pass a 50 m anchor target so the vertical lug target is 71 m. Arms close at 0.18/s only within 50 m horizontally, anchor altitude 47–150 m (a 3 m lower approach margin) and ground-relative speed <30 m/s. Capture requires arms >=99% closed, actual lug horizontal miss <0.35 m, lug altitude error <0.5 m, lug ground-relative speed including body-axis motion <3 m/s, and body tilt <5 degrees. The tower constraint then sets the current point's velocity to corotation; position is never snapped. This is an explicitly approximate mechanical constraint, not a full contact solver. A miss continues through ordinary WGS84 impact/destruction; a surface landing elsewhere is not a catch.

`Return.Tests --require-catch` checks the measured pad-to-orbit mission, 60 s coast, return continuity, finite state, propellant monotonicity, unsupported vehicle/empty tank rejection, and physical lug/arm capture gates. `--half-step` uses a 0.01 s authoritative step. `--force-depletion` injects complete propellant loss at landing flip and requires normal ground termination without a catch. The test project targets .NET 8 with major runtime roll-forward allowed, matching the qualified bundled SDK. Diagnostic mode without `--require-catch` prints terminal failures without treating them as a qualified recovery. The empty-tank rejection fixture changes only a private test tank value and restores measured fuel before the actual mission; it is not a demo checkpoint.

Heating references: [Sutton and Graves, NASA TR R-376 (1971)](https://ntrs.nasa.gov/citations/19720003329), [NASA engineering correlation discussion](https://ntrs.nasa.gov/api/citations/20060004824/downloads/20060004824.pdf). No third-party mod implementation was copied.

### Qualified headless return checks

On the project macOS ARM VM using Unity's bundled .NET SDK 8.0.318, the measured offline Starbase pad-to-orbit mission plus 60 s unpowered coast passed `Return.Tests --require-catch` at authoritative steps of 0.02 s and 0.01 s. Both runs used the numerical planner and ordinary force integration, with no loaded/assigned flight state. At orbit-request time the 0.02 s fixture had 135,821.2 kg propellant, altitude 240,109.1 m and minimum predicted WGS84 orbital altitude 200,064.7 m.

| Step | Capture fuel | Actual lug horizontal miss | Lug altitude error | Lug ground-relative speed | Arms |
| --- | --- | --- | --- | --- | --- |
| 0.02 s | 65,116.3 kg | 0.3492 m | -0.1863 m | 0.0562 m/s | 100% |
| 0.01 s | 68,259.4 kg | 0.0476 m | -0.4997 m | 0.0773 m/s | 100% |

The independently planned approach-footprint estimates were 0.7 m and 26.1 m. Peak accepted-state heating proxies were 521,457 and 521,517 W/m². These are independently guided missions at two step sizes, not a claim that their discrete guidance/capture events occur at identical times. Their capture-fuel difference is approximately 4.8%.

`--force-depletion` removed remaining propellant at landing flip as an explicitly injected failure fixture, then continued the same physics. It produced a 147.95 m/s ground impact 675.6 m from Starbase, with arms at 0% and no catch. Unsupported-stage and empty-orbit-tank request rejection also passed. The existing `FlightCore.Tests` suite passed on the final source: gravity, failed-step state preservation, orbit energy/angular-momentum drift and coarse-to-fine position error ratio 3.9999, solar lighting, and celestial frame checks.

These checks qualify the pure C# offline point-mass route, not the rendered vehicle/tower alignment, live weather route, packaged application, Windows target, or a rigid-body contact model. Those integration/target checks are separate evidence.
