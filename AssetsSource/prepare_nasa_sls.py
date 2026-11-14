"""Adapt NASA's downloaded SLS Block 1 STL; never construct replacement rocket parts.

Inspect without Blender:
  python3 prepare_nasa_sls.py SOURCE.stl OUTPUT_DIR --inspect
Run using pinned Blender:
  blender --background --python prepare_nasa_sls.py -- SOURCE.stl OUTPUT_DIR
"""
import argparse
import collections
import hashlib
import json
import math
import pathlib
import struct
import sys

SOURCE_URL = 'https://science.nasa.gov/3d-resources/space-launch-system-sls-block-1/'
SOURCE_SHA256 = 'd3af75412a54f1d15d644cce56e895fe6813452b49dc0558b4b2ddfb484844ac'
TARGET_HEIGHT = 98.3


def bounds(points):
    return ([min(p[a] for p in points) for a in range(3)],
            [max(p[a] for p in points) for a in range(3)])


def read_source(path):
    raw = path.read_bytes()
    digest = hashlib.sha256(raw).hexdigest()
    if digest != SOURCE_SHA256:
        raise ValueError('Different NASA source revision: inspect before changing the pinned SHA-256')
    count = struct.unpack_from('<I', raw, 80)[0]
    if len(raw) != 84 + count * 50:
        raise ValueError('Unexpected binary STL length')
    vertices, faces, lookup = [], [], {}
    for triangle in struct.iter_unpack('<12fH', raw[84:]):
        face = []
        for offset in (3, 6, 9):
            point = triangle[offset:offset + 3]
            # STL repeats vertices. Quantization is solely a connectivity key;
            # the first original floating-point position is retained unchanged.
            key = tuple(round(c, 5) for c in point)
            if key not in lookup:
                lookup[key] = len(vertices)
                vertices.append(point)
            face.append(lookup[key])
        faces.append(face)
    parent = list(range(len(vertices)))

    def root(index):
        while parent[index] != index:
            parent[index] = parent[parent[index]]
            index = parent[index]
        return index

    for a, b, c in faces:
        parent[root(b)] = root(a)
        parent[root(c)] = root(a)
    groups = collections.defaultdict(list)
    for face in faces:
        groups[root(face[0])].append(face)
    parts = []
    for part_faces in groups.values():
        indices = sorted({v for face in part_faces for v in face})
        low, high = bounds([vertices[i] for i in indices])
        parts.append(dict(faces=part_faces, indices=indices, low=low, high=high))
    parts.sort(key=lambda part: -len(part['faces']))
    if len(parts) != 37 or count != 274432:
        raise ValueError('Connectivity changed; do not apply unreviewed grouping')
    return vertices, faces, parts, digest


def classify(part):
    low, high = part['low'], part['high']
    if len(part['faces']) == 12 and low[2] < .001 and high[2] < 5.2:
        return 'PrintStand'
    if low[2] > 133:
        return 'UpperAssembly'
    if high[0] < -5.9:
        return 'LeftSRB'
    if low[0] > 5.9:
        return 'RightSRB'
    return 'CoreAssembly'


