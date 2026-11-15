# Solar display optics

The existing celestial-background pass draws the Sun in flight; no near-camera
sphere or independent sky planet is created. Its radius is `SunRadius / distance`
(695,700 km / 149,597,870.7 km in flight), approximately 0.533 degrees diameter.
Map view uses the camera-to-Sun distance, so its angular size changes naturally.
[NASA's thermal-environment reference](https://ntrs.nasa.gov/api/citations/20220013453/downloads/TFAWS-2022-Short%20Course-Environments_Vector_Shading_MERGED.pptx.pdf)
states approximately 0.53 degrees. This is a fixed representative Earth-distance
size in flight, not a seasonal apparent-size ephemeris.

The nearly white disc has a derivative-antialiased edge, mild limb darkening and
linear HDR display values of 6–12. A compact bloom-like halo and faint horizontal
camera streak make its brightness legible without enlarging the physical disc.
These are original procedural camera appearance approximations, not calibrated
solar radiometry, lux, or an optical instrument simulation. The map sphere uses
the same near-white HDR appearance at the real display radius. Existing material
and shader references remain valid; there are no new binary or scene assets.

The background draw precedes the opaque Earth and Moon, transparent cloud shell,
and atmosphere, so those layers naturally cover or attenuate the disc and local
optical glow. The cloud fraction layer remains a display approximation rather
than measured cloud optical depth. No remote ghost images are drawn across
unrelated pixels. Additionally, Earth-source occultation disables the camera
glow when the source center is below the WGS84 horizon, using the reconstructed
camera ECEF position in double precision. The disc itself is clipped per pixel by
the rendered globe. This binary center gate does not model fractional solar
occultation, atmospheric refraction or eclipse diffraction. Day/night star
suppression is applied only to the sampled star map before adding solar radiance.
A display exposure approximation fades stars with local solar elevation from
-12 to 0 degrees and camera altitude with a 20 km falloff; horizon rays receive
stronger suppression than zenith rays. Night and orbital stars remain readable.
This exposure term is not physical atmospheric extinction. The atmosphere pass
retains its scalar transmission for the combined background, including the Sun;
it must not replace that transmission with sky-brightness opacity. The sky map
asset itself is unchanged.

`StarField.SolarAngularDiameterDegrees`, `SolarFlareVisibility`, and
`SolarDiscPeakDisplayRadiance`, and `StellarZenithVisibility` expose display diagnostics for scene QA. Validate
in the packaged renderer: Sun-facing day pad, cloudy pad, dark hemisphere, limb
rise/set and orbit; inspect HDR tonemapping/bloom together. The macOS development player compiled and its screenshots were inspected; see [packaged validation](visual-polish-validation.md).
