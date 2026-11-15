# NASA SLS Block 1 adaptation

The rocket mesh comes from [NASA's SLS Block 1 download](https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/). `AssetsSource/prepare_nasa_sls.py` adapts that downloaded STL; it does not construct replacement rocket parts. The older procedural `build_sls.py` is not the source of this adaptation.

## Provenance and changes

Original download: [Space Launch System (SLS) Block 1.stl](https://assets.science.nasa.gov/content/dam/science/cds/3d/resources/printable/space-launch-system-(sls)-block-1/Space%20Launch%20System%20(SLS)%20Block%201.stl).

Pinned SHA-256: `d3af75412a54f1d15d644cce56e895fe6813452b49dc0558b4b2ddfb484844ac`. Original file: 13,721,684 bytes, 274,432 triangles. NASA's page identifies this as its SLS Block 1 resource; it does not provide a model-specific author or separate license. Credit NASA and consult [NASA media usage guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/), including the separate restrictions on NASA identifiers and third-party material. Do not represent the adaptation as NASA endorsement or a newly authored rocket mesh.

Suggested credit: **SLS Block 1 geometry: NASA 3D Resources. Adaptation for ESP: connected-part separation, print-stand exclusion, uniform scale, smooth shading and original visualization materials.** Link the source and NASA usage guidance alongside the credit.

The STL contains no materials, textures or UV coordinates. Our orange foam, white exterior and dark nozzle materials are visualization choices, not materials supplied by NASA. The original adaptation did not invent additional rocket geometry; the later Unity-only ICPS approximation is described below. A studio ground plane and lights are preview assets only and never enter the rocket OBJ.

## Direct geometry inspection

A standard-Python binary STL reader inspected coordinates before authoring the adaptation. Duplicate STL vertices use five-decimal source-coordinate keys solely for connectivity; the retained coordinate is the first original float value. The result has 137,360 welded vertices and 37 connected components.

| Component | Original triangles | Original Z range | Treatment |
| --- | ---: | ---: | --- |
| Main core | 101,648 | 11.9016–133.0658 | Orange foam; preserve existing ribbing |
| Left SRB | 61,248 | 5.0271–111.6155 | White exterior; dark existing nozzle region |
| Right SRB | 60,864 | 5.0271–111.6155 | White exterior; dark existing nozzle region |
| Main upper vehicle | 26,752 | 133.0988–198.7409 | White exterior |
| Four disconnected core nozzles | 1,532 each | 4.9920–12.3936 | Dark engine material |
| Small pipes, brackets and upper details | Remaining flight components | Various | Preserve; assign by actual component bounds |
| Rectangular printing stand | 12 | approximately 0–5.1870 | Archive only; exclude from flight export |

The original complete bounds are X −22.7652 to 22.5318, Y −11.4868 to 11.4868, Z approximately 0 to 198.7409. Those bounds include the stand. The lowest actual rocket nozzle is Z 4.99203. After removing the separate stand, the rocket is uniformly scaled by **0.5073578604569523 meters per source unit**, making the chosen game height **98.3 m**. This is a visual normalization to the project's SLS height, not a claim that the STL supplies physical units or engineering dimensions. Source asymmetries are retained.

All **274,420 flight triangles** are retained. There is no blanket decimation, because this first game adaptation should preserve the supplied silhouette and details. Its actual Unity performance still requires measurement; optional later LOD work must be compared visually against this preserved source.

## Run and output

Using the project's pinned Blender 5.2.3 LTS:

```sh
blender --background --python AssetsSource/prepare_nasa_sls.py -- SOURCE.stl OUTPUT_DIR
```

The source file and output directory are explicit arguments; private machine paths are not embedded. Before Blender execution, inspect with ordinary Python:

```sh
python3 AssetsSource/prepare_nasa_sls.py SOURCE.stl OUTPUT_DIR --inspect
```

Outputs:

- `SLS-Block1.blend`: 36 adapted flight components, unscaled original source including printing stand in a hidden archive collection, materials, preview camera and studio. Preserve this editable file outside Unity's automatic blend importer.
- `SLS-Block1.obj` and `.mtl`: flight meshes only, meters, origin at the source centerline/lowest nozzle plane, Y up and −Z forward.
- `SLS-Block1-import.json`: source hash, bounds, component accounting, exact nozzle anchors and changes.
- `SLS-Block1-preview.png`: 900 × 1200 portrait studio render, three-quarter view and ground contact. `--no-render` skips only the PNG render.

The Blender model uses X right, Y depth, Z up. OBJ export maps Blender `(x,y,z)` to `(x,z,-y)`. All flight object origins share the bottom-center frame and all vertices are already in meters. Apply no additional scale in Unity. Import the OBJ/MTL into the SLS vehicle art folder, with its Unity `.meta` files, rather than importing the `.blend` and its studio/archive.

Names begin `CoreAssembly__`, `UpperAssembly__`, `LeftSRB__` or `RightSRB__`, matching `ImportDetailedVehicles.cs`. Material names match its existing URP palette. The importer creates independent SRB groups. The flight core burns the solids in parallel with the liquid core and jettisons them before core separation.

## Geometry-derived integration anchors

The manifest is authoritative for these adapted mesh positions. Unity/OBJ coordinates, in meters:

| Anchor | X | Y (height) | Z |
| --- | ---: | ---: | ---: |
| Core nozzle 1 | −2.348043 | 0 | −1.948683 |
| Core nozzle 2 | 2.301947 | 0 | −1.948683 |
| Core nozzle 3 | −2.348043 | 0 | 1.937664 |
| Core nozzle 4 | 2.301947 | 0 | 1.937664 |
| Left SRB nozzle | −6.994925 | 0.017793 | 0.175360 |
| Right SRB nozzle | 6.994925 | 0.017793 | 0.175360 |

The upper component begins at **64.995970 m**. This differs from the old procedural model's approximate 59 m separation. The supplied source has no separately modeled upper-stage engine component; do not describe an adapter-base plume anchor as a verified engine nozzle.

## Verification status

Source checksum, binary STL structure, connectivity, component grouping, triangle accounting, scale, nozzle positions, manifest generation and Python syntax have been checked locally. The coordinator ran the adaptation in the VM and supplied the rendered 900 × 1200 studio PNG; visual review confirms the sourced rocket silhouette, original surface detail, stage materials, clean print-stand exclusion and ground contact. Actual Unity import, exhaust alignment and packaged-game performance still require integration verification. The 274,420-triangle source is retained until profiling justifies LOD work.

## Unity ICPS presentation approximation

The source STL does not model the ICPS and its engine within the orange launch vehicle stage adapter (LVSA). NASA states the LVSA stays attached to the core during separation, while the ICPS continues with Orion; NASA lists the ICPS diameter as approximately 5.1 m ([SLS reference guide](https://www.nasa.gov/wp-content/uploads/2022/03/sls_reference_guide_2022_web.pdf), [ICPS description](https://www.nasa.gov/reference/icps/)). The Unity importer assigns the orange LVSA submesh to the core. It adds an original, deliberately simplified 4.8 m diameter cylinder from y=67.5 to 72.29 m and a tapered engine bell from y=65.5 to 67.5 m to the upper assembly, wholly inside the adapter's visible envelope on the pad. The cosmetic upper plume begins at the bell lip, y=65.5 m. These shapes and dimensions are visual approximations, not a measured ICPS, RL10, or verified engine exit; they do not affect mass, thrust, or staging physics. The source's abort tower remains fused to the upper mesh and does not jettison. The generated approximation and separation passed packaged Unity rendering review; see [validation](visual-polish-validation.md).
