# Mission display finishing

`MissionFinishing.asset` is a serialized URP VolumeProfile containing only ACES
tonemapping and restrained highlight Bloom. BuildMission validates it and gives
the global scene Volume a shared-profile reference before saving/building the
scene. Existing URP assets already support HDR and reference post-process data.
The profile's script GUIDs come from the checked-in URP sample profile.

Bloom uses threshold 1.2, intensity 0.12, scatter 0.55, clamp 24, quarter-resolution
filtering, at most four iterations, and standard filtering. It has no dirt map.
ACES rolls off the Sun/exhaust HDR highlights; normal scene lighting intensity
is unchanged. No motion blur, depth of field, chromatic aberration, vignette,
exposure adaptation or additional custom full-screen pass is added. Existing
URP defaults have motion blur, depth of field and chromatic aberration disabled.

The final launch overlay applies finishing once to the complete flight stack;
the scaled Earth Base explicitly keeps post-processing off. The standalone map
applies it once. Inset cameras inherit the same Volume mask and enable the same
finishing: the vehicle inset is the final flight overlay, while the map inset is
a standalone Base. Their HDR intermediate is tone-mapped into the existing
384x216 ARGB32 display texture before UI composition. UI remains display-referred.

This presentation was checked in the development macOS player; see [packaged validation](visual-polish-validation.md). The integration owner must confirm
shader compilation/retention in a player, compare pad/cloud/night/orbit/map and
insets, and profile the packaged route. Reducing Bloom intensity to zero is the
first rollback if cost or glare obscures a shot; profile assets should remain
serialized. No claim of calibrated radiometry or target performance is made.

References: [Unity Bloom](https://docs.unity.cn/Packages/com.unity.render-pipelines.universal@17.0/manual/post-processing-bloom.html),
[Unity ACES tonemapping](https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@17.4/api/UnityEngine.Rendering.Universal.TonemappingMode.html).
