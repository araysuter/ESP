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

