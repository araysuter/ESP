# Starship ceramic heat-shield appearance

The Clarence365 V3 source assigns `Heatshield Tiles` to the windward hull and
four flaps. Its Principled shader feeds an image into metallic, which can make
those tiles reflect like metal. ESP intentionally adapts just these authored
faces to matte black ceramic: metallic zero, smoothness 0.18, and dark albedo
with the existing luminance variation. Exposed steel, engines, logos, geometry,
UVs and normal maps retain their original assignments.

`AssetsSource/correct_starship_heatshield.py` runs in background Blender after
`prepare_starship_v3.py`. Supply the saved runtime `.blend` (including its archived
original scene) and a fresh output directory. The script checks matching face
vertex membership, accounts for mirrored winding, and bakes exact source-material
masks onto the existing UVs. It emits ten replacement albedo/metallic maps, a
hash/provenance report and a separate editable blend. Copy the replacement PNGs
into the runtime texture directory; keep existing Unity metadata. This correction
must be reapplied after regenerating the original atlases.

The five-part conversion completed in the macOS VM with Blender 5.2.3. It checked
that non-tile pixels were unchanged before PNG encoding. Input/output hashes and
face/pixel counts are in `AssetsSource/heatshield-correction.json`. Unity explicitly
uses the packed map alpha for smoothness. Packaged visual review is pending.

Original model: [SpaceX Starship Block 3 by Clarence365](https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2),
[CC BY 4.0](https://creativecommons.org/licenses/by/4.0/). This material adaptation
does not claim an engineering-accurate tile specification.
