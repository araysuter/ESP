# ESP

ESP is an original Earth-scale rocket sandbox built for Hack The Nest with Unity and a pure C# flight core. Explore a WGS84 globe, choose a representative launch site at Starbase or Kennedy LC-39A, then fly an assisted or manual mission. The three vehicle options are the fictional ESP Demo Rocket, a Starship/Super Heavy preset, and an SLS Block 1 preset with parallel solid boosters. The representative vehicles use estimated game parameters.

## Run the mission

Use **Unity 6000.6.9f1** with **URP 17.6.0**. Open the repository's `UnityProject/` in Unity Hub. Keep `FlightCore/` and `UnityProject/` as siblings. The package manifest loads the pure C# core through `file:../../FlightCore`; opening a standalone copy of the Unity folder leaves that dependency unresolved.

For the complete macOS development player, run the build from the repository root with the editor closed for this checkout. Point `UNITY_EDITOR` to the activated editor executable.

```sh
export UNITY_EDITOR="/path/to/Unity.app/Contents/MacOS/Unity"
mkdir -p "$PWD/Builds"
ESP_BUILD_PATH="$PWD/Builds/ESP.app" "$UNITY_EDITOR" \
  -batchmode -quit -projectPath "$PWD/UnityProject" \
  -executeMethod BuildMission.Build -logFile "$PWD/Builds/unity-build.log"
```

Open `Builds/ESP.app` after the build succeeds. `BuildMission.Build` rebuilds the detailed vehicle prefabs from the bundled runtime assets and saves `Assets/ESP/Art/Mission.unity`. The separate Earth and asset preview scenes are useful for inspecting art and coordinate placement. Blender **5.2.3 LTS** is used for asset preparation. The bundled runtime exports are sufficient to build the player. [Mission integration](docs/mission-integration.md) · [Vehicle asset build handoff](docs/vehicle-asset-build.md)

## Controls

| Action | Control |
| --- | --- |
| Explore the globe | Drag to rotate, scroll to zoom, and right-click a visible point for modeled weather and regional imagery |
| Change the map layer | **Surface**, **Satellite**, or **Clouds** for bundled Blue Marble, dated MODIS true color, or daytime cloud fraction |
| Choose a launch site and vehicle | Select a globe marker or enter **Launch View**, then use the vehicle selector on the pad |
| Menus / pause | **Esc** or **Menu**; New Mission begins at the globe, Resume retains the current in-memory session |
| Physics / cheats | Pause → **Live physics panel** or **Cheats**; modified flights cannot receive normal orbit verification |
| Frame rate | Settings → **30 / 60 / 90 / 120 / 144 / Unlimited**, plus VSync; default 60 FPS, VSync off |
| Fly | Select **Launch**. Assistance is enabled by default and uses the same flight controls as manual flight |
| Take manual control | Use the throttle slider or **W/S** or **↑/↓** to change pitch and disable assistance |
| Separate stages | Press **Space** or use the staging button after launch. For SLS, the action jettisons attached, spent solids before advancing the liquid stage |
| Change simulation speed | Select **1×**, **5×**, or **20×**. The HUD displays mission time and the selected rate |
| Start again | Select **Reset** or change launch sites |

Orbit verification requires 60 continuous seconds of unpowered flight above the simulator's conservative 180 km perigee clearance criterion. The map uses the simulated Earth-fixed position. Above 120 km, the flight view uses a separate scaled Earth backdrop. [Mission controls and acceptance checklist](docs/mission-acceptance.md) · [Vehicle parameters and guidance](docs/vehicle-specs.md) · [Physics audit and verification](docs/physics-verification.md)

## Weather, Earth imagery, and credits

