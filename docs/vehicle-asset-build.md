# Vehicle asset build handoff

The repository contains source-faithful runtime exports, textures, preparation scripts and provenance. Both vehicle prefabs and a macOS ARM packaged flight were tested; Windows remains untested. Use the project's qualified Blender version, work in a single-owner workspace, and preserve the unmodified source archives and editable `.blend` files outside Unity's automatic importer. The source archives are retained in the project VM; do not commit large binaries until Git LFS rules are established. Never put credentials, signed download links, or private machine paths in Git.

## Starship V3

1. Obtain Clarence365's original Blender download and accompanying textures through the [Sketchfab model page](https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2). Sign-in is required. Preserve the original ZIP and extracted files together. The inspected ZIP is 66,661,373 bytes and SHA-256 `97c21ff7836b607bac698840d647b5d04ebb185afe00cbee218725be7e610699`; `source/Starship Block 3 V4.blend` is SHA-256 `5a8003c070f9c0211f622d11c1fab421e04ab4c1f3787c1d5d030b87df7195f6`. Verify hashes before use; investigate a changed revision rather than bypassing the script's check. The model is [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/): credit Clarence365, link the model and license, and state the actual adaptations.
2. From the repository root, use placeholder paths for the extracted blend, texture directory and output directory:

   ```sh
   blender --disable-autoexec --background --python AssetsSource/prepare_starship_v3.py -- SOURCE.blend OUTPUT_DIR --texture-dir TEXTURE_DIR
   python3 AssetsSource/split_vehicle_obj.py OUTPUT_DIR
   ```

   The script checks the source hash, relinks supplied textures, measures and normalizes the mesh, and bakes PBR textures. It writes the three manifest-listed `ESP-Starship-Booster`, `ESP-Starship-BoosterDetails` and `ESP-Starship-Ship` OBJ/MTL pairs, `Textures/`, `ESP-Starship-import.json`, an editable `ESP-Starship.blend`, and a preview. `--no-bake` is diagnostic only and does not produce a shipping export; `--no-render` skips the preview. The baked model, materials and visual details need inspection.
3. Place the OBJ, MTL, `Textures/`, and manifest together under `UnityProject/Assets/ESP/Art/Vehicles/Starship/`, preserving relative paths. The transport splitter creates ten `ESP-Starship-Part-NNN.obj` files and updates `model_files`. It verifies exact face attributes; no geometry is simplified. Copy those files and the three shared MTL files, omitting the larger intermediate stage OBJs. The importer validates the manifest-listed part names. Generate/retain Unity `.meta` files with their assets, inspect material maps and model orientation, and run the project editor's `ImportDetailedVehicles.BuildStarship` under the single-editor ownership rule. Generated prefabs, materials and combined meshes are reproducible build outputs excluded from Git; source exports, textures, manifests and their `.meta` files are committed. Use ESP → Assets → Rebuild downloaded vehicle prefabs in the editor, or BuildMission.Build for the packaged mission.

## NASA SLS Block 1

1. Download the original STL from [NASA's model page](https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/) and preserve it in the VM. The inspected 13,721,684-byte binary file has SHA-256 `d3af75412a54f1d15d644cce56e895fe6813452b49dc0558b4b2ddfb484844ac`; the preparation script rejects a different source. NASA model data and branding have distinct terms; consult [NASA's media guidelines](https://www.nasa.gov/nasa-brand-center/images-and-media/) and `docs/model-acquisition.md` before distribution.
2. Adapt the original printable mesh, then create the baseline markings without NASA logos:

   ```sh
   blender --disable-autoexec --background --python AssetsSource/prepare_nasa_sls.py -- SOURCE.stl SLS_OUTPUT_DIR
   blender --disable-autoexec --background --python AssetsSource/apply_sls_decals.py -- SLS_OUTPUT_DIR/SLS-Block1.blend ARTWORK_DIR LIVERY_OUTPUT_DIR
   ```

   `ARTWORK_DIR` is a required positional argument; the default no-logo run does not read the optional NASA logo artwork. The first script writes the editable `SLS-Block1.blend`, OBJ/MTL, import JSON and preview; the second writes separately named `SLS-Block1-LiveryReview` OBJ/MTL and blend, `SLS-Livery-Review-Atlas.png`, manifest and review renders. Both accept `--no-render`. The optional `--include-nasa-logos` creates a private review only and does not establish publication clearance. Preserve the source and adapted blends; do not treat photo-estimated markings as survey data.
3. The current importer expects `UnityProject/Assets/ESP/Art/Vehicles/SLS/SLS-Block1.obj`; the livery export has a different name, so explicitly reconcile the OBJ/MTL references and atlas with the importer before use. Preserve the atlas and UVs and use an alpha-clipped URP material as described in `docs/sls-decals.md`. Run `ImportDetailedVehicles.BuildSls` in the owned Unity editor only after the expected paths are satisfied; retain generated `.meta` files.

For both vehicles, Blender output and existing script paths do not establish a successful Unity import or packaged build. Verify textures, transforms, nozzle alignment, stage separation, actual performance, licensing/credits, and target-platform packaging before marking either runtime asset ready.
