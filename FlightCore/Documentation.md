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

`StarshipReturnController` uses the existing double-precision force/fuel integrator. `TryStart` accepts the supported Starship upper stage in Earth orbit with minimum WGS84 altitude >=120 km and remaining propellant. It preserves position, velocity, mission time and fuel. Non-Earth return requests are rejected; guidance shuts down if the primary changes away from Earth.

Planning holds mission time while one cancellable worker owns private prediction clones. It searches 600 finite-burn candidates, 125 initial refinements, four 25-candidate refinements and bounded Newton targeting, then validates one complete return. Prediction steps are 4 s in coast, 0.2 s in early burn/entry searches and 0.02 s in final refinement. The live player always retains its 0.02 s integration step. Candidate count and elapsed wall time are displayed in both camera and map views. The search never replaces the authoritative state. A predicted catch is not a guarantee.

The body/thrust axis normally slews at an estimated 10 degrees/s. Atmospheric command stays broadside to relative airflow, with bank limited to 60 degrees at 5 degrees/s. Projected area varies between 63.62 and 450 m² with estimated Cd=1.2 and L/D=0.25. The powered flip starts from measured downward velocity, body tilt, stopping distance and explicit margins; it uses an estimated 45 degrees/s finite slew and 8% aggregate thrust while turning. Normal landing guidance takes over as thrust aligns. Both the displayed attitude and force direction use the same actual axis. The player switches to 1× during flip and final approach.

Landing targets the actual lug 21 m along the body axis, compensates modeled aerodynamic and rotating-frame forces, and uses ordinary limited thrust and fuel. It does not assign acceleration, descent speed or trajectory position. The authored tower uses a 100 m COM reference and 121 m lug target. Its arm carriage is raised 50 m from the imported pose and waits open before launch. The core permits arm closure only after the initial 10 s readiness interval, within 50 m horizontally, COM altitude 97–200 m and lug-relative speed below 30 m/s. Closure advances at 0.18/s. Capture requires >=99% closure, lug horizontal miss <0.35 m, altitude error <0.5 m, lug speed including body-axis motion <3 m/s and tilt <5 degrees. The approximate constraint then sets velocity to corotation without snapping position. Failed approaches continue to physical impact.

This is an experimental point-mass model, not six-degree-of-freedom aerodynamics, a flap/moment model, collision-contact mechanics, structural loads or TPS damage. Aero coefficients, attitude authority, continuous throttle, instant ignition/restarts and use of aggregate upper-stage engines are gameplay approximations. Engine-count sequencing is not modeled. The accepted-state Sutton–Graves heating proxy is `1.74153e-4 sqrt(rho / 4.5) v³` W/m²; force evaluation is pure. [NASA TR R-376](https://ntrs.nasa.gov/citations/19720003329).

### Studio verification

The revised high-catch/powered-flip controller passed these celestial-enabled pure C# cases on the project macOS ARM VM with Unity's bundled .NET SDK 8.0.318 and bundled Moon/Sun reference data:

| Case | Result | Remaining propellant |
| --- | --- | --- |
| 0.02 s step, 60 s orbit coast | Caught | 68,805.9 kg |
| 0.01 s step | Caught | 74,069.9 kg |
| 45 s orbit coast | Caught | 68,818.8 kg |
| 90 s orbit coast | Caught | 56,797.2 kg |
| Synthetic east/north wind 10/5 m/s | Caught | 68,449.1 kg |
| Synthetic east/north wind 15/-5 m/s | Caught | 67,599.1 kg |
| Propellant removed at landing flip | Ground impact, no catch | 0 kg |

All successful cases satisfy the same lug/arm envelope. Synthetic wind tapers to zero at 20 km and is not NASA weather. The negative case impacts at 115.35 m/s with arms open. The nominal sequence asserts actual thrust while tilted >45 degrees and finite body motion <=45 degrees/s; it begins the flip at 707.84 m and reaches <8 degrees tilt after 1.50 s. These are measured gameplay outcomes, not real Starship flight specifications. The two independently guided timestep cases differ in retained fuel by about 7.7%; discrete targeting/capture events are not claimed to converge identically.

Before the new flip tuning, an exact numeric A/B signature compared the planner optimization against its previous implementation: burn time/delta-v, predicted miss, terminal time, position, velocity, attitude, fuel, heat and capture metrics matched exactly. Full headless execution including build decreased from 32.14 s to 23.43 s. Prediction-only telemetry/string work and duplicate pure force evaluations were removed; search order, integration and physical limits were unchanged by that optimization.

The existing core suite previously passed gravity, failed-step state preservation, orbit drift/convergence, solar lighting, JPL interpolation, actual Moon/Sun impacts, SOI velocity continuity and primary-relative thrust. `ReturnClone` preserves launch UTC/reference ephemeris. The near-Earth collision shortcut uses both endpoints inside a conservative 200 Mm Earth-centered ball; full checks apply outside it. This assumes the physical ephemeris domain, not arbitrary fabricated celestial vectors.

The macOS ARM QA5 development player completed a real pad-to-orbit-to-return-to-catch and reset rehearsal with Open-Meteo weather frozen at launch (2026-11-15T06:00Z). Planning took 63.87 s for 853 candidates while displaying progress. The powered flip began at 706.31 m and upright landing guidance followed at 568.54 m. Capture retained 70,048.03 kg propellant with 0.34991 m horizontal lug miss, -0.00363 m height error and 0.05008 m/s relative speed. Onboard/chase views, entry glow, powered flip and reset passed. A subsequent overhead user inspection found inward-opening visual arms; opening direction is now derived from imported geometry, with a wider 35-degree waiting pose and a dedicated development pose review. These package results do not qualify Windows or a signed public release.
