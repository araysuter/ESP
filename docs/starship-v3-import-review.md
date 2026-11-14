# Starship V3 source-faithful import review

Status: reference review prepared November 14, 2026. The Clarence365 archive has been downloaded and its member listing inspected; Blender scene inspection is pending. No geometry, materials, fin poses, scale, polygon count, or Unity performance are approved by this document. The rejected Stephen Melton model must not be refined or shipped as the selected V3 vehicle.

## Selected source and attribution

Candidate: [SpaceX Starship Block 3 by Clarence365](https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2). Coordinator reports a downloadable complete stack, approximately 913,000 faces and CC BY 4.0. Direct page access returned HTTP 403 during this review; preserve the coordinator's successful listing/license evidence with the actual archive before marking acquisition verified.

The downloaded archive is 66,661,373 bytes, SHA-256 `97c21ff7836b607bac698840d647b5d04ebb185afe00cbee218725be7e610699` (coordinator-verified). Its contents are `source/Starship Block 3 V4.blend` (65,038,916 bytes) and five textures: `roughness1.png`, `starshipxlogov2.png`, `RBXX_Logo_V3.png`, `tiletexture.jpeg`, and `Starship_S45.png`. Preserve that unmodified archive and supplied textures; record download date, filenames, model ID, author, license and every adaptation. Check large-binary LFS rules before adding the archive or exports. Do not substitute an unrelated similarly named model or an undocumented optimized copy.

