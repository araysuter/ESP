# Rocket asset source

`build_rocket.py` generates an original, fictional two-stage ESP placeholder rocket. Its geometry is procedural and uses no external models, textures, logos, or trademarks. It is not a dimensionally accurate representation of a historical vehicle. The export contains no materials; Unity assigns its own URP materials.

## Geometry and coordinate conventions

Blender uses metric units at one unit per meter, with +Z up. The assembly reference origin is at the center of the booster base (0, 0, 0), not at the vehicle's center of mass. The script creates separate objects whose individual origins are at their primitive centers; the OBJ is exported without applying object locations or rotations.

| Part | Dimensions and placement in Blender coordinates (meters) |
| --- | --- |
| Booster | 20 m tall, 4 m diameter; z = 0 to 20 |
| Interstage | 1.2 m tall, 4 m diameter; z = 20 to 21.2 |
| Upper stage | 10 m tall, 4 m diameter; z = 21.2 to 31.2 |
| Nose | 6 m tall, tapering from 4 m diameter to a point; z = 31.2 to 37.2 |
| Five engines | 1.2 m tall, radii 0.55 m at the bottom and 0.32 m at the top; centers on a 1.15 m radius circle, z = -1.2 to 0 |
| Four fins | Each 2 × 0.12 × 4 m before rotation, centered at z = 3 m on a 2.1 m radius circle; radially oriented |

The assembly spans 38.4 m vertically, including engines. Fins reach 3.1 m from the centerline along their radial axes; the cylindrical body is 4 m in diameter. These are visual placeholder dimensions, not mass or aerodynamic specifications.

OBJ export uses **-Z forward, +Y up**, with object groups and triangulated meshes. The Unity integration uses a rocket-base pivot; callers must account for any offset from their simulated center of mass. See [the integration handoff](../docs/assets-integration.md) for the presentation boundary and Unity import process.

## Regeneration

Using the qualified Blender version recorded in the integration handoff, run from the repository root:

```sh
blender --background --python AssetsSource/build_rocket.py -- <export-directory>
```

The script generates `ESP-Rocket.blend` and `ESP-Rocket.obj` in the chosen directory. Preserve the editable `.blend` in the authoring VM, outside Unity's `Assets` directory; copy the OBJ into Unity as described in the integration handoff. Regeneration clears the Blender scene before creating the rocket, so run it in a dedicated background process rather than an open authoring session.
