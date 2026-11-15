"""Correct only authored Heatshield Tiles faces in the existing V3 runtime atlases.

Run in background Blender: --python correct_starship_heatshield.py -- GAME.blend NEW_OUTPUT
Original/source scene, geometry, UVs, steel and normal maps remain unchanged.
The editable game blend must contain the archived source scene from prepare_starship_v3.py.
"""
import hashlib
import json
from pathlib import Path
import sys

import bpy
import numpy as np


def digest(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def main():
    if not bpy.app.background:
        raise RuntimeError('Run in a separate background Blender process')
    args = sys.argv[sys.argv.index('--') + 1:]
    source, output = (Path(p).resolve() for p in args)
    output.mkdir(parents=True, exist_ok=False)
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
    runtime = bpy.data.scenes['ESP Clarence365 V3 runtime adaptation']
    original = bpy.data.scenes['SOURCE ORIGINAL - Clarence365 Block 3 CC BY 4.0']
    bpy.context.window.scene = runtime
    runtime.render.engine = 'CYCLES'
    runtime.cycles.samples = 1
    runtime.cycles.use_denoising = False
    runtime.render.threads_mode = 'FIXED'
    runtime.render.threads = 2
    runtime.render.bake.use_selected_to_active = False
    runtime.render.bake.use_clear = True
    runtime.render.bake.margin = 8
    runtime.render.bake.margin_type = 'EXTEND'
    names = ['Starship Block 3 V4', 'Aft Flaps.002', 'Aft Flaps.003', 'Foward Flap.002', 'Foward Flap.003']
    report = {'source_blend_sha256': digest(source), 'material': 'Heatshield Tiles',
              'adaptation': 'Matte black ceramic tiles; metallic=0, smoothness=0.18; authored face mask, unchanged geometry/UV/normals.', 'parts': []}
    for name in names:
        authored = original.objects[name]
        obj = next(o for o in runtime.objects if o.get('source_object') == name)
        if len(obj.data.polygons) != len(authored.data.polygons):
            raise RuntimeError('Source/runtime topology differs: ' + name)
        for a, b in zip(authored.data.polygons, obj.data.polygons):
            # The adapter reverses winding for mirrored source transforms.
            if sorted(a.vertices) != sorted(b.vertices):
                raise RuntimeError('Source/runtime face ordering differs: ' + name)
        tile_faces = [p.index for p in authored.data.polygons if authored.data.materials[p.material_index].name == 'Heatshield Tiles']
        if not tile_faces:
            raise RuntimeError('No authored tiles: ' + name)
        material = obj.data.materials[0]
        base_name = material.name + '_BaseColor.png'
        packed_name = material.name + '_MetallicSmoothness.png'
        work = obj.copy()
        work.data = obj.data.copy()
        runtime.collection.objects.link(work)
        work.data.materials.clear()
        masks = []
        for i in range(2):
            mat = bpy.data.materials.new('Tile mask ' + str(i))
            mat.use_nodes = True
            nodes = mat.node_tree.nodes
            nodes.clear()
            out = nodes.new('ShaderNodeOutputMaterial')
            emission = nodes.new('ShaderNodeEmission')
            emission.inputs['Color'].default_value = (i, i, i, 1)
            mat.node_tree.links.new(emission.outputs[0], out.inputs['Surface'])
            target = nodes.new('ShaderNodeTexImage')
            nodes.active = target
            work.data.materials.append(mat)
            masks.append(target)
        for index in tile_faces:
            work.data.polygons[index].material_index = 1
        bpy.ops.object.select_all(action='DESELECT')
        work.select_set(True)
        bpy.context.view_layer.objects.active = work
        part = {'source_object': name, 'tile_faces': len(tile_faces), 'files': []}
        for filename, packed in [(base_name, False), (packed_name, True)]:
            input_path = source.parent / 'Textures' / filename
            img = bpy.data.images.load(str(input_path), check_existing=False)
            img.colorspace_settings.name = 'Non-Color' if packed else 'sRGB'
            width, height = img.size
            mask = bpy.data.images.new('Exact authored tile mask', width=width, height=height, alpha=True)
            mask.colorspace_settings.name = 'Non-Color'
            for node in masks:
                node.image = mask
            bpy.ops.object.bake(type='EMIT', uv_layer='ESP_RuntimeUV')
            pixels = np.array(img.pixels[:], dtype=np.float32).reshape(-1, 4)
            before = pixels.copy()
            selection = np.array(mask.pixels[:], dtype=np.float32).reshape(-1, 4)[:, 0] > .5
            if not np.any(selection) or np.all(selection):
                raise RuntimeError('Invalid tile mask: ' + name)
            if packed:
                pixels[selection, 0] = 0
                pixels[selection, 3] = .18
            else:
                # Retain authored luminance variation, capped to charcoal ceramic.
                gray = np.clip(np.mean(pixels[selection, :3], axis=1), .004, .018)
                pixels[selection, :3] = gray[:, None]
            if not np.array_equal(before[~selection], pixels[~selection]):
                raise RuntimeError('Non-tile pixels changed')
            img.pixels.foreach_set(pixels.ravel())
            img.filepath_raw = str(output / filename)
            img.file_format = 'PNG'
            img.save()
            # Rebind the saved editable runtime material to the corrected map.
            for node in material.node_tree.nodes:
                if node.type == 'TEX_IMAGE' and node.image and Path(node.image.filepath).name == filename:
                    node.image = img
            part['files'].append({'file': filename, 'input_sha256': digest(input_path),
                                  'output_sha256': digest(output / filename), 'tile_pixels': int(selection.sum()),
                                  'pixels': len(selection), 'untouched_non_tile_pixels': True})
            bpy.data.images.remove(mask)
        report['parts'].append(part)
        mesh = work.data
        bpy.data.objects.remove(work, do_unlink=True)
        bpy.data.meshes.remove(mesh)
        print('ESP_HEATSHIELD_PART_OK', name, flush=True)
    (output / 'heatshield-correction.json').write_text(json.dumps(report, indent=2) + '\n')
    bpy.ops.wm.save_as_mainfile(filepath=str(output / 'ESP-Starship-ceramic.blend'))
    print('ESP_HEATSHIELD_CORRECTION_OK', flush=True)


if __name__ == '__main__':
    main()
