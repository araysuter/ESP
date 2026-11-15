# Physics verification and model limits

Source audit: **2026-11-14**, baseline commit **`9d9848773a208ddff1e97403e6c88c94c54b599b`**, followed by review of the integrated cheats, telemetry, session and weather-height changes associated with **PR #25 head `3d84e4c6fcc43d59c15e92b4991ef09a06d920e6`**. This review compares implemented equations and units with primary references and inspects numerical fixtures. The reviewer did **not** execute tests, a Unity build, or a flight. The coordinator's subsequent numerical, Unity build and packaged menu results are recorded below. Earlier results in [rocket-flight.md](rocket-flight.md) and [vehicle-specs.md](vehicle-specs.md) describe earlier verification contexts.

The core uses defensible equations for an **educational point-mass Earth launch simulation**. That does not establish the accuracy of a real Starship or SLS trajectory. Vehicle inputs, atmosphere outside its supported range, attitude control, aerodynamics and collision response contain substantial approximations. A provider height-convention mismatch found during the baseline audit has been corrected in source, with the Unity-side check now passing.

## Units, constants and frames

Authoritative `FlightCore` position, velocity, time, propellant and force calculations use C# `double` values in SI. Angles passed into frame transforms are radians; steering commands are degrees with explicit conversion. Unity coordinates and visual scale do not determine authoritative flight state.

| Quantity | Implementation value / unit | Assessment |
| --- | --- | --- |
| Earth gravitational parameter `mu` | `3.986004418e14 m^3/s^2` | WGS84 conventional GM, not a separately estimated Earth mass times an inconsistent G. |
| WGS84 semi-major axis `a` | `6378137 m` | Full Earth scale. |
| WGS84 flattening `f` | `1/298.257223563` | Semi-minor axis is derived as `b=a(1-f)`. |
| Constant rotation `omega` | `7.292115e-5 rad/s` | Conventional WGS84 mean rotation, simplified to the inertial +Z axis. |
| Standard gravity `g0` | `9.80665 m/s^2` | Used for Isp conversion and the standard atmosphere; not substituted for local orbital gravity. |
| Pressure / temperature / density | Pa / K / kg/m^3 | Celsius conversion is `K = degC + 273.15`; hPa is multiplied by 100. |

