"""Convert the pinned NASA mobile launcher to a static, textured Unity FBX.

Run with Blender in the authoring VM:
  Blender --background --factory-startup --python prepare_nasa_mobile_launcher.py -- SOURCE.glb OUTPUT_DIR

The original GLB remains the editable source; this process only applies a
uniform visual approximation and preserves the model's two source materials.
"""
import hashlib
import json
import math
import pathlib
import sys

import bpy
from mathutils import Matrix, Vector

SOURCE_HASH = '411ad36727a4290123431e77ca7a4622dcd534e2e76eb1134dc19fc07dd5be65'
TARGET_HEIGHT_METERS = 121.92  # NASA ML1: approximately 400 feet above ground.
SOURCE_NAMES = {'mobilelauncher_mat': ('ML_Base', 'MobileLauncherBase.png'),
                'mobilelauncher_railings_mat': ('ML_Railings', 'MobileLauncherRailings.png')}


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def world_bounds(objects):
    low = [math.inf] * 3
    high = [-math.inf] * 3
    for obj in objects:
        for vertex in obj.data.vertices:
            point = obj.matrix_world @ vertex.co
            for axis in range(3):
                low[axis] = min(low[axis], float(point[axis]))
                high[axis] = max(high[axis], float(point[axis]))
    if not all(math.isfinite(x) for x in low + high):
        raise ValueError('No finite mesh bounds')
    return low, high


def main():
    source, output = [pathlib.Path(arg).expanduser().resolve() for arg in sys.argv[sys.argv.index('--') + 1:]]
    if sha(source) != SOURCE_HASH:
        raise ValueError('Unexpected NASA model hash')
    output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(source))
    bpy.context.view_layer.update()
    meshes = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if len(meshes) != 1 or len(meshes[0].data.materials) != 2:
        raise ValueError('Unexpected source mesh/material count')
    obj = meshes[0]
    if len(obj.data.vertices) != 30277 or len(obj.data.polygons) != 19296:
        raise ValueError('Unexpected source mesh topology')
    low, high = world_bounds(meshes)
    height = high[2] - low[2]
    if height <= 0:
        raise ValueError('Invalid source height')
    factor = TARGET_HEIGHT_METERS / height
    center = Vector(((low[0] + high[0]) / 2, (low[1] + high[1]) / 2, low[2]))
    matrix = Matrix.Scale(factor, 4) @ Matrix.Translation(-center) @ obj.matrix_world
    obj.data.transform(matrix)
    if matrix.determinant() < 0:
        obj.data.flip_normals()
    obj.matrix_world = Matrix.Identity(4)
    obj.animation_data_clear()
    for material in obj.data.materials:
        if material is None or material.name not in SOURCE_NAMES or not material.use_nodes:
            raise ValueError('Unexpected source material')
        images = [n.image for n in material.node_tree.nodes if n.type == 'TEX_IMAGE' and n.image]
        if len(images) != 1:
            raise ValueError('Expected one packed texture for ' + material.name)
        image = images[0]
        image.filepath_raw = str(output / SOURCE_NAMES[material.name][1])
        image.file_format = 'PNG'
        image.save()
    bpy.ops.object.select_all(action='DESELECT')
    obj.select_set(True)
    bpy.context.view_layer.objects.active = obj
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')
    bpy.ops.object.mode_set(mode='OBJECT')
    parts = [o for o in bpy.context.scene.objects if o.type == 'MESH']
    if len(parts) != 2 or sum(len(o.data.polygons) for o in parts) != 19296:
        raise ValueError('Material split changed source geometry')
    for part in parts:
        used = {part.data.materials[p.material_index].name for p in part.data.polygons}
        if len(used) != 1 or next(iter(used)) not in SOURCE_NAMES:
            raise ValueError('Unexpected split material')
        part.name = SOURCE_NAMES[next(iter(used))][0]
        part.data.name = part.name
    if {p.name for p in parts} != {'ML_Base', 'ML_Railings'}:
        raise ValueError('Missing source material group')
    bpy.ops.wm.save_as_mainfile(filepath=str(output / 'MobileLauncher-editable.blend'), compress=True)
    bpy.ops.object.select_all(action='DESELECT')
    for part in parts:
        part.select_set(True)
    bpy.context.view_layer.objects.active = parts[0]
    fbx = output / 'MobileLauncher.fbx'
    bpy.ops.export_scene.fbx(filepath=str(fbx), use_selection=True, object_types={'MESH'},
        axis_forward='-Z', axis_up='Y', apply_unit_scale=True, bake_anim=False,
        mesh_smooth_type='FACE', path_mode='AUTO')
    low_out, high_out = world_bounds(parts)
    manifest = {'sourceSha256': SOURCE_HASH, 'blenderVersion': bpy.app.version_string,
        'units': 'meters', 'targetHeightMeters': TARGET_HEIGHT_METERS,
        'sourceBoundsBlender': [low, high], 'normalizationScale': factor,
        'sourcePivotBlender': list(center), 'normalizedBoundsBlender': [low_out, high_out],
        'materialObjects': [p.name for p in parts], 'sourceTriangles': 19296,
        'outputs': {p.name: {'sha256': sha(p), 'bytes': p.stat().st_size}
                    for p in (fbx, output / 'MobileLauncherBase.png', output / 'MobileLauncherRailings.png')}}
    (output / 'MobileLauncher.json').write_text(json.dumps(manifest, indent=2) + '\n')
    print('ESP_ML1_CONVERSION_OK ' + json.dumps(manifest), flush=True)


if __name__ == '__main__':
    main()
