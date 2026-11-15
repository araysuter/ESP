# Sun and Earth map

The main map follows the current simulated vehicle in Earth-fixed coordinates.
Before launch it follows the selected launch site. Drag to orbit the view and
scroll to zoom; **SHIP** returns to the vehicle and **SUN / EARTH** frames the
larger view. The Earth, flight state, picking and orbit prediction remain in
their existing frames. A near-Earth camera is kept on the vehicle's visible
side of the globe when a rotation would otherwise hide it behind Earth.

The Sun is an original unlit sphere with a procedural disc and optical flare in
the celestial sky pass. Its direction comes from `MissionSunlight.SunEcef`, the same live or
simulated clock and Earth-fixed direction that drives sunlight. In map views
the apparent direction and angular radius also account for the camera's
position relative to the Sun. The background pass is occluded by Earth.

The orbit line is a visual approximation: a circular 1 AU path in the mean
ecliptic plane, with a Sun radius of 695,700 km. It is drawn only at wide map
scales. The Earth remains at the map origin; the line and Sun move in the
rotating Earth-fixed display frame. This is Sun/Earth context, not a full
planetary ephemeris or a heliocentric flight simulation. Planetary positions,
Earth's orbital eccentricity and Sun gravity are not modeled here. The existing
double-precision flight state and its Earth-centered orbit prediction are not
changed. The Sun's glow and flare are artistic, not calibrated radiometry.

The map uses one Unity unit per million meters and adjusts camera clipping as
it zooms. The inset keeps its existing Earth-focused framing. Ground picking
at very large zoom distances is limited by floating-point screen-ray precision.

## Reconciliation and validation

The reviewed integration incorporates main `ce27f77` while retaining the
ship-centered map and solar context. The current parallel macOS/Windows build
workflow and packaging fixes are preserved, alongside NASA imagery freshness,
cached Surface/Satellite/Cloud switching, 32-pixel overlap crossfades, 0.22
cloud daytime opacity, night diffusion and local-ground lighting fixes. The
flight core and authoritative state are unchanged.

On November 14, 2026 EST, Unity 6000.6.9f1 compiled and packaged macOS ARM integration source
`e3c4247`. The opt-in `-espShipMapSmoke` passed real ascent, moving ship-center
tracking, Sun/Earth and SHIP toggles, inset framing and reset for all three
vehicles across both sites. Ship and system captures were inspected. The
packaged NASA freshness smoke also passed automatic 4K cloud loading, repeated
clicks, cached mode cycles, explicit refresh, failed-refresh texture/date
retention and offline restoration. All three numerical CI workflows passed.

The contributor reported Windows development-player headless and Direct3D 12
checks on the original source. The reconciled source's Windows-only Actions
[rehearsal](https://github.com/araysuter/ESP/actions/runs/43676041805) is still
building at this update; its release/installer result is not yet established.
A separate macOS headless release probe was interrupted to open the interactive
player for the user's test and is not recorded as a pass. Native mouse interaction has not yet been retested on the reconciled build.
Existing release tags and already-running publication sources were not changed.
