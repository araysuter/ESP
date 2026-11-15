"""Prepare the pinned NASA historical LC-39 gantry without inventing geometry.

Run ONLY in the qualified authoring VM, in a dedicated Blender background process:
  blender --background --python AssetsSource/prepare_nasa_gantry.py -- SOURCE.glb OUTPUT_DIR [--preview]

The editable blend retains the imported source and the normalized runtime meshes.
Only the three material-group runtime objects enter the triangulated Y-up OBJ.
"""
import argparse
import gzip
import hashlib
import json
import math
import pathlib
import sys

SOURCE_SHA256 = 'ad89678f80ca08102fa8c730fde6b93a0b700c60a15d69d5de422f839c66eb82'
SOURCE_REVISION = '11ebb4ee043715aefbba6aeec8a61746fad67fa7'
SOURCE_URL = 'https://science.nasa.gov/3d-resources/gantry/'
DOWNLOAD_URL = ('https://raw.githubusercontent.com/nasa/NASA-3D-Resources/' + SOURCE_REVISION +
                '/3D%20Models/Gantry/Gantry.glb')
HEIGHT_REFERENCE = 'https://asd.gsfc.nasa.gov/archive/sm3b/launch-info/kennedy-part1.html'
ATTRIBUTION = 'Historical LC-39 gantry: NASA / Michael D. Carbajal; ESP scale and material-group adaptation.'
TARGET_HEIGHT_METERS = 105.7
EXPECTED_SOURCE = {'objects': 724, 'vertices': 176490, 'polygons': 172602, 'materials': 3}


def digest(path):
    result = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            result.update(block)
    return result.hexdigest()


def bounds(objects):
    minimum = [math.inf] * 3
    maximum = [-math.inf] * 3
    for obj in objects:
        matrix = obj.matrix_world
        for vertex in obj.data.vertices:
            point = matrix @ vertex.co
            for axis in range(3):
                minimum[axis] = min(minimum[axis], float(point[axis]))
                maximum[axis] = max(maximum[axis], float(point[axis]))
    if not all(math.isfinite(value) for value in minimum + maximum):
        raise ValueError('Source has no finite mesh bounds')
    return {'minimum': minimum, 'maximum': maximum}


def material_record(material, runtime_name):
    color = list(material.diffuse_color)
    metallic, roughness = 0.0, .6
    if material.use_nodes:
        shader = next((node for node in material.node_tree.nodes if node.type == 'BSDF_PRINCIPLED'), None)
        if shader is None:
            raise ValueError('Source shader requires a reviewed conversion: ' + material.name)
        for name in ('Base Color', 'Metallic', 'Roughness'):
            if shader.inputs[name].is_linked:
                raise ValueError('Unexpected linked material input: ' + material.name + '/' + name)
        color = list(shader.inputs['Base Color'].default_value)
        metallic = float(shader.inputs['Metallic'].default_value)
        roughness = float(shader.inputs['Roughness'].default_value)
    return {'name': runtime_name, 'sourceName': material.name, 'baseColor': color,
            'metallic': metallic, 'roughness': roughness}


def select_only(bpy, objects):
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objects:
        obj.hide_set(False)
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objects[0]


