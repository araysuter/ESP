# Navball frame checks

Run in the project VM, per the current development workflow:

```sh
dotnet run --project Navball.Tests/Navball.Tests.csproj --configuration Release
```

The harness compiles the exact pure C# helper used by the Unity instrument. It checks surface-rotation subtraction, ECI-to-local ENU conversion at both launch sites and several mission epochs, the stationary pad (including pole), actual nose centering, heading handedness, zenith, and spherical projection round trips. It does not compile Unity UI code or validate rendered output; those require the shared Unity project and its designated editor owner.

The instrument is an orthographic sphere with a geodetic local horizon, 30° meridians/parallels, 10° short pitch ticks, cardinal labels, a fixed amber nose indicator and a green surface-relative prograde ring. Prograde is hidden below 1 m/s and on the back hemisphere or when its full ring would cross the rim. The flight core models pitch and heading only; roll is not measured or simulated. The displayed zero-bank reference is disclosed as `ROLL N/A`. The prelaunch frame uses vertical attitude and the selected site's heading, with no velocity marker. Current flight attitude comes from `ActualPitchDegrees`/`ActualHeadingDegrees`, independently of the rendered rocket/camera and of pitch commands. Texture allocation is per instrument lifetime; repainting occurs only when attitude changes by 0.02°, while the velocity marker updates with telemetry.
