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

## Low-thrust departure and safety

Long burns first raise the parking orbit with a finite prograde burn, coast to
apogee, and search departure opportunities at least 15,000 km from Earth's
center. This avoids aiming a long SLS burn inward across a low parking orbit.
The planner refines the departure against a cloned vehicle simulation with
actual thrust, propellant mass flow and attitude slew. Live flight executes
those same controls; no authoritative position or velocity is assigned by the
planner. Progress and cancellation remain available during the background search.

Free-return candidates reserve at least 500 km of predicted Earth clearance,
target approximately 1,000 km, and must pass an additional atmospheric-clearance
check on the actual return. Prediction takes smaller steps near the Moon's
sphere-of-influence boundary. Lunar capture slows to real time early enough for
the selected vehicle's burn duration.

SLS is included in the default finite-burn regression matrix, alongside the
existing Starship/Super Starship fixtures. The SLS mission fixtures use infinite
fuel: the current modeled ICPS propellant budget is insufficient for the tested
complete missions without it. Ordinary limited-fuel missions must still pass
planning affordability checks; a separate test verifies that insufficient SLS
fuel is rejected without advancing or consuming the live vehicle's state.
Cancellation, mid-burn fuel exhaustion, changed gravity and missed encounters
must cut throttle and restore manual control. These tests do not qualify every
parking orbit, every launch date, or real SLS mission performance.
