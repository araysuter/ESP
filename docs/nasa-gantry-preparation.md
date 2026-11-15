# NASA historical LC-39 gantry preparation

`AssetsSource/prepare_nasa_gantry.py` adapts the downloaded NASA gantry; it does
not create replacement structures. Run only in the qualified Blender 5.2.3
authoring VM, using a dedicated background process. It clears that process's
scene. Source editing and review alone do not establish that conversion works.

```sh
blender --background --python AssetsSource/prepare_nasa_gantry.py -- SOURCE.glb OUTPUT_DIRECTORY --preview
```

Omit `--preview` to skip the optional Workbench PNG. The output directory should
be an authoring/export directory, not Unity's `Assets` directory. Re-running
replaces the script's named outputs there; preserve any manually edited blend
under a separate filename first.

## Provenance and scale

- [NASA Gantry model](https://science.nasa.gov/3d-resources/gantry/), credited to
  **NASA / Michael D. Carbajal**.
- NASA-3D-Resources revision `11ebb4ee043715aefbba6aeec8a61746fad67fa7`, file
  `3D Models/Gantry/Gantry.glb`.
- Expected downloaded SHA-256:
  `ad89678f80ca08102fa8c730fde6b93a0b700c60a15d69d5de422f839c66eb82`.
- The coordinator's VM source inspection found 724 mesh objects, 176,490
  vertices, 172,602 polygons, three gray materials and no image textures. The
  script checks these counts and rejects changed sources or image dependencies.
- The NASA [Kennedy launch-site reference](https://asd.gsfc.nasa.gov/archive/sm3b/launch-info/kennedy-part1.html)
  gives **105.7 m** from the pad base to the fixed service structure's lightning
  mast. The script uniformly normalizes the complete model's vertical bounds to
  that height. This is an explicitly approximate adaptation of an artistic model,
  not a surveyed reconstruction or verification of every feature's dimensions.

NASA's [repository README](https://github.com/nasa/NASA-3D-Resources/blob/11ebb4ee043715aefbba6aeec8a61746fad67fa7/README.md)
describes the collection as free and without copyright. Retain the named credit
and [NASA usage guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/).
Identify the asset as **historical LC-39 gantry infrastructure**; it is not a
current SpaceX LC-39A reconstruction, Starbase tower, or SLS mobile launcher.

## Artifacts and Unity contract

| Output | Purpose |
| --- | --- |
| `NasaGantry.blend` | Editable VM-only source archive plus three normalized runtime meshes |
| `NasaGantry.obj` | Triangulated, normal-bearing, Y-up model; generated integration input |
| `NasaGantry.obj.gz` | Deterministic gzip of the OBJ for small bundled distribution |
| `NasaGantry.mtl` | Original three constant material colors/material parameters |
| `NasaGantry.json` | Source pin, hashes, transformation, geometry/material metadata and validation results |
| `NasaGantry-preview.png` | Optional Workbench image with historical-model attribution stamp |

There are exactly three exported objects named `NASA_Gantry_Material_00` through
`NASA_Gantry_Material_02`, matching their material names. Runtime geometry is
merged only by the source materials. No vertices are decimated, no disconnected
detail is discarded, and no new model geometry or textures are generated.

Blender coordinates use meters with Z up. The horizontal bounding-box center is
translated to X=Y=0 and the lowest point to Z=0. OBJ maps `(x,y,z)` to `(x,z,-y)`,
so its up axis is Y and its minimum Y is zero. The common pivot is **not** a
surveyed rocket mount: the Unity placement owner must inspect the imported model
and choose the rocket's clear launch position separately.

The importer consumes camelCase fields `compressedSha256`, `objSha256`,
`objBytes`, `heightMeters`, `upAxis`, `baseY`, `sourceSha256`, `sourceUrl`,
`attribution`, `materialNames`, `materials` and `bounds`. Each material record
contains `name`, `sourceName`, `baseColor` (linear RGBA), `metallic` and
`roughness`. Bounds contain `minimum` and `maximum` arrays in OBJ meters.

The script validates actual exported OBJ names, triangles, normals and bounds
before declaring preparation complete. This is not Unity import/render
validation. The manifest explicitly starts `unityImportTested: false`.

Copy only the reviewed gzip, JSON, MTL and required metadata into the source
asset bundle. Keep the editable blend in the authoring VM. The importer creates
the decompressed OBJ and prefab as generated artifacts. Establish an explicit
small-file Git LFS exception for the gzip if necessary before publishing; this
script changes no repository Git attributes or Unity files.

## Handoff status

The preparation script was authored without executing Blender, Python, Unity or
tests locally. Conversion, optional preview rendering, geometric placement and
Unity import remain VM qualification steps. Preserve the original downloaded
GLB alongside the editable blend for source recovery.

## Integrated VM check

Unity 6000.6.9f1 imported the normalized source as a 105.700 m mesh with three
submeshes and 144,945 imported vertices. Unity's OBJ importer requires explicit
`g` groups; the importer verifies the original compressed and expanded hashes,
then rewrites only object/group records in the generated derivative. Coordinates,
normals, UVs and triangle records remain unchanged. Both source and staged bounds
are checked, and the combined mesh must retain all 172,602 source triangles.

The macOS packaged review passed structure presence, height/base, map/reset
persistence, and absence at Starbase. Screenshot inspection confirmed the
historical model beside SLS; its offset is illustrative, not a survey alignment.
