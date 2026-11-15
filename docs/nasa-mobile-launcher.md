# NASA mobile launcher for the Kennedy scene

The Kennedy scene uses a simplified visual adaptation of NASA / Samuel H.
Neblett's [Mobile Launcher model](https://science.nasa.gov/3d-resources/mobile-launcher/).
The original file, source URL, SHA-256, provenance, and NASA usage guidance
are recorded in [the source archive](../AssetsSource/ThirdParty/NasaMobileLauncher/README.md).
NASA's model page does not state its exact era or scale. Its broad tower and
base silhouette resembles NASA photographs of ML1, but the simplified model
does not include all of the real launcher's modern umbilicals and equipment.
It is not a survey or detailed current reconstruction.

## Preparation

In the qualified Blender VM, run `AssetsSource/prepare_nasa_mobile_launcher.py`
with the source GLB and an output directory after `--`. It verifies the source
hash and topology, imports both materials and textures, applies the imported node
scale, and saves an editable `.blend` along with a static FBX and two PNG
textures. No animation or game physics is taken from the source model.

The model is translated to a centered, floor-level pivot and uniformly scaled
to 121.92 m (400 ft) overall height, matching NASA's approximate above-ground
height for [Mobile Launcher 1](https://www.nasa.gov/wp-content/uploads/2021/04/final_-_mobile_launcher_1_fact_sheet.pdf).
This is an approximation, not a verified original model scale. The FBX is Y-up
for Unity; the Unity importer applies a 0.01 scale to compensate for the tested
FBX unit conversion and checks the resulting model height. The original GLB is preserved, so the editable source and conversion
can be reproduced. The body and double-sided translucent railings retain their
respective NASA-derived textures.

The real launcher deck is approximately 47 ft above ground. The uniformly
normalized source model's deck is approximately 14.7 m above its feet. The
local visual places the vehicle origin above the modeled deck and shifts the
model to place the rocket near the flame opening; these are rendering offsets
only. The authoritative simulator continues to use the selected site's existing
geodetic state and does not model the launcher's mass or collision geometry.
Launcher's position, orientation and deck alignment are approximate. SLS launches
from LC-39B in reality; the sandbox permits choosing other vehicles at this
representative site.

## Unity verification

On November 14, the macOS ARM VM's packaged development build imported the
121.92 m visual and passed the opt-in `-espMobileLauncherSmoke` check for both
textured materials, liftoff, reset, and absence at Starbase. A daylight-override
screenshot was reviewed after positioning the boosters over the model's opening.
The source FBX importer uses a 0.01 scale, and checks model height and floor.
An assisted SLS run from LC-39B at 1280×720 also reached verified orbit at
mission time 1,109.78 s, rendered a closed orbit on the map, and reset after a
site change. The run used the standard-atmosphere fallback because live weather
was unavailable. Time acceleration used the game's fixed-step simulation.
These checks are not evidence of survey accuracy. Other target systems remain
untested until independently built and run.