- **Offline mode** starts with the bundled NASA Blue Marble globe and uses a deterministic standard atmosphere when a usable launch profile is unavailable. Optional map imagery, point weather and event panels use network services.
- **Launch weather** freezes a NASA GMAO GEOS-FP model column if it is available and at most 12 hours old. Otherwise, it tries an Open-Meteo launch profile at most two hours old, then falls back to the standard atmosphere. The displayed source identifies which profile feeds the flight. The model column is frozen at launch and represents estimates at the site. [Weather provider and historical replay](docs/nasa-weather-provider.md) · [Atmosphere and drag](docs/atmosphere-drag.md)
- **Map data** includes NASA GIBS Blue Marble and optional MODIS layers. The pad textures use NASA GIBS Landsat WELD imagery from the **2000 epoch**, which predates Starbase construction. The flat pad scenery and analytic WGS84 impact surface are approximations. EONET and DONKI provide event context, and Open-Meteo point weather is separate from the flight profile. [Imagery provenance](docs/earth-data.md) · [Point weather](docs/point-weather.md) · [Launch sites](docs/launch-sites.md)
- **Vehicle art** includes the original ESP rocket, Clarence365's *SpaceX Starship Block 3* model under CC BY 4.0, and NASA 3D Resources' *SLS Block 1* geometry. ESP adapted the Starship model's scale, stage grouping, and materials, and the NASA mesh's scale, component grouping, materials, and livery. NASA imagery and geometry credits, adaptation notes, and usage guidance are in the source documentation. [Asset credits and acquisition](docs/model-acquisition.md) · [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/)
- **Open-Meteo** model data are credited under CC BY 4.0. Its hosted API terms apply, including free-use limits. [Data license](https://creativecommons.org/licenses/by/4.0/) · [Hosted API terms](https://open-meteo.com/en/terms)

## Code and verification

| Area | Responsibility |
| --- | --- |
| [`FlightCore/`](FlightCore/) | Dependency-free .NET Standard 2.1 library. Double-precision SI state, point-mass gravity, WGS84 and Earth rotation, fixed-step RK4 powered flight, pressure-dependent thrust and fuel use, staging, atmospheric drag, and two-body orbit diagnostics |
| [`UnityProject/`](UnityProject/) | Mission UI, globe and site rendering, weather and map clients, and a driver that feeds the flight core and renders camera-relative state |
| [`AssetsSource/`](AssetsSource/) | Blender preparation scripts and asset handoff. Source archives and editable Blender files are preserved separately from the checked-in runtime assets |
| [`data/`](data/) and [`scripts/`](scripts/) | Historical NASA GEOS-FP fixture and tools for retrieving and checking model profiles |
| [`docs/`](docs/) | [Flight model and limitations](docs/rocket-flight.md), [coordinate conventions](docs/earth-frames.md), [mission integration](docs/mission-integration.md), and [implementation plan](docs/implementation-plan.md) |

Run the console suites from the repository root with the **.NET 9 SDK**. They cover gravity, circular coast and step convergence, atmosphere and drag, frame round trips and orbit diagnostics, fuel cutoff and staging continuity, impact, assisted ascent, and representative vehicle behavior.

```sh
for project in FlightCore.Tests Atmosphere.Tests Frames.Tests Rocket.Tests Mission.Tests Booster.Tests Vehicle.Tests
do
  dotnet run --project "$project" -c Release || exit 1
done
python3 -m unittest discover -s tests -p 'test_nasa_weather.py'
```

Reproduce the historical data-to-force check using the bundled profile and an explicit UTC clock. This probe evaluates atmospheric forces at sample altitudes. [Rocket flight CI](.github/workflows/rocket-flight.yml) · [Atmosphere CI](.github/workflows/atmosphere.yml)

```sh
dotnet run --project Atmosphere.Probe -c Release -- \
  data/weather/geos-fp-lc39a-20261114T0900Z.json --at 2026-11-14T12:00:00Z
```

## Verification record

Run the packaged smoke test with `Builds/ESP.app/Contents/MacOS/ESP -espFlightSmoke`. It starts an assisted LC-39A mission and checks orbit and site-change reset. Append `-espVehicle 1` for Starship or `-espVehicle 2` for SLS.

- **Headless baseline.** The flight core's .NET 9 CI passed after PR #10 merged. Offline missions at both sites and a historical NASA-weather mission at LC-39A passed at 20 ms and 10 ms steps. [Flight verification details](docs/rocket-flight.md)
- **Packaged flight checkpoint, November 14.** The macOS ARM64 development player completed `-espFlightSmoke` with a live-fetched NASA GEOS-FP analysis valid at 09:00 UTC. It staged at approximately 148 s, verified 60 s of unpowered orbit at 481.14 s and about 227.5 km altitude, displayed the simulated map position, and reset when switching sites. The scaled Earth camera rendered during flight. The Unity build also passed parser and compilation checks. [Mission integration record](docs/mission-integration.md)
- **Representative vehicle packages.** Recorded macOS ARM64 runs verified orbit at 529.36 s for Starship and at 1,109.32 s for SLS, followed by successful map and site-change reset. Both used a frozen Open-Meteo profile because the available NASA analysis was stale. [Vehicle verification details](docs/vehicle-specs.md)
- **Remaining platform verification.** Packaged checks were performed in the Apple Silicon macOS VM. Windows, Intel macOS, and judging hardware remain unverified. A packaged offline rehearsal remains on the acceptance checklist. The flight models use representative parameters and simplified atmosphere, impact, and terrain assumptions. [Mission acceptance checklist](docs/mission-acceptance.md)
