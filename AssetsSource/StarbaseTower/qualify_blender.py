"""Create isolated derived meshes and paired review renders; never edit inputs.

Invoke through run_blender_trial.py for a bounded fresh Blender process.
UNEXECUTED TRIAL: Blender API compatibility and output quality are unverified.
The output is a review candidate, not an approved Unity/runtime asset.
"""
import bpy
import bmesh
import hashlib
import json
import math
import sys
from pathlib import Path
from mathutils import Vector

if not bpy.app.background or bpy.data.filepath:
    raise ValueError("Run in a fresh background Blender process without a loaded .blend file")
if bpy.app.version != (5, 2, 3):
    raise ValueError("This unqualified trial requires the pinned Blender 5.2.3 version")
args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
if len(args) != 2:
    raise ValueError("Expected source module directory and NEW output directory")
source, output = (Path(value).resolve() for value in args)
if source == output or source in output.parents:
    raise ValueError("Use a separate output directory outside the source modules")
source_hashes = {}
for name in ("TowerLevel", "TowerTop", "CatchArms", "QuickDisconnect", "LaunchMount", "Foundation"):
    for suffix in (".obj", ".mtl"):
        path = source / (name + suffix)
        if not path.is_file():
            raise ValueError(f"Missing source module {name}{suffix}")
        source_hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
output.mkdir(parents=True, exist_ok=False)
bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.render.threads_mode = "FIXED"
scene.render.threads = 2
scene.render.engine = "BLENDER_WORKBENCH"
scene.render.resolution_x = 1024
scene.render.resolution_y = 1024
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "MATERIAL"
scene.display.shading.show_shadows = True
scene.display.shading.show_cavity = True
scene.display.shading.background_type = "WORLD"
scene.world.color = (.055, .065, .08)

colors = {"Tower_steel": (.43, .46, .48, 1), "Arms_dark_steel": (.10, .12, .13, 1), "Concrete": (.4, .4, .37, 1)}
originals, candidates, report = [], [], []
for name in ("TowerLevel", "TowerTop", "CatchArms", "QuickDisconnect", "LaunchMount", "Foundation"):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.ops.wm.obj_import(filepath=str(source / (name + ".obj")), forward_axis="NEGATIVE_Z", up_axis="Y")
    meshes = [o for o in bpy.context.selected_objects if o.type == "MESH"]
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1:
        bpy.ops.object.join()
    obj = bpy.context.object
    obj.name = name + "_original"
    for material in obj.data.materials:
        color = colors.get(material.name.split(".")[0], (.43, .46, .48, 1))
        material.diffuse_color = color
        material.use_nodes = True
        material.node_tree.nodes.get("Principled BSDF").inputs["Base Color"].default_value = color
    candidate = obj.copy()
    candidate.data = obj.data.copy()
    candidate.name = name
    scene.collection.objects.link(candidate)
    obj.data.calc_loop_triangles()
    before = len(obj.data.loop_triangles)
    bm = bmesh.new()
    bm.from_mesh(candidate.data)
    bmesh.ops.dissolve_degenerate(bm, dist=1e-8, edges=list(bm.edges))
    bmesh.ops.dissolve_limit(bm, angle_limit=math.radians(.05), use_dissolve_boundaries=False,
                            verts=list(bm.verts), edges=list(bm.edges), delimit={"NORMAL", "MATERIAL", "UV"})
    bmesh.ops.triangulate(bm, faces=list(bm.faces))
    bm.normal_update()
    bm.to_mesh(candidate.data)
    bm.free()
    candidate.data.update()
    candidate.data.calc_loop_triangles()
    bpy.context.view_layer.update()
    def bounds(item):
        points = [item.matrix_world @ Vector(p) for p in item.bound_box]
        return [[min(p[i] for p in points) for i in range(3)], [max(p[i] for p in points) for i in range(3)]]
    report.append(dict(module=name, input_triangles=before, output_triangles=len(candidate.data.loop_triangles),
                       input_bounds_blender_m=bounds(obj), output_bounds_blender_m=bounds(candidate)))
    bpy.ops.object.select_all(action="DESELECT")
    candidate.select_set(True)
    bpy.context.view_layer.objects.active = candidate
    bpy.ops.wm.obj_export(filepath=str(output / (name + ".obj")), export_selected_objects=True,
                          forward_axis="NEGATIVE_Z", up_axis="Y", export_materials=True, apply_modifiers=True)
    originals.append(obj)
    candidates.append(candidate)
    if name == "TowerLevel":
        for index in range(1, 23):
            for item, group in ((obj, originals), (candidate, candidates)):
                instance = item.copy()
                instance.data = item.data
                instance.location.z += index * 5.67231
                scene.collection.objects.link(instance)
                group.append(instance)

bpy.ops.object.camera_add(location=(180, -235, 155))
camera = bpy.context.object
camera.rotation_euler = (Vector((0, 18, 68)) - camera.location).to_track_quat("-Z", "Y").to_euler()
camera.data.type = "ORTHO"
camera.data.ortho_scale = 166
camera.data.clip_end = 1000
scene.camera = camera
for name, shown, hidden in (("original", originals, candidates), ("planar-candidate", candidates, originals)):
    for obj in shown: obj.hide_render = False
    for obj in hidden: obj.hide_render = True
    scene.render.filepath = str(output / (name + ".png"))
    bpy.ops.render.render(write_still=True)
bpy.ops.wm.save_as_mainfile(filepath=str(output / "Starbase-planar-review.blend"))
result = dict(blender=bpy.app.version_string, dissolve_angle_degrees=.05, degenerate_edge_distance_m=1e-8,
              input_sha256=source_hashes, camera_position=list(camera.location), camera_target=[0, 18, 68],
              orthographic_scale_m=166, render_resolution=[1024, 1024], render_engine="BLENDER_WORKBENCH",
              modules=report, approved_for_runtime=False,
              assembled_input_triangles=sum(m["input_triangles"]*(23 if m["module"] == "TowerLevel" else 1) for m in report),
              assembled_output_triangles=sum(m["output_triangles"]*(23 if m["module"] == "TowerLevel" else 1) for m in report))
(output / "blender-qualification.json").write_text(json.dumps(result, indent=2) + "\n")
print("PR40_PLANAR_QUALIFICATION_COMPLETE", json.dumps(result))
