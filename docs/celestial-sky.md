# NASA celestial background

The offline sky combines the celestial-coordinate **Milky Way** and **Hipparcos/Tycho bright-star** layers from [NASA SVS Deep Star Maps 2020](https://svs.gsfc.nasa.gov/4851/), visualization by Ernie Wright. Credit: NASA/Goddard Space Flight Center Scientific Visualization Studio; Gaia DR2, ESA/Gaia/DPAC. These are catalog-derived star maps, not a single photographic panorama. No constellation artwork is used. See the [SVS media-use guidelines](https://svs.gsfc.nasa.gov/help/).

The 32768×16384 linear half-float EXR originals are retained in the asset workshop outside Unity. `scripts/prepare-celestial-sky.sh SOURCE_DIR OUTPUT_EXR` downloads and verifies their SHA-256 hashes, then reduces the layers separately with OpenImageIO 3.2.2.0, box-averages to 8192×4096, adds their linear RGB values and writes a half-float DWA-compressed EXR. DWA compression is lossy; the unchanged original sources remain available for future exports. No tone mapping, arbitrary sky rotation, or fictional point stars are added.

## Coordinates and clock

NASA's plate carrée map has J2000/ICRF right ascension zero at the center, increasing to the left, with north declination at the top. The shader uses `u = 0.5 - atan2(y,x)/(2π)` and `v = 0.5 + asin(z)/π`. Horizontal sampling repeats, vertical sampling clamps. Wrapped UV derivatives prevent the RA seam selecting an incorrect mip.

`CelestialFrames` applies IAU 1976 precession from J2000 to the current date, then Greenwich mean sidereal rotation to ECEF. References: Lieske (1979), equations 6–7; [ERFA precession convention](https://github.com/liberfa/erfa/blob/master/src/pmat76.c). The visual approximation uses UTC for UT1/TT and omits nutation, aberration, proper motion and polar motion. It is not an astrometric navigation system.

The map uses the existing ECEF-to-Unity X,Z,Y convention. Flight converts the same celestial axes into the fixed launch site's east/up/north frame, matching the background globe. Never use the simulation's launch-relative inertial epoch as J2000. Both views read `MissionSunlight.CurrentUtc`: live UTC before launch, launch UTC plus simulated elapsed time during flight, frozen during pause and restored on reset. Camera translation has no effect on the sky.

## Runtime budget

A two-triangle full-screen background pass samples one serialized texture shared by map and flight materials. It draws before Earth and the existing atmospheric scattering; neither geometry nor scattering is bypassed. No runtime network request, EXR decoding or full-resolution CPU buffer is required. Per-camera material directions update without per-frame managed allocations.

The desktop importer selects linear RGB BC6H, 8192×4096, trilinear filtering and mipmaps, with CPU readability disabled. Expected GPU payload is about 42.7 MiB versus 4 GiB for a 32K RGBA-half texture before mipmaps. The derived EXR is tracked with Git LFS; fetch LFS assets during checkout before building. Once packaged, the sky works entirely offline. Never place the 32K sources inside Assets.

Runtime validation results are recorded below after packaged testing. Other target GPUs must support BC6H; Unity can otherwise expand the texture to RGBAHalf, so do not assume the measured Mac budget applies to untested hardware.

## Studio packaged validation (Unity 6000.6.9f1, Metal)

- Pure C# gravity/solar tests and added celestial epoch, sidereal-rate, orthogonal-basis, precession and UTC tests passed on the build machine (its Unity .NET 8 SDK, with temporary test-project target adaptation; repository targets are unchanged).
- Development macOS package built successfully. Loaded texture: **8192×4096 BC6H**, CPU readability false, `Profiler.GetRuntimeMemorySizeLong` **44,739,728 bytes (42.7 MiB)**, shared by both camera materials.
- Reviewed packaged captures of the map, Sagittarius region, night/day pad, and simulated flight above 150 km. The atmospheric pass hides stars in the daytime pad view; Earth occludes the background.
- Independently projected the source EXR into the Sagittarius capture: sampled luminance correlation **0.9741** for the expected north-up celestial mapping, versus **−0.0554** for an inverted vertical mapping. This verifies the EXR import orientation as well as the shader's coordinate handedness.
- `-espDayNightSmoke`: both launch sites, daylight/night, actual simulated ascent, time warp, frozen pause clock, map/flight switching and reset passed (`ESP_DAY_NIGHT_OK`).
- `-espCelestialSkySmoke`: two paired 240-frame sky-off/on samples at 1440×900, vsync disabled. Mean frame times: **1.043 → 1.125 ms**, **1.054 → 1.220 ms**; sky-on p95 **1.393 / 1.518 ms**. These are development-player wall-frame samples of an unobstructed sky view, not GPU timestamps or a full-mission frame-rate guarantee. No Windows or non-BC6H GPU runtime qualification is claimed.

Build with `BuildMission.Build` to regenerate serialized map/flight texture references in the mission scene. The existing checked-in scene predates several current runtime components; the build generator remains the authoritative scene setup.

Derived asset SHA-256: `2a15b40a38ccefcef2d623ec982abd7ce735d138893cdef600238bb274d6a122` (23,989,172 bytes). Git LFS upload and server verification succeeded.
