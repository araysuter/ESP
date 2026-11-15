# Unexecuted planar-cleanup trial

`qualify_blender.py` is prepared tooling, **not a completed qualification**.
It has not run in Blender. No derived meshes, images, triangle reductions,
Unity imports, performance results, or approval are supplied by this change.

The intended trial uses Blender 5.2.3 LTS in a new background process. The
`run_blender_trial.py` wrapper enforces two threads and a 180-second timeout,
requests a nonzero process exit on Python errors, and requires a completed
six-module report. Run it on the designated compute host.
Obtain an explicit editor/compute handoff first; preserve other editors,
players and workloads. Use a fresh output directory outside the input modules:

```sh
python3 AssetsSource/StarbaseTower/run_blender_trial.py \
  --blender /path/to/blender MODULE_DIR NEW_OUTPUT_DIR
```

The script refuses interactive Blender, a loaded `.blend` file, missing source
modules, a different Blender version, and an existing output directory. It reads the source modules and
creates separate candidate meshes. The proposed operations dissolve tiny
degenerate edges and nearly coplanar faces, retriangulate, and record bounds
and triangle counts. It applies the handoff's intended placeholder colors to
both comparison groups, creates 23 shared instances of the level mesh, and
requests paired overview renders plus an editable review file. Its report
records input SHA-256 hashes and the relevant camera and cleanup settings.
Timeout or failure may leave partial files only in the new output directory;
they are not qualified results. Do not rerun over that directory.

Before accepting any result, inspect the log for API or import errors; rerun
`audit_modules.py` on the exports; check the full tower and close-up silhouette,
normals, arm openings and platform edges. An overview render alone does not
establish unchanged appearance. Retain input hashes and trial settings. Any
candidate still needs owned Unity import, URP material assignment, vehicle
clearance checks, LOD selection, and packaged frame-time measurements before
runtime integration. The original-source license confirmation documented in
`README.md` remains pending.
