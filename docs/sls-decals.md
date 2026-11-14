# SLS Artemis I paint and major marking review

`AssetsSource/apply_sls_decals.py` adds a reviewable paint and decal layer to the NASA SLS Block 1 geometry produced by `prepare_nasa_sls.py`. It preserves the original 36 flight meshes, their vertex positions and topology; it checks that preservation before exporting. Thin, UV-mapped overlays are ray-projected onto the existing surfaces rather than replacing rocket parts.

This is a **major visible marking pass**, not a claim that every Artemis I marking is accurately surveyed. The source is a printable visualization model, and the photographs do not supply measured positions for every side of every component.

## Sources inspected before placement

Primary NASA references:

- [Artemis I on the pad, March 18, 2022](https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220318-ph-kls03_0079large.jpg): front layout of booster bands, checkerboards, vertical roll markings, nose targets, core targets and paint regions.
- [Aft/core and booster detail, March 16, 2022](https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220316-ph-geb01_0030large.jpg): white engine section, aft-skirt targets, booster boundary bands and existing piping. This is a detail view, not a complete rear orthographic survey.
- [Upper-stack detail, April 21, 2022](https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220421-ph-jbs01_0035large.jpg): orange LVSA, ICPS flag, fairing insignia and upper targets.
- [NASA's completed booster worm photograph](https://www.nasa.gov/wp-content/uploads/2023/03/artemis_i_boosters_with_nasa_worm.jpg), from [NASA's accompanying article](https://www.nasa.gov/image-article/nasa-worm-added-moon-rocket-boosters/): vertical worm orientation and location across the central booster segments.
- [NASA upper-stack illustration](https://www.nasa.gov/wp-content/uploads/2023/03/lower_case_usa_500h.jpg), from [NASA's SLS worm article](https://www.nasa.gov/image-article/nasa-worm-logo-sls-rocket/): flag and “United States” on the launch-abort shroud; worm on the crew-module adapter band; insignia on the service-module fairing.
- [NASA SLS Reference Guide, 2022](https://www.nasa.gov/wp-content/uploads/2022/03/sls-reference-guide-2022-v2-508-0.pdf), printed pages 15 and 25–26: Block 1 elevation, thermal protection colors and explanation of photogrammetric markings. The guide describes 0.6 × 3.3 m multi-checkered booster targets and distinguishes internal targets from external ones.

The downloaded PDF pages and the original photographs were visually inspected. Do not treat an early concept illustration as measured flight-hardware placement. Dimensions, heights and azimuths in the script are photo-based approximations, recorded per marking in its output manifest.

## Paint and marking choices

The lower core engine section becomes white; the launch-vehicle stage-adapter cone becomes orange foam. These are material changes only. The foam's darker visualization color is a presentation choice; solar exposure changes the actual foam appearance.

The baseline includes the prominent booster boundary lines, front checker strips, vertical roll marks, nose/low targets, a limited set of aft-skirt targets, core targets, the ICPS flag, and the launch-abort-shroud flag and “United States.” The U.S. flag has 13 stripes and 50 stars; lettering uses Blender's font as a typographic approximation. Front livery is not blindly mirrored onto the rear. The left booster's prominent nose-base band is not invented on the right booster.

The optional NASA artwork layer places the vertical worms on the **outboard** booster sides, the NASA insignia on the Orion service-module fairing, and a worm on the crew-module adapter band. These are Artemis I placements; later Artemis vehicles can have different logo orientations and additional markings.

There is no verified giant “USA” or “SLS” wordmark on the orange core in these references, so the script does not invent one. The visible national text is “United States.” ESA artwork, the LVSA mission emblem, tiny stencils, individual thermal-cover details and the complete set of internal/external photogrammetric targets remain incomplete. The manifest states these limits rather than calling this an exact full livery.

## Logo policy and official artwork

[NASA's general media guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/) permits factual educational/informational uses of its media and 3D data, including computer simulations, subject to its conditions. Its [brand guidelines](https://www.nasa.gov/nasa-brand-center/brand-guidelines/) separately restrict the NASA insignia and worm. The [video-game section of NASA's merchandise guidance](https://www.nasa.gov/nasa-brand-center/merchandise-approvals/) says:

> The NASA brand in a video game or virtual world must follow the NASA Brand Guidelines and go through a vetting process.

Thus NASA-source geometry does **not** by itself establish clearance to publish a game bearing NASA logos. No permission, endorsement, approval or contact with NASA is claimed. The script defaults to omitting NASA logos. `--include-nasa-logos` creates a local review version; it does not establish publication clearance. Keep the local logo review separate from the distributable baseline while the coordinator resolves that requirement. This is a specific source requirement, not a new approval rule inferred from hypothetical risk.

Use official artwork, not an imitation font or generated logo:

- `nasa-worm-guide.png`: [official NASA logotype graphic](https://www.nasa.gov/wp-content/uploads/2023/07/nasa-logotype-protectedspace.png). The script samples the supplied white glyph contour and applies the red logo color; it does not redraw the letters. The surrounding guide labels are excluded from the atlas.
- `nasa-meatball.png`: rasterization of [NASA's official SVG](https://www.nasa.gov/wp-content/themes/nasa/assets/images/nasa-logo.svg), preserving its artwork. The coordinator prepared the raster from that SVG for Blender's image loader.

The script records artwork sources and transformations in its JSON. Credit NASA as the source of the geometry and source media, identify ESP's adaptation, and avoid presenting the product as official NASA software.

## Execution and integration

```sh
# Baseline, no NASA identifier artwork required:
blender --disable-autoexec --background --python AssetsSource/apply_sls_decals.py -- SLS-Block1.blend ARTWORK_DIR OUTPUT_DIR

# Separate private review, requires both official artwork files above:
blender --disable-autoexec --background --python AssetsSource/apply_sls_decals.py -- SLS-Block1.blend ARTWORK_DIR REVIEW_OUTPUT_DIR --include-nasa-logos
```

Input must be the preserved NASA adaptation, not the former procedural SLS. `--no-render` skips the two PNG views. The script opens the input with source-file scripts disabled.

Outputs have distinct `LiveryReview` names to avoid automatically replacing approved game assets:

- Editable `.blend`, game-coordinate `.obj`/`.mtl`, packed atlas and standalone 1024 × 1024 RGBA PNG.
- Front and rear review PNGs using the existing studio.
- `SLS-Livery-Review.json` with every placement, reference, source-geometry check and remaining uncertainty.

All positions remain meters, with the existing common rocket origin. OBJ conversion remains Blender `(x,y,z)` to Unity `(x,z,-y)`. Decal object names preserve `CoreAssembly__`, `UpperAssembly__`, `LeftSRB__` and `RightSRB__` prefixes.

**Unity must retain the atlas and UVs.** The current plain-color vehicle palette must not replace `SLS Livery Review Atlas`. Use a URP/Lit material with atlas RGB as Base Map, alpha clipping enabled (starting cutoff 0.5), low metallic and moderate roughness. Include the texture and its `.meta` file. Existing stage mesh combination can combine the overlays by this shared material. Inspect front/back face orientation, logo/flag UV orientation, alpha edges, offsets and exhaust alignment in the actual Unity view before accepting the import.

## Verification status

Python syntax, reference/artwork files, Blender decal projection and both review renders are checked. The local logo review produced 57 marking objects and verified unchanged original vertex positions and topology. The standalone atlas exists; a relative-path export warning is being corrected before game integration. No claim is made yet that the revised material survives Unity import or that all photogrammetric markings are complete.

Coordinator verification: revised export produced 58 review markings (54 in the no-logo baseline), no missing-atlas warning, and unchanged original geometry. The baseline Unity prefab import succeeded at 98.30 m height. Packaged visual inspection remains pending.
