# Starship powered approach

The user-provided reference sequence calls for belly descent, engine ignition,
a finite flip, braking before the tower and a physical catch. It is a visual
reference, not measured trajectory or propulsion data.

Previously the return planner minimized distance from the tower **at flip
ignition**. Residual horizontal motion then carried the ship past the tower;
the landing controller's lateral-error height hold let it hover and return.

The planner now scores the estimated end of horizontal braking, including the
finite flip and available lateral deceleration. Coarse search and refinement use
the same projected point, moving ignition upstream. This is only a planning
metric; it never assigns the vehicle's position or velocity. Final guidance uses
a stopping-speed envelope and a slightly overdamped terminal velocity response,
with continuous descent rather than a range-dependent hover height.

The 45 degree/second flip, 30-degree thrust cone, real propellant use and catch
criteria are unchanged. Catch still requires closed arms, lug distance below
0.35 m, lug height error below 0.5 m, lug speed below 3 m/s and body tilt below
5 degrees. Insufficient fuel can still cause a failed catch.

## Measured nominal comparison

Studio headless test, `dotnet run --project Return.Tests -c Release -- --require-catch`:

| Metric | Previous main | New guidance |
| --- | ---: | ---: |
| Ignition location along approach | 18.38 m past tower | 122.12 m before tower |
| Horizontal speed at ignition | 23.82 m/s | 23.82 m/s |
| Maximum COM overshoot along approach | 129.34 m | 0.54 m |
| Powered approach duration | 61.24 s | 17.06 s |
| Remaining fuel at catch | 68,806 kg | 101,027 kg |
| Outcome | Physical catch after overshoot | Physical catch |

Return.Tests now records these approach metrics and requires ignition upstream
with at least one second of horizontal-speed lead (minimum 5 m) and no more than
5 m of along-track overshoot in successful catch fixtures. Existing finite
attitude-rate, fuel-conservation, terminal-state and catch-envelope checks remain.
The signed range is measured in Earth-fixed coordinates along the horizontal
inbound direction at ignition; it is not a total three-dimensional lug error.

These are numerical results, not rendered visual approval. A new player build
still needs a Studio playtest to inspect the flame/flip timing and tower-arm view.

All seven Studio return scenarios passed: nominal catch, half-step catch, 10/5 m/s
and 15/-5 m/s east/north crosswinds, return requests after 45 s and 90 s coast,
and full propellant loss at flip. All six success scenarios caught in 15.44–17.96 s
with maximum along-track overshoot below 0.8 m. The depletion case hit the ground
at 115.11 m/s and did not falsely catch. No visual-player test is claimed here.
