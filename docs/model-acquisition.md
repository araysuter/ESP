# External rocket asset acquisition

## Selected Starship V3

The selected model is Clarence365’s [SpaceX Starship Block 3](https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2), licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). Credit Clarence365, link the source and license, and identify ESP adaptations. Download the original Blender ZIP through the author’s download control; Sketchfab sign-in is required. No private download URL or credential belongs in Git.

Archive SHA256: `97c21ff7836b607bac698840d647b5d04ebb185afe00cbee218725be7e610699` (66,661,373 bytes). Its Blender file SHA256 is `5a8003c070f9c0211f622d11c1fab421e04ab4c1f3787c1d5d030b87df7195f6`. Keep the accompanying textures. Source inspection found two stages, three outward grid fins, four ship flaps and separate inner engines; some outer booster engines are joined into the main mesh. Source unit scale requires normalization, and exported texture names require relinking. This is an artist’s V3 representation, not a measured hardware survey.

The source archive and editable files are preserved in the project VM. This checkpoint contains inspection/adaptation code; the final game export and in-engine validation are pending. See `starship-v3-import-review.md`.

The earlier Stephen Melton CC0 printing model was inspected and rejected because it does not represent V3. Its geometry must not be included in the final catalog. User-selected Ijsz23 full-stack model was view-only; no viewer geometry was extracted.

## NASA SLS Block 1

Use [NASA’s SLS Block 1 STL](https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/). The inspected binary STL has 274,432 triangles and 13,721,684 bytes. `AssetsSource/prepare_nasa_sls.py` preserves the downloaded flight geometry, removes the printing stand, assigns materials and normalizes it to 98.3 m. The original and adapted Blender source remain in the VM.

`AssetsSource/apply_sls_decals.py` adds photo-referenced major paint and marking overlays. Its revised 58-marking local review rendered successfully and verified unchanged original vertex positions/topology. NASA logo publication is a separate unresolved branding requirement; the default export omits that optional artwork. See `sls-decals.md` for sources and limits. The 54-marking baseline imported into Unity at 98.30 m height; packaged visual inspection remains pending.
