# Lunar autoguidance

The **Guidance** button opens the existing Starship deorbit option and two lunar
options. To plan a lunar maneuver, reach an unpowered, bound Earth orbit with
at least 120 km of clearance, cut the throttle and select **Moon free return**
or **Moon orbit insertion**. Normal gravity is required. The search pauses
simulation time and reports progress; it may take a while or report that no
safe, affordable trajectory was found. Infinite fuel is supported, and the
fictional Super Starship provides more usable propellant at the same mass.

The planner searches parking-orbit departure times, flight times and lunar
aim points using the moving Moon and the flight core's patched-conic gravity.
The guidance uses the selected vehicle's ordinary attitude, engine thrust and
propellant; it does not teleport the vessel or apply an instantaneous impulse.
It executes the departure burn, performs a midcourse correction and, for lunar
orbit insertion, burns near closest approach and checks for a bound orbit above
the lunar surface. Free return ends after the Moon flyby on the approach to
Earth; it does not perform an Earth capture or reentry burn. Use the existing
deorbit guidance separately when eligible.

The search is bounded and approximate, so it cannot guarantee a solution for
every starting orbit or vehicle. An unsafe or unaffordable predicted transfer
is rejected; loss of thrust, propellant, the lunar encounter or the expected
trajectory stops guidance and returns control to the player. The cancel button
also returns control. Time warp is available during unpowered coasts and is
reduced to 1× near burns and corrections. The displayed actual rate can be
lower than the requested rate if the simulation cannot keep up.

Regression fixtures exercise both maneuvers from three Earth orbits, including
inclined orbits and infinite fuel, with the normal 20 ms step during burns and
accelerated coast steps. They check for an actual bound lunar orbit or a lunar
flyby returning to an Earth periapsis above the modeled atmosphere. They are
not a substitute for a full interactive playthrough on each target platform.

## Handoff: SLS finite-burn targeting

The current predictor treats departure and correction maneuvers as impulses.
SLS's low-thrust upper stage has a much longer finite departure burn, and its
infinite-fuel low Earth orbit fixture currently aborts with "Course correction
could not be calculated" rather than reaching lunar orbit. Free return has not
been verified for SLS. Do not mark SLS lunar guidance complete or merge this
draft without resolving the mismatch and verifying both missions.

To reproduce the SLS fixture, set `ESP_TEST_SLS_LUNAR=1` and run the
`FlightCore.Tests` project. Improve the departure and correction predictor to
account for finite thrust, slew and propellant (or select/reject candidates
using a finite-burn simulation), then rerun the 20 ms burn fixtures for both
SLS and Starship variants. Verify sufficient-fuel and exhaustion paths, the
existing Starship deorbit autopilot, time warp, cancellation, and an interactive
Unity player on each supported platform. Review this branch against current
`main`, preserving any later work when integrating.
