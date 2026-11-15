# Waterfall nozzle-exhaust texture subset

ESP directly reuses two small noise images from
[Waterfall](https://github.com/KSPModStewards/Waterfall), pinned revision
`c232b6722496ec991fa0c6fbd87b0ad694b8eb49`:

- `GameData/Waterfall/FX/fx-noise-1.dds`
- `GameData/Waterfall/FX/fx-noise-2.dds`

Each source is 256 × 256, DXT5, 87,536 bytes. The unmodified originals,
SHA-256 values, exact upstream paths and license are preserved under
`AssetsSource/ThirdParty/Waterfall/`. The original texture commit
`f861ceae06d1e9adcfe65e15eabc6aa32cdf994c` credits Chris Adderley (Nertea).
These are the noise textures referenced by Waterfall's lower and upper Raptor
templates; no authored engine templates, KSP model files or sounds are imported.

## Terms and attribution

The pinned upstream license covers this image subset under
[CC BY-NC-SA 4.0](https://creativecommons.org/licenses/by-nc-sa/4.0/).
Credit Chris Adderley (Nertea) and Waterfall contributors; retain the upstream
license/link and identify DDS-to-PNG conversion. Converted images retain
CC BY-NC-SA 4.0; their license is separate from ESP's original code license.
`Assets/StreamingAssets/Waterfall-NOTICE.txt` includes the provenance, credit,
change notice and complete upstream license notice in the packaged player.

The existing ESP URP shader samples these images with original texture lookup
and modulation code. No Waterfall shader math is copied. In particular, ESP
does not import its built-in surface shaders, `SquadCore/LightingKSP.cginc`,
KSP assemblies, compiled AssetBundles or the complete effects pack.

## VM conversion and build wiring

Conversion ran in Blender 5.2.3 LTS inside the qualified VM. Both DDS files
loaded as 256 × 256 RGBA and were saved as PNG without resizing. Reproduce
with Blender Python (run Blender with `--background --python`):

```python
from pathlib import Path
import bpy
source = Path('AssetsSource/ThirdParty/Waterfall').resolve()
target = Path('UnityProject/Assets/ESP/Resources/Waterfall').resolve()
target.mkdir(parents=True, exist_ok=True)
for name in ('fx-noise-1', 'fx-noise-2'):
    image = bpy.data.images.load(str(source / (name + '.dds')))
    image.filepath_raw = str(target / (name + '.png'))
    image.file_format = 'PNG'
    image.save()
```

The PNG `.meta` files set linear sampling, mipmaps, bilinear filtering and
repeat wrapping. Keep those GUIDs and commit the resulting PNG images with the
supplied metadata. Unity's documented editor texture formats include PNG;
DDS is deliberately retained as source outside `Assets` rather than assumed
to import correctly on Metal.

`ImportDetailedVehicles` already assigns the serialized `Plume.shader` reference
when it rebuilds SLS/Starship prefabs. `RocketPlume.Awake` constructs the runtime
material and fills its two public serialized texture slots from
`Resources.Load<Texture2D>("Waterfall/fx-noise-1")` and `fx-noise-2` when not
explicitly assigned. Unity includes Resources assets in the player, so
`BuildMission` needs no edits. At the default 0.35 strength, images vary only
the diffuse envelope emission by at most approximately ±16%; hot cores, nozzle
anchors, pressure expansion, bounds, throttle and flight physics are unchanged.
Missing images log one warning and leave the original procedural fallback.

## Verification handoff

No conversion, code build, shader compilation, rendering or QA was run on the
contributor's host. VM conversion is reported above; shader/player verification
remains pending. On the qualified VM, refresh imports and inspect
the two textures as 256 × 256 Texture2D assets with sRGB disabled. Check
`RocketPlume.UsesWaterfallTextures == true` in both editor and packaged player;
the fallback warning must be absent. Compare actual Starship lower/upper-stage
burns at equal camera/exposure, sea-level and vacuum, including throttle zero,
fuel cutoff, staging and reset. The visible change should be subtle turbulent
breakup, retaining the existing expanding translucent vacuum fan and hot cores.
