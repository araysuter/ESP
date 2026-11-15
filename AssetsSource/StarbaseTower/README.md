# Starbase launch tower: provisional model handoff

This directory contains a **full-scale, approximate static visual** for review,
not a complete or survey-accurate reconstruction. It is not wired into the
game. The components are Wavefront OBJ with adjacent MTL files; Blender uses
metres and Z-up, and the OBJ exports use metre-sized units, Y-up and -Z forward.
The launch mount's horizontal center at ground level is the common origin.
The geometry is outside Unity's Assets directory until the integration owner
checks it and imports it with matching `.meta` files.

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

1. Assign an owner to the Unity project/editor and import modules into a
   separate test project or an owned checkout; generate and retain `.meta`
   files. Assign URP materials and inspect normals and shading.
2. Build a prefab or procedural assembly from the modules, sharing the level
   mesh where possible. Replace the generated tower and mount in
   `LaunchEnvironment.Build` for Starbase, not the ground imagery. The existing
   optional `importedStructure` branch is for a historical Kennedy gantry and
   uses a separate offset; do not reuse it unchanged for this Starbase assembly.
3. Align the current vehicle's base and the mount top; verify launch clearance,
   arm placement, scale and site orientation. Existing rocket-base height is a
   placeholder. Keep this asset visual-only and camera-relative: do not use it
   as authoritative flight state or terrain/collision data.
4. Measure packaged performance on target hardware, optimize as needed, update
   attribution, and test reset and site selection. Verify the rendered mission
   and packaged builds on their actual target operating systems.

## Verification and reproduction

The assembled OBJ was exported and re-imported in Blender 5.2.3 LTS as 38 mesh
objects, with Y bounds -2.0 to 136.75 m. Blender discarded 32 degenerate
triangles from the source tower level and 14 from the QD arm. An isolated
Unity 6000.6.9f1 test could not run because the Windows editor had no valid
license. Unity import, materials, in-game appearance, and performance are
therefore **unverified**.

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
