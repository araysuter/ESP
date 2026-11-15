# Starbase launch tower: provisional model handoff

This directory contains a **full-scale, approximate static visual**. The
mission builder imports these OBJ and MTL modules into Unity's generated
assets and selects the assembled prefab at Starbase. Blender uses metres and
Z-up. The OBJ exports use metre-sized units, Y-up and -Z forward. The launch
mount's horizontal center at ground level is the common origin.

## Provenance and adaptations

Derived from **High detail, mechanized Spacex Starship Mechazilla tower and
OLM**, by **herbys (Enrique Saggese)**,
<https://www.thingiverse.com/thing:5403074>,
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Adaptations:
selected, recentered, scaled, repeated and positioned print meshes; assigned
placeholder materials; added a simplified foundation and mount support legs;
and omitted most mechanical fittings, hardware and cables. Credit the author,
link the source and license, and describe the changes in distributed credits.
The original source archives and editable Blender file are retained separately.

## Assemble the model

All component vertices already use the common origin. Add one of each OBJ
except `TowerLevel.obj`, which must be instantiated **23 times** at Unity-local
offset `(0, i * 5.67231, 0)` metres for `i = 0..22`. `TowerTop.obj` is already
positioned at the top; do not offset it. Each OBJ refers to its own adjacent
MTL. Tower top is about 136.75 m above ground; foundation extends 2 m below.
Mount top is approximately 9.1 m above ground. Original STL meshes were
specified at 1:100 in millimetres, and were multiplied by 0.1 for metres at
full scale. Flat materials are placeholders, not final URP materials.

The catch arms and QD arm have an approximate, static pose. Their attachment,
clearance, pivots and alignment with the current vehicle are unverified. The
mount legs and foundation are simple stand-ins for parts the source author
expected to fabricate separately. Tank farm, terrain, colliders, rig, animation,
and vehicle are not supplied. Total assembled geometry is approximately
781,000 triangles, with no measured game performance.

## Integration and review remaining

1. Run `BuildMission.Build` in an owned Unity checkout. Inspect the generated
   prefab's normals and shading. The builder shares one mesh across the
   23 tower levels and assigns URP materials.
2. Select Starbase to verify the imported tower and mount replace the procedural
   versions. Switch to Kennedy and back to check site selection and reset.
3. Align the current vehicle's base and the mount top; verify launch clearance,
   arm placement, scale and site orientation. Existing rocket-base height is a
   placeholder. Keep this asset visual-only and camera-relative: do not use it
   as authoritative flight state or terrain/collision data.
4. Measure packaged performance on target hardware, optimize as needed, update
   attribution, and test reset and site selection. Verify the rendered mission
   and packaged builds on their actual target operating systems.

## Verification and reproduction

The assembled OBJ was exported and re-imported in Blender 5.2.3 LTS as 38 mesh
objects, with Y bounds -2.0 to 136.75 m. That import report is not evidence
that the separately exported modules are clean. An isolated
Unity 6000.6.9f1 test could not run because the Windows editor had no valid
license. Unity import, materials, in-game appearance, and performance are
therefore **unverified**.

The subsequent source audit of commit `98c72919c92bdb91ee9cc4f51fcb2af0687728f7`
ran on the designated compute host. See `module-audit.json` for input SHA-256
hashes, exact bounds and per-module counts. All vertex/normal/UV values are
finite, face references are valid, and material references resolve. The
assembly contains **780,726 triangles**. At a squared cross-product threshold
of `1e-20 m^4`, **32 triangles in TowerTop and 3 in QuickDisconnect are
degenerate**. No source geometry was modified by this audit. All exported
materials currently use the same gray diffuse value despite their distinct
names; assign and verify intended URP colors before a visual review.

```sh
python3 AssetsSource/StarbaseTower/audit_modules.py AssetsSource/StarbaseTower --output module-audit.json
```

License attribution above is the handoff author's declaration. The primary
Thingiverse page returned HTTP 403 during the subsequent audit, so independent
confirmation remains pending. Retain the original download license metadata
and source archive hashes before distributing an integrated derivative. Do
not substitute a similarly named remix's license for this original model's.

To regenerate, obtain the original CC BY source, preserve its archives and
extract the required STL files into `STL_DIR`. The source archive contains two
different pairs of files sharing filenames; preserve them separately and
inspect rather than silently overwriting. The scripts use Blender 5.2.3 LTS:

```sh
blender --disable-autoexec --background --python AssetsSource/StarbaseTower/build_handoff.py -- STL_DIR OUTPUT_DIR
blender --disable-autoexec --background --python AssetsSource/StarbaseTower/export_modules.py -- OUTPUT_DIR/ESP-Starbase-Tower.blend MODULE_DIR
```

The reconstructed source and module exports are provisional and require the
review steps above before being used in a shipped game.

### Efficient integration gate

- Preserve the six source modules. Remove verified zero-area faces in derived
  exports, then compare bounds and silhouette; do not silently rewrite the
  source files. Store source hashes and optimization settings with each export.
- Share one level mesh across its 23 instances. Levels alone account for
  631,948 triangles (81% of the assembly), so concentrate geometry reduction
  there before simplifying the more visible catch arms. Do not combine all
  repeated geometry into a single duplicated mesh merely to reduce object count.
- Qualify explicit LODs using packaged camera views at the pad and ascent,
  including both cameras when picture-in-picture is active. Choose screen-size
  transitions from observed silhouette quality, and measure the same route
  with and without the tower. Record frame p50/p95, resolution and graphics API.
- Keep colliders disabled and the tower outside authoritative flight state.
  The mount top is 9.1 m and tower height is 136.748581 m in these source
  coordinates. Verify the vehicle base offset and arm clearance before launch;
  neither the mesh bounds nor the source audit establishes clearance.
- Import into an explicitly owned Unity project only after the previous
  editor owner hands it off. Commit generated `.meta` files and a reviewed
  prefab only after reset, site selection and packaged rendering pass.
