# KSP reuse decisions

Reviewed November 14, 2026. A CKAN/SpaceDock license label is a discovery aid;
the exact upstream revision and individual files determine what can be reused.
ESP's original-code license does not replace third-party terms.

## Selected for integration

- **SmokeScreen**, revision `245afb49063f48a8beb4289986aaccc0446700ad`:
  BSD-2-Clause emission accumulation and sub-frame placement techniques from
  `PersistentKSPShurikenEmitter.cs`. ESP adapts a small scheduling helper for an
  original procedural SLS smoke trail. No KSP assemblies, stock textures or plume
  assets are copied. Preserve the source's 2019 Sarbian notice and bundled notice.
  [Source](https://github.com/sarbian/SmokeScreen/blob/245afb49063f48a8beb4289986aaccc0446700ad/PersistentKSPShurikenEmitter.cs),
  [upstream license](https://github.com/sarbian/SmokeScreen/blob/245afb49063f48a8beb4289986aaccc0446700ad/Licence.txt).
- **NASA Gantry**, NASA-3D-Resources revision
  `11ebb4ee043715aefbba6aeec8a61746fad67fa7`: direct historical LC-39 model,
  credited to NASA / Michael D. Carbajal. This is a direct NASA alternative to
  the KSP-specific pad conversion, not a KSP asset. Preserve its provenance and
  [NASA use guidance](https://www.nasa.gov/nasa-brand-center/images-and-media/).
  [Catalog](https://science.nasa.gov/3d-resources/gantry/),
  [pinned repository](https://github.com/nasa/NASA-3D-Resources/tree/11ebb4ee043715aefbba6aeec8a61746fad67fa7/3D%20Models/Gantry).

- **Waterfall noise textures**, revision `c232b6722496ec991fa0c6fbd87b0ad694b8eb49`: two 256×256 Raptor noise images, converted from DDS to PNG in Blender on the VM. CC BY-NC-SA 4.0, credited to Chris Adderley (Nertea) and Waterfall contributors. Original URP shader samples these textures; no KSP shaders, assemblies or sounds are imported. See [texture provenance and integration](waterfall-plume-textures.md).

## Useful candidates, not bundled

| Candidate | Verified constraint and next step |
| --- | --- |
| Starship Launch Expansion | Current models/textures are all rights reserved despite an MIT SpaceDock label. Historical revision `6cc33e81c5bbf04996368c91358ac813f2323116` has a full MIT license, followed by a license-only change. Its Base/Core/Top tower files and textures total 30,498,487 bytes. Only that exact old subset is a candidate; preserve its MIT notice and SAMCG14 credit. No Mechazilla model exists in that snapshot. `.mu` conversion, assembly and dimensions remain unqualified. |
| CanaveralPads LC-39 | Revision `f819d950ee83914a08d031b34fc117402d5bf211`, `pads/kLC39`: six files, 56,321,762 bytes. README credits Katniss for LC-39 and Aviation365 for configs, under CC BY-NC-SA 4.0. User accepts those restrictions; event-rule review and embedded binary-material provenance remain pending. Do not mix OSSNTR/Tundra extras. |
| Waterfall full effects/templates | Only the two noise images above are bundled. Remaining effects require a URP adaptation; sounds need separate provenance review. No complete pack or KSP shader includes imported. |
| RealPlume | Mixed per-artist terms and an explicit redistribution-permission request. Do not import the complete effects pack. No files imported. |
| Parallax / ROEngines | No-derivatives restrictions make converted/adapted runtime assets unsuitable without additional permission. No files imported. |
| Kerbal-Konstructs | Plugin and static-art licenses differ. An MIT plugin does not grant rights to all bundled scenery. No files imported. |

Historical SLE evidence:
[MIT snapshot](https://github.com/SAMCG14/StarshipLaunchExpansion/tree/6cc33e81c5bbf04996368c91358ac813f2323116),
[license-only change](https://github.com/SAMCG14/StarshipLaunchExpansion/commit/1f5a63b9e247d8d230528a8600eccf6a0eec55a4),
[current license](https://github.com/SAMCG14/StarshipLaunchExpansion/blob/Main/LICENSE).
The historical author describes the models as not 1:1; the RO patch's 1.6 scale
is a starting point for inspection, not proof of accurate current Starbase geometry.

KSP stock/DLC assets, generated mod caches, ambiguous local files and assets with
unresolved permissions are excluded. Free distribution alone does not settle
noncommercial, share-alike, promotional or contest-use requirements.