def inspect_obj(path, expected_names):
    """Validate actual exporter output, including triangulation/axis conversion bounds.

    No triangulation or coordinate reconstruction is performed here: all geometry
    remains Blender's export of the source-derived meshes.
    """
    low, high = [math.inf] * 3, [-math.inf] * 3
    counts = {'vertices': 0, 'normals': 0, 'triangles': 0}
    objects, materials = set(), set()
    for line in path.read_text(encoding='utf-8').splitlines():
        fields = line.split()
        if not fields:
            continue
        if fields[0] == 'v':
            point = [float(value) for value in fields[1:4]]
            if len(point) != 3 or not all(math.isfinite(value) for value in point):
                raise ValueError('Invalid exported vertex')
            counts['vertices'] += 1
            for axis in range(3):
                low[axis] = min(low[axis], point[axis])
                high[axis] = max(high[axis], point[axis])
        elif fields[0] == 'vn':
            counts['normals'] += 1
        elif fields[0] == 'f':
            if len(fields) != 4:
                raise ValueError('OBJ contains a non-triangle face')
            counts['triangles'] += 1
        elif fields[0] == 'o':
            objects.add(' '.join(fields[1:]))
        elif fields[0] == 'usemtl':
            materials.add(' '.join(fields[1:]))
    if objects != set(expected_names) or materials != set(expected_names):
        raise ValueError('OBJ object/material names do not match importer contract: ' + repr((objects, materials)))
    if counts['normals'] == 0 or counts['triangles'] == 0:
        raise ValueError('OBJ is missing normals or faces')
    if abs(low[1]) > .002 or abs((high[1] - low[1]) - TARGET_HEIGHT_METERS) > .002:
        raise ValueError('OBJ axis/height/floor check failed: ' + repr((low, high)))
    return counts, {'minimum': low, 'maximum': high}


