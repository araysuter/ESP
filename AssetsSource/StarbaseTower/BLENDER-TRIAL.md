# Unexecuted planar-cleanup trial

`qualify_blender.py` is prepared tooling, **not a completed qualification**.
It has not run in Blender. No derived meshes, images, triangle reductions,
Unity imports, performance results, or approval are supplied by this change.

The intended trial uses Blender 5.2.3 LTS in a new background process, with
two threads and an external 180-second timeout on the designated compute host.
Obtain an explicit editor/compute handoff first; preserve other editors,
players and workloads. Use a fresh output directory outside the input modules:

```sh
blender --factory-startup --disable-autoexec --background --threads 2 \
  --python AssetsSource/StarbaseTower/qualify_blender.py -- MODULE_DIR NEW_OUTPUT_DIR
```

The script refuses interactive Blender, a loaded `.blend` file, missing source
modules, and an existing output directory. It reads the source modules and
creates separate candidate meshes. The proposed operations dissolve tiny
degenerate edges and nearly coplanar faces, retriangulate, and record bounds
and triangle counts. It applies the handoff's intended placeholder colors to
both comparison groups, creates 23 shared instances of the level mesh, and
requests paired overview renders plus an editable review file.

Before accepting any result, inspect the log for API or import errors; rerun
`audit_modules.py` on the exports; check the full tower and close-up silhouette,
normals, arm openings and platform edges. An overview render alone does not
establish unchanged appearance. Retain input hashes and trial settings. Any
candidate still needs owned Unity import, URP material assignment, vehicle
clearance checks, LOD selection, and packaged frame-time measurements before
runtime integration. The original-source license confirmation documented in
`README.md` remains pending.
