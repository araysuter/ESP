# Broadcast flight HUD and live alternate view

Speed and altitude use two white open-arc gauges at bottom left. Speed is
relative to the rotating Earth, displayed in km/h; altitude is WGS84 height
in km. Flight-core state remains SI/double precision. The existing navball
stays centered, with mission time and rate visible. Compact fuel, throttle,
staging, assistance and time-rate controls remain available. Information and
source notices open in a bounded scrolling panel.

The lower-right inset shows the actual alternate view: Earth while flying,
and the current vehicle while the main view is the map. Clicking the inset
or pressing M swaps views. There is no additional orientation model.

A single reusable 384×216 render texture is scheduled at up to 10 Hz. Two
passive preview cameras reuse the actual scene and current simulated state;
main-camera GameObjects stay mutually exclusive for input isolation. Map,
local flight and scaled Earth use separate layers and per-camera sunlight.
The continuous Earth background/URP overlay stack follows the active flight
render camera. Smoke, impact effects and local imagery use that same camera,
so their lifecycle and source data remain continuous while switching views.

No synthetic trajectory, screenshot thumbnail or second simulation is used.
The cap bounds preview work; it is not a measured FPS or memory-performance
claim. Windows remains unqualified until its package is tested.

## Packaged qualification

Run a development player with `-espBroadcastSmoke`. It checks start/new mission,
a real 30-second Starship ascent, paused telemetry unit conversion, fresh rendered
map and flight inset frames, render-target population, repeated view switches,
reset/site/vehicle changes, stable camera/render-texture counts, and 1440×900
and 1280×720 layouts including the scrolling information panel. Screenshots
are written alongside the game's persistent data and require visual review.

This fixture has been authored; final integrated VM results are recorded after
execution rather than inferred from the source.
