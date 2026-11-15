# SLS and solar/atmosphere packaged validation

A development macOS player built with Unity 6000.6.9f1 was run on the remote development machine at 1600 x 900. Only one operator used the shared renderer.

The opt-in `VisualPolishSmoke` run passed: SLS at Kennedy, daytime assemblies, 0.533-degree solar diameter, daylight flare visibility, night Earth occultation suppressing glare, rejected early staging during solid burn, solid separation, core separation, ICPS coast and circularization ignition, bounded low Earth orbit, and reset restoring the complete stack. Measured ascent altitude drawdown was 0 m. At the end of the 60-second unpowered verification the osculating conservative perigee was 200,024 m and apogee altitude 321,462 m. All motion came from the shipping driver; the clearly labeled UTC override changed presentation lighting only.

Captured pad day/night, horizon sunset, upper-stage ignition, orbital Sun and orbital horizon were visually inspected. The LVSA departed with the core; the approximate exposed ICPS and connected plume remained with the upper stage. The solar disc was visible through the blue atmosphere, and the orbital view showed restrained glare/streak and a continuous atmospheric limb. The old atmosphere's artificial sky-opacity boost was removed because it hid the daylight Sun; daytime stellar visibility is now adjusted separately before solar radiance is added.

At the same paused orbital camera, three-second samples recorded atmosphere-on p50/p95 17.33/19.86 ms and atmosphere-off 17.22/19.36 ms. These are capped wall-frame intervals, not isolated GPU measurements, and do not establish performance at other resolutions or on other hardware.

Independent pure-core tests passed solid-booster/staging behavior, SLS ascent drawdown and apogee bounds at both 20 ms and 10 ms steps, a 60-second coast, and Starship/SLS vehicle missions at all three sites. Earlier solar frames, Earth lighting, lunar/solar interpolation and gravity/orbit checks also passed. Optical scattering, bloom/exposure and the new ICPS mesh remain documented visual approximations; the source abort tower remains fused to the upper mesh.