Suggested credit, after verification: “SpaceX Starship Block 3 by Clarence365, via Sketchfab, licensed under CC BY 4.0. Adapted for ESP: [actual changes].” Link the model, [author](https://sketchfab.com/clarence365), and [license](https://creativecommons.org/licenses/by/4.0/) in bundled credits and repository provenance. CC BY requires appropriate credit, a license link, and disclosure of modifications; do not imply endorsement. Reference photographs are for comparison and are not automatically licensed game textures.

## Primary geometry references

- SpaceX, [Introducing Starship V3](https://www.spacex.com/updates), May 12, 2026; the indexed [updates mirror](https://new.spacex.com/updates) exposes the article text. SpaceX identifies three grid fins instead of four, each 50% larger, lower mounting, revised clocking and integrated catch features. The hot-stage structure is integrated rather than the earlier expendable protective ring. These are version-identification checks, not instructions to rebuild the downloaded vehicle.
- SpaceX, [Starship's twelfth flight test](https://www.spacex.com/launches/starship-flight-12): identifies the first flight of the V3 ship, booster and Raptor 3 engines. Use that generation as the reference; do not mix earlier four-fin booster photographs into this adaptation.
- SpaceX, [May 11 launch-rehearsal photographs](https://x.com/SpaceX/status/2053929135936864393). X returned HTTP 403 here; the SpaceX-authored [full-stack photograph](https://cdn.mos.cms.futurecdn.net/iAHzx5idBfCE7bcvxncqJ3.jpg) and [overhead photograph](https://cdn.mos.cms.futurecdn.net/QHqCTjB2xbSsBYeR88oHcH.jpg) were visually inspected through their credited reproduction in [Space.com's photograph gallery](https://www.space.com/space-exploration/launches-spacecraft/spacex-fuels-up-starship-v3-megarocket-for-1st-time-ahead-of-crucial-test-flight-photos). A [stage-interface close-up](https://cdn.mos.cms.futurecdn.net/ZTDA5UAfYyWmAvjfsTKoc8.jpg) is also linked there. The photos are primary visual evidence; the gallery supplies their credit/date, not engineering dimensions.

## Grid-fin launch pose: resolved visually

The pad photographs show the booster fins **extended radially**, not folded lengthwise against the barrel. In the overhead view the open lattice is visible across broad, roughly transverse fin faces; the cells admit airflow approximately along the vehicle axis. In the side view their outward silhouette remains visible below the stage interface. This is an image-based orientation inference, not a measured actuator angle.

Do not interpret “folded for launch” as a Falcon 9-style stow operation. Preserve the source's three outward fins if they match these photos. A grid-fin steering rotation about its outward shaft is different from folding the whole fin against the body. Also distinguish the booster's lattice fins from the ship's two forward and two aft solid flaps.

Do not assume equal 120-degree clocking, identical apparent height, or exact steering angles from one perspective photograph. Preserve source mounting/clocking and compare multiple views. If a downloaded fin is actually posed incorrectly, rotate the existing fin around its actual shaft pivot, record the change, and compare front, side and overhead renders; do not remodel its lattice from primitives.

## Archive inspection and adaptation sequence

1. Inventory the original scene graph, units, transforms, materials, texture maps and animations before edits. Render the untouched source from front, rear, side, underside and above. Record true triangle count after triangulation rather than treating listing faces as triangles.
2. Identify the ship and booster subtrees, three grid fins, four ship flaps, stage interface and engine groups. Count physically modeled engine bells; inspect hidden/inset meshes before adding any presumed missing engines. Check underside views against source references. No replacement rocket geometry is authorized.
3. Establish metre scale from the cylindrical barrel and source dimensions, excluding fin span and appendages. Apply one uniform scale to the complete stack and record the resulting overall and stage dimensions. Do not independently stretch stages to fit an older prefab.
4. Preserve editable source, hierarchy, UVs, normal maps and material distinctions. Translate supplied materials to URP while retaining their appearance. Check glTF metallic/roughness channel conversion, normal-map direction and texture color space explicitly. Keep steel reflections, dark heatshield, welds, engines and decals distinct; avoid blanket glossy-gray material replacement.
5. Create separate stage parents without moving geometry in world space. Preserve full-stack alignment and identify the genuine separation plane. Keep hot-stage structure with its source/correct stage; do not retain an older V1/V2 ring simply because the previous importer expected one.
6. Add only non-rendered integration anchors at the actual engine exit centers, with local -Y following exhaust. Measure each nozzle radius and derive plume anchors from the imported geometry, replacing old hard-coded Starship offsets. Use the existing `RocketPlume.stageIndex` contract. The art model does not set propulsion or mass.
7. Optimize only after a baseline render/profile. Retain the source as LOD0 if practical; reduce hidden/internal detail first. Protect silhouette, thin grid-fin bars, flap edges, tile boundaries and engine bells. Compare the same camera before and after any reduction. Proposed performance budgets are targets, not measured results.

## Unity acceptance evidence still required

- Named import revision, source hash, exact export units/axes and Blender version; `.meta` files included.
- Side-by-side source and Unity images under neutral lighting, including underside and fin close-up. No missing textures, inverted normals, tiled-steel substitution, floating fins or clipped nozzles.
- Pad, first powered ascent, stage separation and upper-stage views in the actual player. Booster disappears/separates once; the ship retains its source proportions and correct engines. No source pose or scale change affects authoritative flight state.
- Exhaust starts at each measured bell lip, shuts down with its stage, and matches nozzle width. Check clustered sea-level exhaust and expanded high-altitude exhaust separately.
- Actual packaged frame-time and memory observations before claiming the 913k-face candidate meets the target. Offline textures and attribution must survive packaging.

Ownership: Astra visual review owns this Starship adaptation when the archive arrives. The coordinator owns the shared Unity editor and VM. This review does not transfer editor ownership or authorize concurrent binary editing.

## Inspection command

`AssetsSource/inspect_starship_v3.py` records all source objects, hierarchy, matrices, material node graphs, UVs, modifiers, animation/driver inventory, images, source scene/frame/units, evaluated triangle counts and bounds. It preserves source geometry and saved pose, repairs only supplied texture paths by basename, and renders a separate neutral inspection scene. It does not save over the original blend or generate rocket geometry.

```sh
blender --disable-autoexec --background --python AssetsSource/inspect_starship_v3.py -- \
  SOURCE.blend OUTPUT_DIR --texture-dir TEXTURES --views three-quarter,above,underside
```

Use `--no-render` for inventory alone. Camera fitting infers the longest source axis; confirm this against the inventory if a source floor, prototypes or exploded parts are present. Python syntax was checked locally; Blender execution remains the coordinator's next verification step.

## Measured adaptation checkpoint

The pinned Blender source passed texture relinking and actual nozzle-lip extraction: 33 Super Heavy and six Starship engines. Uniform scaling to the measured 9 m tank barrel yields a 124.1863 m stack; the model is not stretched to a nominal published height. Three outward grid fins and the existing hot-stage geometry are preserved. Runtime PBR baking and Unity validation remain pending.
