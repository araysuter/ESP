# Clear-air scattering presentation

The continuous WGS84 atmosphere uses RGB Rayleigh single scattering with coefficients 5.8, 13.5 and 33.1 per million meters and an 8 km density scale height. A Henyey-Greenstein aerosol term (g=0.76, 1.2 km scale height) supplies forward haze. Sunlight is integrated through six quadratically spaced intervals with Earth shadowing. Eight nonuniform view intervals concentrate toward the ray closest approach to the ellipsoid, including orbital limb rays. Each interval has its own midpoint and length.

This is a bounded real-time visual approximation: RGB wavelengths, an exponential clear-air density model, no multiple scattering or refraction, approximate ellipsoid altitude, and scalar background extinction. Live cloud imagery remains a separate layer; the physics atmosphere and flight state are unchanged. Display exposure and ACES are artistic mappings rather than calibrated radiometry.

References: [Bruneton atmospheric scattering](https://ebruneton.github.io/precomputed_atmospheric_scattering/) and [NASA Planetary Spectrum Generator atmosphere reference](https://psg.gsfc.nasa.gov/helpatm.php). The shader is original and does not copy either implementation.

Packaged validation uses the opt-in development argument `-espVisualPolishSmoke` (plus `-espVisualPolishQuit` for bounded exit). Captures explicitly identify cosmetic UTC overrides. Review pad day/night, low Sun, orbital limb, source occultation and frame intervals. Finite sampling can still introduce errors at grazing angles; no claim of spectral accuracy or unrestricted performance is made.