The Earth constants agree with [NGA's WGS84 defining parameters](https://earth-info.nga.mil/?action=wgs84&dir=wgs84). Standard gravity and the temperature conversion are conventional unit definitions, supported by [NIST's conversion reference](https://www.nist.gov/pml/special-publication-811/nist-guide-si-appendix-b-conversion-factors/nist-guide-si-appendix-b9) and [NIST's standard-acceleration table](https://nvlpubs.nist.gov/nistpubs/Legacy/MP/nbsmiscellaneouspub253.pdf). This choice is intentionally WGS84 GM; it need not equal a later fitted ephemeris GM digit for digit.

`EarthFrames` uses right-handed ECEF: X at the equator/Greenwich, Y at the equator/90 degrees east, and Z north. With geodetic latitude `phi`, longitude `lambda`, height `z`, and `e2=f(2-f)`, its forward transform uses `N=a/sqrt(1-e2*sin(phi)^2)` and coordinates `((N+z)cos(phi)cos(lambda), (N+z)cos(phi)sin(lambda), (N(1-e2)+z)sin(phi))`. The iterative inverse returns geodetic height, not geocentric radius minus a constant.

The simplified inertial frame coincides with ECEF at **mission time zero**, then `r_I=Rz(omega*t) r_E`. It is not J2000, ICRF, or a date-calibrated Earth-orientation model. The stationary pad starts with `v_I=omega cross r_I`. Air velocity is `omega cross r_I + wind_I`; subtracting it before calculating drag is essential. Rotation of a velocity vector alone does not subtract rotating-ground velocity.

`WindToInertial` uses geodetic ENU and the inertial longitude `lambda+omega*t`. Meteorological wind **from** north becomes negative north; wind from east becomes negative east. Thrust elevation/heading are also resolved in ENU at the **current vehicle location**, not the original pad. Presentation maps ENU east/up/north into Unity X/Y/Z, which reverses handedness, and subtracts the ECEF origin in doubles before casting to floats. See [earth-frames.md](earth-frames.md).

## Translation, gravity and numerical integration

`RocketSimulation.Derivative` implements

```text
dr/dt = v
dv/dt = -mu*r/|r|^3 + (T_core + T_SRB)*direction/m + F_drag/m
df_core/dt = -T_core/(Isp*g0)
df_SRB/dt = -T_SRB/(Isp_SRB*g0)
```

Every acceleration term has units `m/s^2`; propellant rates are `kg/s`. This is central Newtonian gravity with no J2/other harmonics, Moon/Sun perturbations, relativity, tides or rotating-frame fictitious forces. No centrifugal or Coriolis force belongs in the free-flight inertial equation: Earth rotation instead enters the initial velocity, air motion, and coordinate transforms. The inverse-square gravity and reference orbit relations agree with [NASA's orbital-equations reference](https://ntrs.nasa.gov/api/citations/19910002328/downloads/19910002328.pdf).

The production driver accumulates real elapsed time times the selected rate and advances the same **0.02 s fixed steps**, retaining any backlog rather than skipping simulated time. `RocketSimulation.Integrate` evaluates the classical four RK stages at start, midpoint, midpoint and end and combines them with weights `1,2,2,1` divided by six. Position, velocity and both propellant quantities are integrated together; pressure, wind, local direction, drag and mass are evaluated at trial states. This agrees with the RK4 formula in [NASA GEONS mathematical specifications, section 4.2](https://ntrs.nasa.gov/api/citations/20240004259/downloads/GEONSMS_R3_0_NASA-TP-20240004259.pdf).

**Do not call the entire gameplay solver fourth-order accurate.** Actual pitch and heading advance once before each fixed step and remain fixed during its RK stages; guidance is sampled once per step. Pad hold-down advances and consumes propellant over a whole tick. Impact time uses a linear fraction of the crossing step. These discontinuities and controller splitting can dominate global error. The existing two-step comparisons test specific error bounds, not an observed fourth-order convergence rate.

The separate `OrbitSimulator` fixture uses **velocity Verlet**, not RK4: a half velocity kick, full position drift and second half kick. Its circular-orbit test checks the expected second-order error reduction and energy/angular-momentum drift. Passing that fixture alone would not verify `RocketSimulation`'s RK4 implementation.

## Atmosphere, density and drag

`StandardAtmosphere` converts geometric altitude `z` to geopotential height `H=R*z/(R+z)` using `R=6356766 m`. It integrates the lapse-rate layers at geopotential heights `0,11,20,32,47,51,71,84.852 km`, starting at `288.15 K` and `101325 Pa`. For lapse `L`, it uses `T=T_b+L*(H-H_b)` and `p=p_b*(T/T_b)^(-g0/(R_air*L))`; for an isothermal layer it uses `p=p_b*exp(-g0*(H-H_b)/(R_air*T_b))`. These are the hydrostatic, ideal-air lower-atmosphere relations associated with the [NOAA/NASA/USAF U.S. Standard Atmosphere, 1976](https://ntrs.nasa.gov/citations/19770009539). This is a lower-layer implementation, not the complete published atmosphere through 1000 km.

Above approximately **86 km geometric altitude**, the code retains the top-layer temperature, extends pressure exponentially, and applies a smooth artificial taper to **zero pressure and density at 120 km**. This is a game boundary, not the physical end of Earth's atmosphere. It removes thermospheric drag, real atmospheric variability and long-term orbital decay. Lower-model sampling is limited to `z>=-500 m`; intermediate below-ground RK evaluations are clamped to that minimum before impact handling.

`AtmosphereSample` computes dry ideal-air density as `rho=p/(287.05287*T)`, consistent in form and units with [NASA Glenn's equation of state](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/equation-of-state/). It does not model humidity, changing gas composition, rarefied-flow effects or chemical dissociation. Accurate high-altitude kinetic temperature/composition is outside this model.

`Aerodynamics.Compute` implements `u=v_vehicle-v_air`, `q=rho*|u|^2/2`, and `F_drag=-q*Cd*A*u/|u|`, with a zero-speed branch. This is [NASA Glenn's drag equation](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/drag-equation/) applied opposite air-relative motion. Thus `F_drag dot u<=0`; drag can still exchange inertial energy with moving air. `Cd` and reference area are fixed per active stage. There is no Mach/Reynolds/angle-of-attack curve, lift, stall, aerodynamic torque, breakup or separate booster drag calculation. Displayed dynamic pressure is instantaneous; a peak-Q claim requires accumulating its maximum over a run.

## Weather interpretation and corrected height convention

`WeatherAtmosphere` freezes one timestamped vertical column at launch. It interpolates temperature and ENU wind linearly and pressure logarithmically; outside supplied levels it blends pressure/temperature ratios and wind into the baseline over 500 m below and 2000 m above. This preserves positive pressure and temperature, but is an interpolation/extrapolation approximation, not a hydrostatically reconstructed four-dimensional atmosphere. The column follows altitude along the entire flight without horizontal weather evolution, gusts, vertical wind or in-flight time updates. Dry-air density is still used.

At the reviewed revision the Unity handoff prefers a matching requested-site NASA analysis no more than 12 hours old, then a nonfuture Open-Meteo profile no more than two hours old, otherwise the offline atmosphere. These are application freshness policies, not accuracy guarantees. NASA native-height interpretation and spherical geopotential conversion remain qualified in [nasa-weather-provider.md](nasa-weather-provider.md). Actual requested/grid coordinates, provider and UTC valid time should be saved with test evidence. Point-weather and satellite/cloud displays are not additional flight-force inputs.

**Baseline finding, now corrected in source:** `LaunchWeatherProfile.Parse` originally passed Open-Meteo's `geopotential_height_*` fields directly into a height later treated as geometric. The provider documents these as [geopotential heights above sea level](https://open-meteo.com/en/docs). Geopotential and geometric height are distinct; [ECMWF's primary documentation](https://confluence.ecmwf.int/pages/viewpage.action?pageId=226496389) gives the spherical approximation `z=R*H/(R-H)`. The integrated parser now applies that conversion with `R=6371000 m`, matching the NASA provider's spherical convention. At `H=20 km` it yields approximately `20062.982 m`. The source-inspected Unity editor fixture `WeatherHeightChecks.Validate` checks that value, the unchanged surface row and hPa-to-Pa conversion; its Unity execution and the associated packaged build passed. Mean-sea-level/geoid versus WGS84 ellipsoidal height remains an unmodeled datum difference. The near-surface row also combines 2 m temperature and 10 m wind with surface pressure at the grid elevation; it is a representative lower boundary, not a collocated instrument measurement.

## Engines, mass, staging and attitude

The core's `mdot=T/(Isp*g0)` is dimensionally and physically consistent with [NASA's specific-impulse definition](https://www.grc.nasa.gov/www/BGH/specimp.html). With constant Isp and no external forces it should reproduce `delta-v=Isp*g0*ln(m_initial/m_final)`, the [ideal rocket equation](https://www1.grc.nasa.gov/beginners-guide-to-aeronautics/ideal-rocket-equation/). This identity requires a separate controlled fixture; realistic ascent includes gravity and drag losses.

Liquid thrust and Isp interpolate independently between supplied vacuum and sea-level endpoints using ambient pressure divided by `101325 Pa`, clamped to `[0,1]`. Throttle scales liquid thrust and therefore mass flow. This supplies an effective engine performance table; it is not a chamber/nozzle-flow solution. Real nozzle thrust includes momentum and pressure terms, `F=mdot*v_exit+(p_exit-p_ambient)*A_exit`, as described by [NASA's rocket thrust equations](https://www.grc.nasa.gov/WWW/BGH/rktthsum.html). Independent endpoint estimates need not imply a real engine's mass-flow/throttle behavior.

Mass includes the active dry mass and remaining propellant, all later stages' wet masses, and any attached solid-booster dry mass and propellant. Serial staging removes the current stage and any unburned propellant, preserves the surviving position/velocity/time exactly, and selects the next stage. This is zero separation impulse, with no independently propagated discarded stages. It does not conserve the modeled system's total momentum by tracking debris, but preserves the surviving craft's velocity as intended for zero relative separation speed.

Parallel solids contribute both thrust and mass while attached and burn independently of liquid throttle. Their constant mass flow is derived from constant thrust/Isp. The integrator splits at solid burnout and brackets liquid depletion, with finite searches, before coasting the remainder of a step. Spent booster dry mass remains until jettison. The SLS pair is symmetric and aggregated: no thrust curve, pressure correction, shutdown command, asymmetric failure or certified motor behavior. Its inferred Isp matches the chosen mass/burn duration with **maximum vacuum thrust treated as constant**; this is a substantial performance approximation. See [solid-booster-physics.md](solid-booster-physics.md) and the sourced/estimated parameter distinctions in [vehicle-specs.md](vehicle-specs.md).

Actual attitude follows commanded local elevation and heading at at most **5 degrees per simulated second**; heading takes the shortest wrapped angular path. Elevation is restricted to 0–90 degrees. This is direct finite-rate steering, **not torque, gimbal authority, angular momentum or six-degree-of-freedom attitude dynamics**. No roll, moment of inertia, reaction controls or control-authority/fuel penalty is simulated. Assisted ascent is an altitude-based pitch program through the same controls, with ordinary staging and cutoff; it is not a real launch guidance algorithm.

## Surface contact, orbit and cheats

The pad remains attached to rotating Earth until vertical thrust exceeds the projection of central gravity along geodetic up. This hold test omits centrifugal relief, a small conservative error near thrust-to-weight one; mass-driven release is reconsidered on the next tick.

Flight contact is a crossing of **the launch pad's constant WGS84 geodetic height**, using a linearly interpolated impact time and position. It is not collision with the flat authored visual pad, mountains, ocean waves, image pixels or a terrain database. The baseline sets velocity to rotating-ground velocity and stops integration at contact: it is an inelastic stop with no suspension, bounce or resolved structure. `OrbitProjection` intersects the zero-height WGS84 ellipsoid, whereas the flight collision shell uses pad height; they agree for the current zero-elevation sites, not for an arbitrary elevated pad.

`OrbitDiagnostics` uses inertial `h=r cross v`, `epsilon=|v|^2/2-mu/|r|`, `e_vector=(v cross h)/mu-r/|r|`, `p=|h|^2/mu`, and `r_peri=p/(1+e)`. For a bound nonradial conic it obtains `a=-mu/(2*epsilon)`, `r_apo=a*(1+e)` and `period=2*pi*sqrt(a^3/mu)`. These are instantaneous osculating quantities, consistent with [JPL NAIF's inertial-state requirement and singular-case cautions](https://naif.jpl.nasa.gov/pub/naif/toolkit_docs/C/cspice/oscelt_c.html). Degenerate radial, nearly circular and near-parabolic cases need explicit tolerances; orientation of circular apsides is not a meaningful observable.

The map draws a gravity-only conic at the **current** Earth angle, clipped at its first predicted WGS84 contact. It is a coast prediction if engines cut now, not a finite-burn trajectory or future rotating ground track. Apsidal geodetic heights are measured at radial apsides; they are not necessarily the orbit's minimum geodetic height.

Success uses `r_peri-a_WGS84>=180000 m`, negative orbital energy, an airborne state and 60 consecutive unpowered simulated seconds. Guidance targets a 200 km bound. The criterion is a **sufficient conservative clearance bound**: WGS84 is contained inside the radius-`a_WGS84` sphere, so clearance from that sphere guarantees at least that clearance from the ellipsoid. It can reject some otherwise qualifying inclined orbits and is not an exact minimization of WGS84 height. A one-minute coast with the model's artificial vacuum does not establish real orbital lifetime or reentry safety.

The integrated `RocketSimulation` implements gravity scaling from **0 through 5**, infinite remaining propellant and a crash/destruction toggle. Defaults are ordinary gravity, finite propellant and enabled destruction. Gravity scaling affects both integration and the liftoff check, leaving Earth rotation and atmosphere unchanged. Infinite fuel freezes remaining liquid and attached-solid supplies and their mass, including during hold-down; it does not refill empty tanks or permit burning solids to separate. Disabling destruction leaves surface collision and the terminal stop enabled.

Ground-relative impact velocity is interpolated and recorded **before** velocity is replaced by rotating-ground velocity. With damage enabled, speed **strictly greater than 10 m/s** sets `IsDestroyed`; both intact and destroyed impacts remain terminal. This threshold is a gameplay rule, not a material-strength or safe-landing calculation. There is no per-part heat flux, temperature, ablation, stress or thermal failure model.

Nondefault assignments immediately set sticky `HasUsedCheats`. Restoring defaults does not erase it. Core `Reset()` clears it; the Unity session also retains a modified-flight flag across option restoration and ordinary flight reset, cleared by **New mission** or **Restart mission**. `RocketFlightDriver` invalidates ordinary `OrbitVerified` for either flag. Nonstandard gravity disables assisted guidance, while the map conic uses the scaled gravitational parameter; zero gravity explicitly has no Kepler projection. This is a modified sandbox trajectory, not normal orbit qualification.

`RocketPhysicsTelemetry` uses the **same force evaluator as integration** for sampled atmosphere, mass, gravity, thrust and drag. Its acceleration is the magnitude of the vector free-flight force sum divided by mass; it excludes pad/ground reaction and is labeled accordingly. It is not an accelerometer's proper acceleration. Total propellant includes active, attached-solid and future-stage supplies, excluding discarded fuel. `MissionSession` pauses fixed-step advancement while menus are open; time spent paused is not added to the integration backlog. Unity menu/input and packaged pause/resume behavior still require their separate Unity rehearsal.

## Verification fixtures and coordinator-reported VM results

The table describes source-inspected coverage and acceptance thresholds. The coordinator subsequently reported that **all ten numerical suites passed** on the designated VM at PR #25 head `3d84e4c6fcc43d59c15e92b4991ef09a06d920e6`, including `Cheats.Tests`. This review did not independently execute them. Their environment and scope are recorded after the table; a suite passing at a different revision/platform is historical evidence only.

| Suite | Meaningful coverage and current bounds |
| --- | --- |
| `FlightCore.Tests` | Surface/off-axis/inverse-square gravity; gravity-only Verlet circular orbit at 7000 km radius for 5820 s, 10/5 s steps; position errors <=1750/450 m, velocity <=1.9/0.5 m/s, error ratio 3.8–4.2, relative energy drift <=1e-8 and angular momentum drift <=1e-12. |
| `Frames.Tests`, `Navball.Tests` | Representative sites, poles, negative/high altitude, ECEF/geodetic and inertial round trips; rotating air, surface-relative velocity and ENU attitude projection. Frame altitude round trip <=0.1 mm in the supplied cases. |
| `Atmosphere.Tests` | Independent layer references at 0/11/20/32 km geopotential; T <=0.02 K, pressure <=max(0.05 Pa,0.05%), density <=max(1e-5 kg/m^3,0.1%); boundary continuity, dry density, drag direction/strength, winds, profile interpolation/freshness/invalid data. |
| `Rocket.Tests` | Pad/reset, fuel cutoff, zero-throttle coast, staging position/velocity/time and mass accounting; failed suborbital contact; 20/10 ms comparison at 30/60 s with componentwise position <=0.1 m, velocity <=0.02 m/s and fuel <=0.1 kg. |
| `Booster.Tests` | Parallel independent fuel/mass, solids at zero liquid throttle, spent dry mass, separation continuity/reset, and two depletion events within one step; 0.2/0.005 s event fixture <=5 mm and <=0.005 m/s per component. |
| `Mission.Tests` | Fictional demo at both sites offline and one explicitly historical NASA profile; 20/10 ms cutoff difference <=0.1 s, perigee-bound difference <=2 km, 60 s coast above 180 km bound. |
| `Vehicle.Tests` | Estimated Starship/SLS presets at both sites, 20/10 ms steps; cutoff difference <=1 s and bound difference <=2 km; initial cutoff bound >=200 km and subsequent 60 s coast >=180 km. Does not compare to flight telemetry. |
| `OrbitProjection.Tests` | Circular/elliptic/open/impact/polar/grazing paths, current-angle consistency, surface-start inward/outward/tangent and invalid/near-radial states; impact height tolerance 2 cm. |
| `Cheats.Tests` | Defaults/sticky flags/reset, rejected/scaled/zero gravity and liftoff, infinite liquid/solid/parallel/held fuel without refill, resumed depletion, destructive versus gentle contact, retained pre-stop speed, and telemetry agreement with the integration force/mass path. |

Additional high-value cases before stronger accuracy claims:

1. Check **the production rocket integrator** against an analytic gravity-only orbit at several steps, including energy and angular-momentum drift; the existing Verlet fixture exercises a different integrator.
2. Isolate constant-Isp vacuum propulsion with external acceleration disabled and compare fuel use, cutoff impulse and delta-v to the ideal rocket equation, including an exhausted tank partway through a step. Keep this modified-physics fixture separate from normal mission qualification.
3. Compare `h`, `h/2`, `h/4` trajectories with a fixed control program and through steering, stage events and impact; measure observed convergence instead of assuming RK4 order.
4. Add independent atmosphere reference rows at 47/51/71/84.852 km geopotential. Continuity alone cannot establish correct values; explicitly bound any difference from the full 1976 composition/temperature treatment near its upper boundary.
5. Broaden provider fixtures for hPa/K conversion, grid/site metadata and stale/future/provider switching. Test the NASA height assumption independently; data freshness does not validate a height datum.
6. Test landing at nonzero pad height and multiple latitudes, almost-zero launch clearance, high-speed crossing at half steps, and recorded ground-relative impact speed before freezing. Compare the orbit-map ellipsoid with the chosen collision shell.
7. Extend the passing Unity session rehearsal with controlled threshold cases just below/at/above 10 m/s and sustained orbital disqualification; current core tests cover gentle/destructive outcomes and core sticky flags, while sustained orbital certification is a separate Unity path. These check gameplay consistency, not a damage model's physical accuracy.

### Integrated VM evidence and remaining gates

The coordinator reported the following completed numerical run:

- Source revision: **PR #25 head `3d84e4c6fcc43d59c15e92b4991ef09a06d920e6`**.
- Target: **Apple Silicon macOS VM**.
- Toolchain: **.NET SDK 8.0.318 bundled with Unity**. Test project targets were changed from `net9.0` to `net8.0` **only in an isolated VM copy**; repository targets were not changed for this run.
- **Passed all ten numerical projects:** `FlightCore.Tests`, `Atmosphere.Tests`, `Frames.Tests`, `Rocket.Tests`, `Mission.Tests`, `Booster.Tests`, `Vehicle.Tests`, `Navball.Tests`, `OrbitProjection.Tests`, and `Cheats.Tests`.
- **Passed four NASA parser Python unit tests.** These do not exercise Unity's Open-Meteo parser or establish current upstream weather availability.

On **2026-11-15 at approximately 00:27 UTC**, the ARM macOS Unity player built successfully with Unity **6000.6.9f1**. Build validation emitted `ESP_CLOUD_BLEND_CHECKS_OK`, `ESP_WEATHER_HEIGHT_CHECKS_OK` and `ESP_MISSION_BUILD_OK`. The packaged `-espMenuSmoke -espKeepOpen` rehearsal emitted `ESP_MENU_SMOKE_OK`: initial start menu, launch, exact position/time/fuel freeze during pause, nested settings/back, 30 FPS cap and VSync application, restored 60 FPS/off defaults, sticky modified status after restoring cheats, normal resume without accumulated pause time, return-to-start/resume, and clean New Mission reset passed. Native UI checks also exercised Escape, start/pause/settings/cheats navigation and window resizing. The inspector was rendered during a real short launch, not a synthetic trajectory.

Numerical invocation, from the isolated test copy, was `dotnet run --configuration Release --project <Suite>.Tests/<Suite>.Tests.csproj` for each of the ten listed suites. NASA fixtures used `python -m unittest discover -s tests -p 'test_nasa_weather.py'`. Representative successful outputs included the LC-39A historical-weather mission at 0.01 s: cutoff 421.130 s, minimum bound 200097.5 m; SLS South Texas at 0.01 s: cutoff 1044.66 s, coast minimum 200013.1 m. These are model results, not measured vehicle trajectories.

All three GitHub CI workflows also passed the source revision on .NET 9/Linux: [rocket checks](https://github.com/araysuter/ESP/actions/runs/43658196416), [atmosphere checks](https://github.com/araysuter/ESP/actions/runs/43658196407), and [flight core](https://github.com/araysuter/ESP/actions/runs/43658196363).

The new package has not received a network-disconnected end-to-end orbit rehearsal. The analytic ideal-rocket fixture and production-RK4 coast test proposed above have not been added. Windows/Intel player behavior and real-vehicle trajectory accuracy remain unverified. Modified-physics runs cannot supply normal orbit qualification.
