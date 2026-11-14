# Map flight instruments

The map uses a small, display-only copy of the sourced vehicle. Its position is
georeferenced, but its size and camera-facing orientation are presentation aids.
The navball shows simulated attitude and surface-relative prograde independently
of the map camera. The same compact flight dock is available in both views after
launch; imagery selectors remain separate.

W/S commands elevation (W increases it); A/D commands heading. Arrow keys also
steer unless a keyboard-active UI control owns focus. Steering disengages assisted
ascent and the flight core retains its finite attitude response. These are local
horizon elevation/heading commands, not a full rigid-body pitch/yaw/roll model.
At vertical elevation a heading change alone cannot tip the vehicle. M switches
map/flight. Space stages in flight view; the on-screen staging control also works
in map view. Focused buttons handle their own Space activation to avoid duplicate
staging. Text entry and app focus loss suppress keyboard flight commands.

## Design references

- [kOS rotational controls](https://ksp-kos.github.io/KOS/commands/flight/raw.html)
  distinguish vessel-relative rotational axes from our simpler horizon commands.
- [JPL osculating elements](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/oscelt_c.html)
  specify inertial state inputs and document degenerate orbit cases.
- [NASA orbital mechanics](https://science.nasa.gov/learn/basics-of-space-flight/chapter5-1/)
  explains the orbital plane and Earth's rotation beneath it.
- [Orbiter's MIT-licensed Orbit MFD](https://github.com/orbitersim/orbiter/blob/a7a3396e6c3104a05fd5ac55736de75e4c0e921f/Src/Orbiter/MfdOrbit.cpp)
  was inspected for display conventions; no implementation or art was copied.

The user's KSP screenshots guide information hierarchy and small markers. ESP
uses its own UI and rendering assets.
