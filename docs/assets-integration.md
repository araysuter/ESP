# Assets and Unity integration handoff

Owner: assets/integration agent, branch `asher/codex/assets-unity-integration`.
Flight/gameplay owner: teammate, branch `gravity`. No flight code is owned here.
The shared qualification MCP editor is unchanged; asset tests use a separate
integration project. Do not switch the shared endpoint without a handoff.

## Initial asset slice

- Original fictional two-stage rocket, approximately 38.4 m tall and 4 m body diameter.
- Blender generation source: `AssetsSource/build_rocket.py`. No external models,
  textures, logos, or trademarks. Not a dimensionally accurate historical rocket.
- Blender units are meters, +Z up. OBJ export uses -Z forward and +Y up for Unity.
  Rocket assembly origin is the stage-base center, not its center of mass.
- `BoosterAssembly` includes booster, engines and fins; upper stage and nose stay
  on the vehicle root. The interstage remains on the upper assembly for this placeholder.
- Mesh-only OBJ export; Unity assigns URP materials. No automatic .blend import.
  Preserve the editable .blend generated alongside source in the authoring workspace.
- The pad is an authored visual preview, not georeferenced terrain or Earth imagery.

## Presentation boundary (provisional until gravity lands)

`ESP.Presentation.RocketPresentation` accepts camera-relative **meters** and a
Unity-local quaternion. The flight adapter must subtract the double-precision
camera origin and convert frames **before** casting to Vector3. These values
are never read back as authoritative flight state.

`SetPose(position, attitude)` moves the upper/attached assembly.
`SetBoosterPose(position, attitude)` detaches the booster visual and applies its
independently computed pose; no separation dynamics or impulses are invented.
The caller supplies the rocket-base pivot pose, accounting for its center-of-mass
state if necessary. On reset, destroy both the detached booster object and the vehicle root, then instantiate a fresh prefab. The flight adapter owns this lifecycle.
`SetThrottle(fraction)` controls an optional assigned exhaust visual only.
The first asset preview has no exhaust visual assigned.

The asset-preview scene has no flight loop, controls, HUD telemetry, globe,
or fabricated orbit. Its persistent label identifies it as a visual preview.
Flight HUD, orbit rendering and camera-frame adapters will follow the actual
`gravity` API. Do not duplicate the partner's input or simulation loop.

## Reproduction

Use Blender 5.2.3 LTS to run:

```sh
blender --background --python AssetsSource/build_rocket.py -- <export-directory>
```

Copy only the OBJ into `UnityProject/Assets/ESP/Art/`. Keep the .blend outside
Unity's Assets directory. With Unity 6000.6.9f1 + URP 17.6.0, run the editor method
`BuildAssetPreview.Build`, setting `ESP_BUILD_PATH` to the output macOS app path.
The method checks imported bounds and named booster before building. It creates
materials, a prefab and a preview scene; run only in the owned integration project.

## Verified November 14

Blender export and Unity import passed with bounds 6.20 × 38.40 × 6.20 m.
The builder asserts all ten booster meshes are parented under BoosterAssembly.
The macOS Development app compiled and rendered the original rocket and pad
on the VM's Apple Paravirtual Metal device at 1280 × 720. This is visual smoke
qualification, not a performance benchmark or simulated flight.

Open `UnityProject` in the pinned editor, then open
`Assets/ESP/Art/AssetPreview.unity`. NASA imagery, a georeferenced Earth, exhaust,
and integration with the partner's `gravity` branch remain subsequent work.

Unity batch coordinate checks passed: origin, equatorial/polar axes, millimeter displacement at Earth radius, and NaN rejection.

## Partner branch checkpoint

Inspected `gravity` commit `7ca825b` on November 14. It currently supplies
`FlightCore.Vector3d` and `Gravity.Acceleration`, targeting netstandard2.1, plus
gravity tests. It does not yet supply a vehicle state integrator, mission clock,
propulsion, staging, or controls. Preserve that ownership with the partner.
Before wiring flight, agree on an immutable state snapshot: mission time, frame
convention, double position/velocity, attitude convention, stage state and actual
telemetry. Inertial positions must first be rotated to ECEF at the snapshot time
before calling the ECEF presentation helper.

The partner branch includes generated bin/obj artifacts; remove those from its
PR and retain source/project files. Repository ignore rules now cover new caches.