def main():
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    parser.add_argument('--preview', action='store_true', help='Render a labeled Workbench preview in the VM')
    args = parser.parse_args(argv)
    source_hash = digest(args.source)
    if source_hash != SOURCE_SHA256:
        raise ValueError('NASA source hash differs; review the new source before changing the pin')

    import bpy
    from mathutils import Matrix, Vector

    if bpy.app.version[:2] != (5, 2):
        raise RuntimeError('Use the qualified Blender 5.2.x authoring version; observed ' + bpy.app.version_string)
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    bpy.ops.import_scene.gltf(filepath=str(args.source.resolve()))
    bpy.context.view_layer.update()
    source_objects = list(bpy.context.scene.objects)
    source_meshes = sorted((obj for obj in source_objects if obj.type == 'MESH'), key=lambda obj: obj.name)
    source_materials = sorted({slot.material for obj in source_meshes for slot in obj.material_slots
                               if slot.material is not None}, key=lambda material: material.name)
    observed = {'objects': len(source_meshes), 'vertices': sum(len(obj.data.vertices) for obj in source_meshes),
                'polygons': sum(len(obj.data.polygons) for obj in source_meshes), 'materials': len(source_materials)}
    if observed != EXPECTED_SOURCE:
        raise ValueError('Imported source topology changed: ' + json.dumps(observed))
    if any(node.type == 'TEX_IMAGE' for material in source_materials if material.use_nodes
           for node in material.node_tree.nodes):
        raise ValueError('Unexpected source image dependency; this preparation expects three constant gray materials')
    if any(obj.modifiers for obj in source_meshes):
        raise ValueError('Unexpected source modifier; do not silently evaluate altered geometry')
    original_bounds = bounds(source_meshes)
    low, high = original_bounds['minimum'], original_bounds['maximum']
    height = high[2] - low[2]
    if height <= 0:
        raise ValueError('Invalid source vertical extent')
    center = Vector(((low[0] + high[0]) * .5, (low[1] + high[1]) * .5, low[2]))
    scale = TARGET_HEIGHT_METERS / height
    normalization = Matrix.Scale(scale, 4) @ Matrix.Translation(-center)

    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene['source_url'] = SOURCE_URL
    scene['source_sha256'] = source_hash
    scene['asset_description'] = 'Historical shuttle-era LC-39 gantry, approximately scaled; not current LC-39A or Starbase.'
    archive = bpy.data.collections.new('SOURCE ARCHIVE - original imported NASA Gantry')
    runtime = bpy.data.collections.new('RUNTIME - normalized NASA gantry, three material groups')
    scene.collection.children.link(archive)
    scene.collection.children.link(runtime)
    for obj in source_objects:
        for collection in list(obj.users_collection):
            collection.objects.unlink(obj)
        archive.objects.link(obj)

    records, material_map = [], {}
    for index, original in enumerate(source_materials):
        name = 'NASA_Gantry_Material_' + str(index).zfill(2)
        records.append(material_record(original, name))
        adapted = original.copy()
        adapted.name = name
        # Workbench and Unity use the exact source base color, not invented paint.
        adapted.diffuse_color = records[-1]['baseColor']
        material_map[original] = adapted

    copies = []
    for original in source_meshes:
        mesh = original.data.copy()
        world = normalization @ original.matrix_world
        mesh.transform(world)
        if world.determinant() < 0:
            mesh.flip_normals()
        for index, material in enumerate(mesh.materials):
            if material is None:
                raise ValueError('Unassigned source material on ' + original.name)
            mesh.materials[index] = material_map[material]
        mesh.update()
        obj = bpy.data.objects.new('Adapted_' + original.name, mesh)
        runtime.objects.link(obj)
        obj.matrix_world = Matrix.Identity(4)
        copies.append(obj)

    # Join source-derived copies, then split only by their original materials.
    # This preserves disconnected details, polygon smoothing, UVs and all faces.
    select_only(bpy, copies)
    bpy.ops.object.join()
    merged = bpy.context.object
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.mesh.separate(type='MATERIAL')
    bpy.ops.object.mode_set(mode='OBJECT')
    exported_objects = [obj for obj in runtime.objects if obj.type == 'MESH']
    if len(exported_objects) != 3:
        raise ValueError('Expected exactly three runtime material-group objects')
    for obj in exported_objects:
        used = {obj.data.materials[face.material_index] for face in obj.data.polygons}
        if len(used) != 1:
            raise ValueError('A runtime group has multiple used materials')
        material = next(iter(used))
        obj.name = material.name
        obj.data.name = material.name
        obj.data.calc_loop_triangles()
    if sum(len(obj.data.polygons) for obj in exported_objects) != observed['polygons']:
        raise ValueError('Material grouping changed the source face count')
    triangle_count = sum(len(obj.data.loop_triangles) for obj in exported_objects)
    archive.hide_render = True
    archive.hide_viewport = True

    select_only(bpy, exported_objects)
    obj_path = args.output / 'NasaGantry.obj'
    bpy.ops.wm.obj_export(filepath=str(obj_path.resolve()), export_selected_objects=True,
                          forward_axis='NEGATIVE_Z', up_axis='Y', export_materials=True,
                          export_normals=True, export_uv=True, export_object_groups=False,
                          export_triangulated_mesh=True, path_mode='RELATIVE')
    names = [record['name'] for record in records]
    obj_counts, obj_bounds = inspect_obj(obj_path, names)
    if obj_counts['triangles'] != triangle_count:
        raise ValueError('OBJ export changed the triangulated source face count')
    compressed = args.output / 'NasaGantry.obj.gz'
    with obj_path.open('rb') as original, compressed.open('wb') as packed:
        with gzip.GzipFile(filename='', mode='wb', fileobj=packed, compresslevel=9, mtime=0) as compressor:
            for block in iter(lambda: original.read(1024 * 1024), b''):
                compressor.write(block)

    manifest = {
        'schemaVersion': 1, 'name': 'NASA historical LC-39 gantry',
        'sourceUrl': SOURCE_URL, 'downloadUrl': DOWNLOAD_URL, 'sourceRevision': SOURCE_REVISION,
        'sourceSha256': source_hash, 'attribution': ATTRIBUTION,
        'usageGuidelines': 'https://www.nasa.gov/nasa-brand-center/images-and-media/',
        'sourceRightsStatement': 'NASA 3D Resources describes its assets as free and without copyright.',
        'heightMeters': TARGET_HEIGHT_METERS, 'heightReference': HEIGHT_REFERENCE,
        'scaleQualification': 'Entire artistic model bounds normalized to documented FSS mast height; not a surveyed pad reconstruction.',
        'sourceCounts': observed, 'sourceBoundsBlender': original_bounds,
        'metersPerImportedUnit': scale, 'sourceCenterXYAndFloorZ': list(center),
        'normalizationMatrixBlender': [list(row) for row in normalization],
        'upAxis': 'Y', 'forwardAxis': '-Z', 'baseY': 0.0, 'units': 'meters',
        'coordinateConversion': 'Blender (x,y,z) to OBJ (x,z,-y); center XY, floor Z=0 before conversion',
        'pivot': 'horizontal bounding-box center at lowest geometry; not a rocket mount or surveyed launch anchor',
        'bounds': obj_bounds, 'materialNames': names, 'materials': records,
        'meshObjects': len(exported_objects), 'triangles': triangle_count, 'objVertexCount': obj_counts['vertices'],
        'objFile': obj_path.name, 'objBytes': obj_path.stat().st_size, 'objSha256': digest(obj_path),
        'compressedFile': compressed.name, 'compressedBytes': compressed.stat().st_size,
        'compressedSha256': digest(compressed), 'materialFile': 'NasaGantry.mtl',
        'blenderVersion': bpy.app.version_string,
        'changes': ['Uniform scale and translation of imported geometry',
                    'Preserve original import in hidden editable archive collection',
                    'Merge runtime geometry into three original material groups',
                    'Triangulate OBJ export; no decimation, no replacement geometry, no new textures'],
        'validation': {'sourceHash': True, 'sourceCounts': True, 'faceCountPreserved': True,
                       'objTriangles': True, 'objNames': True, 'objHeightAndFloor': True,
                       'unityImportTested': False, 'previewRendered': False},
    }

    # Preview camera and stamp are authoring-only; no new mesh enters the model.
    camera_data = bpy.data.cameras.new('AUTHORING ONLY - gantry preview')
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    scene.collection.objects.link(camera)
    target = Vector((0, 0, TARGET_HEIGHT_METERS * .48))
    camera.location = target + Vector((1.3, -1.8, .65)) * TARGET_HEIGHT_METERS
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = TARGET_HEIGHT_METERS * 1.48
    camera_data.clip_end = 3000
    scene.camera = camera
    scene.render.engine = 'BLENDER_WORKBENCH'
    scene.display.shading.light = 'STUDIO'
    scene.display.shading.color_type = 'MATERIAL'
    scene.display.shading.show_shadows = True
    scene.display.shading.show_cavity = True
    scene.display.shading.cavity_type = 'BOTH'
    scene.display.shading.background_type = 'WORLD'
    if scene.world is None:
        scene.world = bpy.data.worlds.new('Gantry preview background')
    scene.world.color = (.035, .045, .06)
    scene.render.resolution_x = 1200
    scene.render.resolution_y = 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str((args.output / 'NasaGantry-preview.png').resolve())
    scene.render.use_stamp = True
    scene.render.use_stamp_note = True
    scene.render.stamp_note_text = 'HISTORICAL LC-39 GANTRY | NASA / Michael D. Carbajal | Approx. 105.7 m normalization'
    for field in ('date', 'time', 'render_time', 'frame', 'frame_range', 'memory', 'hostname', 'camera', 'lens', 'scene', 'marker', 'filename', 'sequencer_strip'):
        property_name = 'use_stamp_' + field
        if hasattr(scene.render, property_name):
            setattr(scene.render, property_name, False)
    scene.render.stamp_font_size = 16
    scene.render.use_file_extension = True
    bpy.ops.wm.save_as_mainfile(filepath=str((args.output / 'NasaGantry.blend').resolve()), compress=True)
    if args.preview:
        bpy.ops.render.render(write_still=True)
        manifest['validation']['previewRendered'] = True
    (args.output / 'NasaGantry.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    print('ESP_NASA_GANTRY_PREPARED ' + json.dumps({key: manifest[key] for key in
          ('heightMeters', 'meshObjects', 'triangles', 'objBytes', 'compressedBytes', 'compressedSha256')}), flush=True)


if __name__ == '__main__':
    main()
