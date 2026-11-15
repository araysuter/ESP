# Map flight instruments

The map uses a red triangle for the current simulated vehicle position.
No enlarged vehicle model is drawn on the globe. The navball shows simulated attitude and surface-relative prograde independently
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

## Projection and qualification

The cyan curve is an instantaneous gravity-only coast prediction: engines cut
now, no atmospheric drag. Every vertex uses the same current Earth rotation
angle. This is an orbital curve, not a future ground track. It updates four times
per second from authoritative inertial position and velocity. Apsides refer to
radial extrema; their tooltips report WGS84 height at that point. Circular orbits
omit ambiguous apsis markers. Open escape curves omit apoapsis. Impact arcs stop
at the first forward WGS84 intersection; radial degeneracies have no orbit line.

November 14 qualification was performed in the project VM:

- Orbit projection, navball and frame checks passed using the Unity-bundled .NET
  8 SDK in a separate test checkout. Test project targets were temporarily changed
  to net8 there; committed CI targets net9. No test outputs enter the Unity package.
- Unity 6000.6.9f1 packaged the macOS ARM player successfully.
- The packaged Starship assisted ascent reached verified orbit at mission time
  529.34 s, altitude 239.03 km, using the standard-atmosphere fallback. The map
  produced a closed 512-point curve with no impact. This is a real simulation run,
  not a loaded checkpoint or edited state.
- Native VM input verified D changed actual heading and selected manual control;
  W changed the navball attitude; M switched to flight and back. The orbit curve
  remained tied to simulated velocity while coasting.
- Window corner resize worked, and the compact dock remained usable at the
  narrower width. Rotating the map changed the apparent orbit ellipse while
  preserving globe occlusion of the far-side curve and markers.
- All three GitHub workflows passed at commit `464d517` including the new orbit
  projection suite. Windows and Intel macOS were not tested.
