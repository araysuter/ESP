"""Inspect and studio-render the downloaded Clarence365 source without remodeling.

blender --disable-autoexec --background --python inspect_starship_v3.py -- \
    SOURCE.blend OUTPUT_DIR --texture-dir TEXTURES --views three-quarter,above

Writes JSON inventory before rendering. Never overwrites/saves the source blend.
Only cameras, lights and a neutral world are created; no mesh is created or edited.
"""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path
import sys


def values(value):
    if isinstance(value, (str, bool, int, float)) or value is None:
        return value
    try:
        return [float(v) for v in value]
    except (TypeError, ValueError):
        return str(value)


def matrix(value):
    return [[float(v) for v in row] for row in value]


def bounds(points):
    if not points:
        return None
    low = [min(p[i] for p in points) for i in range(3)]
    high = [max(p[i] for p in points) for i in range(3)]
    return {'min': low, 'max': high, 'extent': [high[i] - low[i] for i in range(3)]}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path)
    parser.add_argument('output', type=Path)
    parser.add_argument('--texture-dir', type=Path)
    parser.add_argument('--scene', help='Source scene name; defaults to saved active scene')
    parser.add_argument('--views', default='three-quarter',
                        help='Comma-separated front,rear,side,three-quarter,above,underside')
    parser.add_argument('--samples', type=int, default=24)
    parser.add_argument('--resolution', type=int, default=1200, help='Portrait image height')
    parser.add_argument('--no-render', action='store_true')
    argv = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
    args = parser.parse_args(argv)
    source, output = args.source.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    if source.parent == output and source.name.startswith('source-preview'):
        raise ValueError('Use a separate inspection output directory')
    import bpy
    from mathutils import Vector
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
    original = bpy.data.scenes.get(args.scene) if args.scene else bpy.context.scene
    if original is None:
        raise ValueError('Requested source scene does not exist')
    bpy.context.window.scene = original
    bpy.context.view_layer.update()
    depsgraph = bpy.context.evaluated_depsgraph_get()
    texture_root = (args.texture_dir or source.parent.parent / 'textures').resolve()
    def texture_key(name):
        name = re.sub(r"\.\d{3}$", "", name)
        stem, extension = Path(name).stem, Path(name).suffix.casefold()
        return re.sub(r"[^a-z0-9]", "", stem.casefold()) + ('.jpg' if extension == '.jpeg' else extension)
    search_roots = list(dict.fromkeys([texture_root, source.parent / 'textures', source.parent.parent / 'textures']))
    candidates = [p for root in search_roots if root.is_dir() for p in root.rglob('*') if p.is_file()]
    texture_files = {texture_key(p.name): p for p in candidates}
    print('STARSHIP_TEXTURE_SEARCH', json.dumps({'texture_dir_exists': texture_root.is_dir(),
          'candidate_files': sorted({p.name for p in candidates})}), flush=True)
    recovered, image_records = [], []
    for img in bpy.data.images:
        filename = img.filepath.replace('\\', '/').rsplit('/', 1)[-1]
        record = {'name': img.name, 'filename': filename, 'source': img.source,
                  'size': list(img.size), 'colorspace': img.colorspace_settings.name,
                  'packed': bool(img.packed_file), 'has_data': img.has_data}
        candidate = texture_files.get(texture_key(filename)) or texture_files.get(texture_key(img.name))
        if not img.packed_file and candidate is not None:
            img.filepath = str(candidate.resolve())
            try:
                img.reload()
                # Touch pixels so lazy image loading happens before status capture.
                if len(img.pixels):
                    _ = img.pixels[0]
                recovered.append({'image': img.name, 'file': candidate.name, 'loaded': bool(img.has_data)})
            except RuntimeError as error:
                record['reload_error'] = str(error).replace(str(texture_root), '<textures>')
        record['loaded_after_relink'] = bool(img.has_data)
        record['size_after_relink'] = list(img.size)
        image_records.append(record)
    used_images = {node.image.name for mat in bpy.data.materials if mat.use_nodes
                   for node in mat.node_tree.nodes if getattr(node, 'image', None)}
    missing = [img.name for img in bpy.data.images if img.name in used_images and not img.has_data]
    print('STARSHIP_TEXTURE_RELINK', json.dumps({'recovered': recovered, 'missing_material_images': missing}), flush=True)
    if missing:
        raise ValueError('Required material textures could not load: ' + ', '.join(missing) +
                         '; check --texture-dir points to extracted archive textures directory')

    def animation_record(data):
        if data is None:
            return None
        return {'action': data.action.name if data.action else None,
                'nla_tracks': [{'name': track.name, 'mute': track.mute,
                                'strips': [{'name': s.name, 'action': s.action.name if s.action else None,
                                            'frame_start': s.frame_start, 'frame_end': s.frame_end}
                                           for s in track.strips]} for track in data.nla_tracks],
                'drivers': [{'path': f.data_path, 'index': f.array_index,
                             'type': f.driver.type, 'expression': f.driver.expression}
                            for f in data.drivers]}

    inventory = []
    for obj in sorted(bpy.data.objects, key=lambda item: item.name):
        record = {'name': obj.name, 'type': obj.type, 'parent': obj.parent.name if obj.parent else None,
                  'children': [child.name for child in obj.children],
                  'collections': [c.name for c in obj.users_collection],
                  'in_selected_scene': obj.name in original.objects,
                  'hide_render': obj.hide_render, 'hide_viewport': obj.hide_viewport,
                  'location': list(obj.location), 'rotation_mode': obj.rotation_mode,
                  'rotation_euler': list(obj.rotation_euler), 'rotation_quaternion': list(obj.rotation_quaternion),
                  'scale': list(obj.scale), 'dimensions': list(obj.dimensions),
                  'matrix_world': matrix(obj.matrix_world), 'matrix_local': matrix(obj.matrix_local),
                  'matrix_parent_inverse': matrix(obj.matrix_parent_inverse),
                  'materials': [slot.material.name if slot.material else None for slot in obj.material_slots],
                  'modifiers': [{'name': m.name, 'type': m.type, 'show_render': m.show_render,
                                 'show_viewport': m.show_viewport} for m in obj.modifiers],
                  'constraints': [{'name': c.name, 'type': c.type, 'influence': c.influence,
                                   'target': c.target.name if getattr(c, 'target', None) else None}
                                  for c in obj.constraints],
                  'animation': animation_record(obj.animation_data)}
        if obj.type == 'MESH':
            obj.data.calc_loop_triangles()
            record['mesh'] = {'name': obj.data.name, 'vertices': len(obj.data.vertices),
                              'polygons': len(obj.data.polygons), 'triangles': len(obj.data.loop_triangles),
                              'uv_layers': [uv.name for uv in obj.data.uv_layers],
                              'has_custom_normals': obj.data.has_custom_normals,
                              'material_polygon_counts': {str(i): sum(p.material_index == i for p in obj.data.polygons)
                                                          for i in range(len(obj.material_slots))},
                              'shape_keys': [k.name for k in obj.data.shape_keys.key_blocks] if obj.data.shape_keys else []}
            record['world_bounds'] = bounds([obj.matrix_world @ Vector(corner) for corner in obj.bound_box])
            if obj.name in original.objects:
                evaluated = obj.evaluated_get(depsgraph)
                mesh = evaluated.to_mesh()
                try:
                    mesh.calc_loop_triangles()
                    record['evaluated_mesh'] = {'vertices': len(mesh.vertices), 'triangles': len(mesh.loop_triangles),
                                                'world_bounds': bounds([evaluated.matrix_world @ Vector(c) for c in evaluated.bound_box])}
                finally:
                    evaluated.to_mesh_clear()
        inventory.append(record)
    materials = []
    for mat in bpy.data.materials:
        item = {'name': mat.name, 'diffuse_color': list(mat.diffuse_color), 'use_nodes': mat.use_nodes}
        if mat.use_nodes and mat.node_tree:
            item['nodes'] = [{'name': node.name, 'type': node.bl_idname,
                              'image': node.image.name if getattr(node, 'image', None) else None,
                              'inputs': {socket.name: values(socket.default_value) for socket in node.inputs
                                         if hasattr(socket, 'default_value') and not socket.is_linked}}
                             for node in mat.node_tree.nodes]
            item['links'] = [{'from_node': link.from_node.name, 'from_socket': link.from_socket.name,
                              'to_node': link.to_node.name, 'to_socket': link.to_socket.name}
                             for link in mat.node_tree.links]
        materials.append(item)
    actions = []
    for action in bpy.data.actions:
        actions.append({'name': action.name, 'frame_range': list(action.frame_range),
                        'slots': [slot.identifier for slot in getattr(action, 'slots', [])],
                        'layers': [layer.name for layer in getattr(action, 'layers', [])]})
    eligible = set()
    def collect(collection, hidden=False):
        hidden = hidden or collection.hide_render
        if not hidden:
            eligible.update(obj.name for obj in collection.objects if not obj.hide_render)
        for child in collection.children:
            collect(child, hidden)
    collect(original.collection)
    geometry = [obj for obj in original.objects if obj.name in eligible and obj.type in {'MESH', 'CURVE', 'SURFACE', 'FONT', 'META'}]
    points = [obj.matrix_world @ Vector(corner) for obj in geometry for corner in obj.bound_box]
    total_bounds = bounds(points)
    report = {'source_file': source.name, 'source_sha256': hashlib.sha256(source.read_bytes()).hexdigest(),
              'blender_version': bpy.app.version_string, 'source_scene': original.name,
              'source_frame': original.frame_current, 'source_frame_range': [original.frame_start, original.frame_end],
              'source_unit_system': original.unit_settings.system, 'source_unit_scale': original.unit_settings.scale_length,
              'autoexec_scripts_allowed': False, 'autoexec_fail': getattr(bpy.app, 'autoexec_fail', None),
              'source_scenes': [{'name': s.name, 'objects': len(s.objects), 'camera': s.camera.name if s.camera else None}
                                for s in bpy.data.scenes],
              'objects': inventory, 'materials': materials, 'images': image_records, 'actions': actions,
              'texture_relinks': recovered, 'render_geometry': [obj.name for obj in geometry],
              'render_bounds': total_bounds,
              'notes': ['Source transforms, materials, current frame and geometry retained.',
                        'Only missing external image paths repaired using supplied texture basenames.',
                        'Neutral studio lighting replaces source lighting for inspection.',
                        'Disabled Python drivers may affect evaluated pose; inspect driver inventory.',
                        'Automatic framing includes all render-visible geometry; review inventory for prototypes or a source floor.']}
    report_path = output / 'Starship-V3-source-inspection.json'
    report_path.write_text(json.dumps(report, indent=2) + '\n')
    print('STARSHIP_V3_INSPECTION', json.dumps({'objects': len(inventory), 'materials': len(materials),
          'render_geometry': len(geometry), 'bounds': total_bounds, 'report': report_path.name}), flush=True)
    if args.no_render:
        return
    if not geometry or not total_bounds:
        raise ValueError('No render-visible geometry; inspection JSON has been written')
    # New scene, original object links: do not copy, apply or edit any rocket mesh.
    scene = bpy.data.scenes.new('ESP source inspection studio')
    for obj in original.objects:
        if obj.type not in {'LIGHT', 'CAMERA'} and (obj.name in eligible or obj.type not in {'MESH','CURVE','SURFACE','FONT','META'}):
            scene.collection.objects.link(obj)
    scene.frame_set(original.frame_current)
    bpy.context.window.scene = scene
    extent = total_bounds['extent']
    axis = max(range(3), key=lambda i: extent[i])
    up = Vector(tuple(1 if i == axis else 0 for i in range(3)))
    right = Vector((1, 0, 0)) if axis != 0 else Vector((0, 1, 0))
    front = right.cross(up).normalized()
    center = (Vector(total_bounds['min']) + Vector(total_bounds['max'])) * .5
    span = max(extent)
    if not math.isfinite(span) or span <= 0:
        raise ValueError('Invalid source bounds')
    world = bpy.data.worlds.new('ESP neutral inspection world')
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (.16, .18, .22, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = .65
    scene.world = world
    def area(name, offset, power, size, color):
        data = bpy.data.lights.new(name, 'AREA')
        data.energy, data.shape, data.size, data.color = power * span * span, 'DISK', size * span, color
        obj = bpy.data.objects.new(name, data);scene.collection.objects.link(obj)
        obj.location = center + offset * span
        obj.rotation_euler = (center - obj.location).to_track_quat('-Z', 'Y').to_euler()
    area('Inspection key', right * -.8 + front * .8 + up * .6, 28, .8, (1, .94, .86))
    area('Inspection fill', right * .9 + front * .3, 16, .75, (.76, .87, 1))
    area('Inspection rim', front * -.7 + up * .5, 36, .6, (.92, .96, 1))
    data = bpy.data.cameras.new('Inspection camera')
    camera = bpy.data.objects.new('Inspection camera', data);scene.collection.objects.link(camera)
    scene.camera = camera
    data.type = 'ORTHO'; data.clip_start = span * .0001; data.clip_end = span * 20
    scene.render.engine = 'CYCLES';scene.cycles.device = 'CPU'
    scene.cycles.samples = max(1, args.samples);scene.cycles.use_denoising = True
    scene.render.resolution_x = max(64, int(args.resolution * .75));scene.render.resolution_y = max(64, args.resolution)
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    scene.view_settings.view_transform = 'AgX'
    directions = {'front': front, 'rear': -front, 'side': right,
                  'three-quarter': front + right * .7 + up * .12,
                  'above': up + front * .12, 'underside': -up + front * .12}
    for name in args.views.split(','):
        name = name.strip()
        if name not in directions:
            raise ValueError('Unknown view: ' + name)
        direction = directions[name].normalized()
        camera.location = center + direction * span * 3
        # Camera local +Y follows the rocket's long axis whenever possible.
        target_up = front if abs(direction.dot(up)) > .95 else up
        forward = -direction
        cam_right = forward.cross(target_up).normalized()
        cam_up = cam_right.cross(forward).normalized()
        from mathutils import Matrix
        camera.rotation_euler = Matrix((cam_right, cam_up, direction)).transposed().to_quaternion().to_euler()
        # Fit projected bounds rather than guessing that the scene uses Z up.
        widths = [(point - center).dot(cam_right) for point in points]
        heights = [(point - center).dot(cam_up) for point in points]
        aspect = scene.render.resolution_x / scene.render.resolution_y
        data.ortho_scale = max(max(heights)-min(heights), (max(widths)-min(widths))/aspect) * 1.14
        scene.render.filepath = str(output / ('Starship-V3-source-' + name + '.png'))
        bpy.ops.render.render(write_still=True)
        print('STARSHIP_V3_RENDERED', name, flush=True)
    report['render_axis_guess'] = 'XYZ'[axis]
    report['rendered_views'] = args.views.split(',')
    report_path.write_text(json.dumps(report, indent=2) + '\n')


if __name__ == '__main__':
    main()
