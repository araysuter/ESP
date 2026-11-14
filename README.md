# ESP

ESP is an original Earth-scale rocket sandbox built for Hack The Nest with Unity and a pure C# flight core. Explore a WGS84 globe, choose an approximate Starbase or Kennedy LC-39A launch site, and fly the demonstration vehicle with assisted or manual controls. Weather inputs and scenery are explicitly modeled approximations.

The repository contains `FlightCore`, `UnityProject`, `AssetsSource`, and project documentation. Keep `FlightCore` and `UnityProject` as siblings: Unity loads the core via `file:../../FlightCore` in its package manifest. See [mission integration](docs/mission-integration.md) for build instructions, controls, data provenance, verification and limitations, and [the implementation plan](docs/implementation-plan.md) for project scope.

The flight core's .NET 9 CI passed after PR #10 merged. Offline missions at both sites and a historical NASA-weather mission at LC-39A passed at 20 ms and 10 ms steps. The current Unity build passed parser checks and compilation; its packaged flight smoke test passed as detailed below. Other operating-system targets remain unverified.

## Packaged flight checkpoint — November 14

The macOS ARM development player completed `-espFlightSmoke` with a live-fetched NASA GEOS-FP analysis valid at 09:00 UTC. It staged at approximately 148 s, verified 60 s of unpowered orbit at mission time 481.14 s (altitude about 227.5 km), displayed the actual map position, and reset successfully when switching launch sites. The high-altitude scaled Earth camera rendered successfully; it activates above 120 km. This is a rendered package test in the VM, not a Windows or judging-hardware qualification. Weather is a frozen modeled column with a visible timestamp, not instantaneous measurements along the whole flight. Source profile and camera-relative motion are shared with the headless tests.
