# Map and launch presentation checkpoint

The `Mission` scene adds the simplified map/launch flow without changing the partner's flight code. Use `BuildMission.Build` with `ESP_BUILD_PATH` to regenerate and package it with the pinned Unity editor. The scene starts with the bundled Blue Marble globe. Starbase and Kennedy LC-39A markers open representative scenery; the top-right button returns to the map. Drag to rotate, scroll to zoom, and right-click a point for modeled weather and regional NASA imagery.

Surface, Satellite, and Clouds select the offline composite, MODIS true color, and MODIS daytime cloud fraction respectively. Requested imagery dates remain separate from weather validity times. A layer fetch failure retains the previous displayed image. NASA EONET and DONKI catalog status is informational and does not imply an operational all-clear.

## Flight integration ownership

No simulation driver is assigned in this checkpoint. Launch/stage and throttle controls are disabled, and the UI says flight simulation is unavailable. The flight team's gravity/orbit work remains on `flight-core` and its contributor branches.

Implement `ESP.Presentation.IMissionFlightDriver` in a thin Unity adapter and assign it to `MissionDirector.FlightDriver`. The interface provides launch/reset/stage/throttle commands and telemetry. `MissionDirector.RocketVisual` exposes the current visual vehicle; site changes destroy that instance and reset the driver. Returning from map to the same site preserves the instance. The driver must bind the current instance and apply the existing camera-relative/frame conventions; do not integrate physics in float scene coordinates. The current local launch camera is a stationary preview camera and still needs flight-follow behavior.

`Launch(site, weather)` receives an immutable profile or null. Null explicitly means the driver's documented deterministic atmosphere fallback, never zero density/wind by accident. Profile age is checked again at launch. A snapshot retained for a mission must not silently change when the network component refreshes. Wind is east/north in meters per second; convert it to the same rotating-atmosphere frame used for air-relative drag. Weather-driven forces, fallback continuity, staging, orbital success, and trajectory convergence still require integration and verification.

## Sources and approximations

Regional textures use NASA GIBS Landsat WELD annual imagery for the **2000 epoch**, not today's launch facilities. Starbase construction postdates the imagery. Exact requests and bounds are in `UnityProject/Assets/ESP/Art/Sites/provenance.json`. The 2 km visual ground uses a latitude-adjusted center crop, local east/north axes, and provisional zero elevation. It is flat visual scenery, not terrain elevation or collision. Site coordinates are approximate; see `launch-sites.md`.

NASA GIBS imagery is credited to NASA EOSDIS/ESDIS. [GIBS use guidance](https://nasa-gibs.github.io/gibs-api-docs/) requests acknowledgment; [NASA media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/) applies. No NASA endorsement is implied. No Google imagery is included.

Weather data are from [Open-Meteo](https://open-meteo.com/), under [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). The data are transformed into SI units, interpolated vertically, and presented as modeled estimates. The [free API terms](https://open-meteo.com/en/terms) cover noncommercial use and rate limits. Weather is not supplied by NASA and is not a measurement of exact conditions at the pad.

## Verification

`VerifyLaunchWeatherProfile.Run` exercises synthetic surface conversion, meteorological wind direction, missing pressure levels, bounded interpolation, invalid surface data, and stale responses. It passed in Unity 6000.6.9f1 on the macOS VM. `-espMissionCapture` captures map, each launch site, and selected-point weather from the packaged application; it exercises presentation only and is not a recorded flight.

The macOS ARM64 package builds and renders successfully, including both site textures and the selected-point weather disclosure. The initial black ground material was corrected using a serialized unlit imagery material. Live point weather and regional imagery returned in the packaged application. Windows/Intel builds, physical terrain, simulated launch, and weather-driven trajectory checks are not yet verified. See `mission-acceptance.md` for outstanding integration checks.