def main():
    args_in = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    parser.add_argument('--inspect', action='store_true')
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args(args_in)
    args.output.mkdir(parents=True, exist_ok=True)
    vertices, faces, parts, digest = read_source(args.source)
    for part in parts:
        part['group'] = classify(part)
    flight = [part for part in parts if part['group'] != 'PrintStand']
    floor = min(part['low'][2] for part in flight)
    top = max(part['high'][2] for part in flight)
    scale = TARGET_HEIGHT / (top - floor)

    def world(point):
        return (point[0] * scale, point[1] * scale, (point[2] - floor) * scale)

    manifest = dict(source=SOURCE_URL, source_sha256=digest, source_triangles=len(faces),
                    flight_triangles=sum(len(p['faces']) for p in flight),
                    source_bounds=bounds(vertices), source_nozzle_plane=floor,
                    meters_per_source_unit=scale, height_m=TARGET_HEIGHT,
                    blender_axes='X right, Y depth, Z up',
                    obj_axes='Y up, -Z forward; Blender (x,y,z) maps to OBJ (x,z,-y)',
                    unity_scale=1.0, pivot='centerline at lowest source rocket nozzle plane',
                    changes=['remove separate print stand from flight export',
                             'uniform scale and vertical translation',
                             'split existing connected components; retain all flight triangles',
                             'assign original visualization materials; no invented markings',
                             'smooth surfaces with sharp edges above 30 degrees'],
                    parts=[], core_nozzles_blender_m=[], srb_nozzles_blender_m=[])
    for number, part in enumerate(parts):
        part['name'] = f"{part['group']}__NASA_{number:02d}"
        manifest['parts'].append(dict(name=part['name'], triangles=len(part['faces']),
                                     source_bounds=[part['low'], part['high']],
                                     game_bounds=[world(part['low']), world(part['high'])]))
        # Four disconnected nozzle components have exactly this verified topology.
        if part['group'] == 'CoreAssembly' and len(part['faces']) == 1532:
            center = [(part['low'][a] + part['high'][a]) / 2 for a in range(2)]
            manifest['core_nozzles_blender_m'].append(world((*center, part['low'][2])))
        if part['group'] in ('LeftSRB', 'RightSRB') and len(part['faces']) > 60000:
            # Derive nozzle-center from the lowest ring, not the asymmetric
            # booster/attachment bounding box.
            ring = [vertices[i] for i in part['indices'] if vertices[i][2] < part['low'][2] + .01]
            low, high = bounds(ring)
            center = [(low[a] + high[a]) / 2 for a in range(2)]
            manifest['srb_nozzles_blender_m'].append(world((*center, low[2])))
    manifest['upper_group_base_m'] = min(world(p['low'])[2] for p in flight if p['group'] == 'UpperAssembly')
    manifest['upper_engine'] = 'Not a separately modeled engine: no fabricated engine or definitive plume anchor.'
    report = args.output / 'SLS-Block1-import.json'
    report.write_text(json.dumps(manifest, indent=2) + '\n')
    print('NASA_SLS_INSPECT', json.dumps({k: manifest[k] for k in
          ('source_triangles', 'flight_triangles', 'height_m', 'meters_per_source_unit',
           'core_nozzles_blender_m', 'srb_nozzles_blender_m', 'upper_group_base_m')}), flush=True)
    if args.inspect:
        return

    import bpy
    import bmesh
    from mathutils import Vector
    bpy.ops.object.select_all(action='SELECT')
    bpy.ops.object.delete(use_global=False)
    scene = bpy.context.scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    scene['source_url'] = SOURCE_URL
    scene['source_sha256'] = digest
    scene['adaptation'] = 'NASA STL geometry, original ESP materials; printed display stand excluded from flight export.'
    model_collection = bpy.data.collections.new('SLS NASA adapted flight geometry')
    scene.collection.children.link(model_collection)
    archive = bpy.data.collections.new('SOURCE ARCHIVE - unscaled NASA STL including print stand')
    scene.collection.children.link(archive)
    archive.hide_render = True
    archive.hide_viewport = True
    raw_mesh = bpy.data.meshes.new('NASA original STL welded topology')
    raw_mesh.from_pydata(vertices, [], faces)
    raw_object = bpy.data.objects.new('NASA original source - unscaled - do not export', raw_mesh)
    archive.objects.link(raw_object)
    raw_object.hide_render = True

    def material(name, color, metallic=0, roughness=.5):
        mat = bpy.data.materials.new(name)
        mat.diffuse_color = (*color, 1)
        mat.use_nodes = True
        bsdf = mat.node_tree.nodes.get('Principled BSDF')
        bsdf.inputs['Base Color'].default_value = (*color, 1)
        bsdf.inputs['Metallic'].default_value = metallic
        bsdf.inputs['Roughness'].default_value = roughness
        return mat

    white = material('Booster ceramic white', (.83, .86, .85), roughness=.48)
    orange = material('Core foam orange', (.72, .27, .055), roughness=.60)
    graphite = material('Engine graphite', (.08, .09, .10), .55, .48)
    metal = material('Metal', (.48, .51, .52), .72, .38)
    mats = [white, orange, graphite, metal]
    model = []
    for part in flight:
        remap = {old: new for new, old in enumerate(part['indices'])}
        mesh = bpy.data.meshes.new(part['name'])
        mesh.from_pydata([world(vertices[i]) for i in part['indices']], [],
                         [[remap[i] for i in face] for face in part['faces']])
        mesh.update()
        obj = bpy.data.objects.new(part['name'], mesh)
        model_collection.objects.link(obj)
        obj['provenance'] = SOURCE_URL
        obj['source_triangle_count'] = len(part['faces'])
        obj['source_component_bounds'] = json.dumps([part['low'], part['high']])
        for mat in mats:
            mesh.materials.append(mat)
        for poly, source_face in zip(mesh.polygons, part['faces']):
            z = sum(vertices[i][2] for i in source_face) / 3
            if part['group'] == 'CoreAssembly':
                index = 2 if len(part['faces']) == 1532 else (1 if len(part['faces']) > 100000 else 3)
            elif part['group'] in ('LeftSRB', 'RightSRB'):
                # Only the existing aft nozzle region is darkened, without
                # adding fictitious bands, decals or new rocket geometry.
                index = 2 if z < 8.2 else 0
            else:
                index = 0
            poly.material_index = index
            poly.use_smooth = True
        bm = bmesh.new()
        bm.from_mesh(mesh)
        bm.normal_update()
        for edge in bm.edges:
            edge.smooth = len(edge.link_faces) == 2 and edge.calc_face_angle(0) <= math.radians(30)
        bm.to_mesh(mesh)
        bm.free()
        mesh.update()
        model.append(obj)

    bpy.ops.object.select_all(action='DESELECT')
    for obj in model:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model[0]
    bpy.ops.wm.obj_export(filepath=str(args.output / 'SLS-Block1.obj'),
                          export_selected_objects=True, forward_axis='NEGATIVE_Z', up_axis='Y',
                          export_materials=True, export_normals=True, export_object_groups=True,
                          export_triangulated_mesh=True)
    manifest['exported_meshes'] = len(model)
    manifest['blender_version'] = bpy.app.version_string
    report.write_text(json.dumps(manifest, indent=2) + '\n')

    # Studio-only geometry never enters the exported rocket. The floor gives
    # genuine ground contact and shadows; no replacement rocket mesh is made.
    floor_mat = material('Studio charcoal', (.022, .032, .048), roughness=.72)
    bpy.ops.mesh.primitive_plane_add(size=2000, location=(0, 0, -.025))
    studio_floor = bpy.context.object
    studio_floor.name = 'STUDIO ONLY ground'
    studio_floor.data.materials.append(floor_mat)
    world_shader = scene.world or bpy.data.worlds.new('SLS studio world')
    scene.world = world_shader
    world_shader.use_nodes = True
    background = world_shader.node_tree.nodes.get('Background')
    background.inputs['Color'].default_value = (.13, .18, .25, 1)
    background.inputs['Strength'].default_value = .32
    target = Vector((0, 0, 48))

    def light(name, location, power, size, color):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy = power
        data.shape = 'DISK'
        data.size = size
        data.color = color
        obj = bpy.data.objects.new(name, data)
        scene.collection.objects.link(obj)
        obj.location = location
        obj.rotation_euler = (target - obj.location).to_track_quat('-Z', 'Y').to_euler()

    light('STUDIO warm broad key', (-50, -65, 105), 240000, 55, (1, .90, .78))
    light('STUDIO cool fill', (65, -30, 55), 125000, 65, (.69, .82, 1))
    light('STUDIO bright rim', (20, 55, 108), 300000, 42, (.85, .92, 1))
    camera_data = bpy.data.cameras.new('STUDIO three quarter camera')
    camera = bpy.data.objects.new(camera_data.name, camera_data)
    scene.collection.objects.link(camera)
    camera.location = (110, -230, 110)
    camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
    camera_data.type = 'ORTHO'
    camera_data.ortho_scale = 115
    camera_data.clip_end = 3000
    scene.camera = camera
    scene.render.engine = 'CYCLES'
    scene.cycles.device = 'CPU'
    scene.cycles.samples = 48
    scene.cycles.use_denoising = True
    scene.render.resolution_x = 900
    scene.render.resolution_y = 1200
    scene.render.resolution_percentage = 100
    scene.view_settings.view_transform = 'AgX'
    scene.render.image_settings.file_format = 'PNG'
    scene.render.filepath = str(args.output / 'SLS-Block1-preview.png')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in model:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = model[0]
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output / 'SLS-Block1.blend'))
    if not args.no_render:
        bpy.ops.render.render(write_still=True)
    print('NASA_SLS_ADAPTATION_EXPORTED', str(args.output), flush=True)


if __name__ == '__main__':
    main()
