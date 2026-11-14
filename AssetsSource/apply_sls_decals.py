"""Add reference-based paint/decal overlays to the preserved NASA SLS mesh.

PRIVATE REVIEW OUTPUT, not a representation that NASA logo publication is cleared.
  blender --disable-autoexec -b --python apply_sls_decals.py -- INPUT.blend ARTWORK_DIR OUTPUT_DIR [--include-nasa-logos] [--no-render]
ARTWORK_DIR: nasa-worm-guide.png and nasa-meatball.png (official sources in docs).
No rocket vertices are moved or replaced. Thin overlays are ray-projected onto
existing source surfaces. Height/azimuth estimates are recorded in the manifest.
"""
import argparse
import json
import math
import pathlib
import sys

REFERENCES = {
    'front': 'https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220318-ph-kls03_0079large.jpg',
    'aft_detail': 'https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220316-ph-geb01_0030large.jpg',
    'upper_detail': 'https://www.nasa.gov/wp-content/uploads/2023/03/ksc-20220421-ph-jbs01_0035large.jpg',
    'worm': 'https://www.nasa.gov/wp-content/uploads/2023/03/artemis_i_boosters_with_nasa_worm.jpg',
    'upper_illustration': 'https://www.nasa.gov/wp-content/uploads/2023/03/lower_case_usa_500h.jpg',
    'guide': 'https://www.nasa.gov/wp-content/uploads/2022/03/sls-reference-guide-2022-v2-508-0.pdf',
}


def main():
    import bpy
    from mathutils import Vector
    from mathutils.bvhtree import BVHTree
    args_in = sys.argv[sys.argv.index('--') + 1:]
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('blend', type=pathlib.Path)
    parser.add_argument('artwork', type=pathlib.Path)
    parser.add_argument('output', type=pathlib.Path)
    parser.add_argument('--include-nasa-logos', action='store_true', help='Include logo artwork in this private review only; does not establish publication clearance')
    parser.add_argument('--no-render', action='store_true')
    args = parser.parse_args(args_in)
    args.blend, args.artwork, args.output = (p.resolve() for p in (args.blend, args.artwork, args.output))
    args.output.mkdir(parents=True, exist_ok=True)
    bpy.ops.wm.open_mainfile(filepath=str(args.blend), load_ui=False, use_scripts=False)
    scene = bpy.context.scene
    prefixes = ('CoreAssembly__NASA_', 'UpperAssembly__NASA_', 'LeftSRB__NASA_', 'RightSRB__NASA_')
    originals = [o for o in scene.objects if o.type == 'MESH' and o.name.startswith(prefixes)]
    if len(originals) != 36:
        raise ValueError('Expected the 36 preserved NASA flight mesh components')
    core = scene.objects['CoreAssembly__NASA_00']
    upper = scene.objects['UpperAssembly__NASA_03']
    boosters = [(scene.objects['LeftSRB__NASA_01'], (-6.994925, -.17536), math.pi),
                (scene.objects['RightSRB__NASA_02'], (6.994925, -.17536), 0)]
    initial_topology = {o.name: (len(o.data.vertices), len(o.data.polygons)) for o in originals}
    initial_positions = {o.name: tuple(tuple(v.co) for v in o.data.vertices) for o in originals}
    # Correct two broad paint regions visible in the actual Artemis I photos.
    orange = next(m for m in bpy.data.materials if m.name.startswith('Core foam orange'))
    white = next(m for m in bpy.data.materials if m.name.startswith('Booster ceramic white'))
    for obj, threshold, mat in ((core, 7.0, white), (upper, 72.15, orange)):
        if mat.name not in obj.data.materials:
            obj.data.materials.append(mat)
        slot = list(obj.data.materials).index(mat)
        for poly in obj.data.polygons:
            # Keep triangles crossing the aft paint boundary orange; a
            # projected white paint layer below supplies a straight edge.
            below = all(obj.data.vertices[i].co.z < threshold for i in poly.vertices) if obj == core else poly.center.z < threshold
            if below:
                poly.material_index = slot
    for mat in (orange,):
        mat.diffuse_color = (.62, .22, .035, 1)
        mat.node_tree.nodes.get('Principled BSDF').inputs['Base Color'].default_value = mat.diffuse_color
        mat.node_tree.nodes.get('Principled BSDF').inputs['Roughness'].default_value = .68

    # Atlas pixels are engineering patterns plus a correctly proportioned US
    # flag. Optional NASA glyphs are sampled from official artwork, not redrawn.
    size = 1024
    pixels = [0.0] * (size * size * 4)
    regions = {}

    def pixel(x, y, rgba):
        offset = (y * size + x) * 4
        pixels[offset:offset + 4] = rgba

    def fill(rect, color):
        x, y, width, height = rect
        for yy in range(y, y + height):
            for xx in range(x, x + width):
                pixel(xx, yy, color)

    def region(name, rect):
        regions[name] = rect
        return rect

    black = (0.012, .014, .018, 1)
    red = (.65, .015, .035, 1)
    blue = (.018, .038, .16, 1)
    pure_white = (1, 1, 1, 1)
    fill(region('black', (16, 16, 24, 24)), black)
    fill(region('white', (48, 16, 24, 24)), pure_white)
    checker = region('checker', (16, 64, 480, 80))
    for y in range(80):
        for x in range(480):
            pixel(16 + x, 64 + y, black if (x // 40 + y // 40) % 2 == 0 else pure_white)
    target = region('target', (520, 16, 96, 96))
    for y in range(96):
        for x in range(96):
            pixel(520 + x, 16 + y, black if (x // 48 + y // 48) % 2 == 0 else pure_white)
    flag = region('flag', (16, 176, 456, 240))
    for y in range(240):
        for x in range(456):
            stripe_from_top = min(12, int((239 - y) * 13 / 240))
            color = red if stripe_from_top % 2 == 0 else pure_white
            if x < round(456 * .4) and y >= round(240 * 6 / 13):
                color = blue
            pixel(16 + x, 176 + y, color)

    def inside_polygon(x, y, points):
        result = False
        last = len(points) - 1
        for current in range(len(points)):
            ax, ay = points[current]
            bx, by = points[last]
            if (ay > y) != (by > y) and x < (bx - ax) * (y - ay) / (by - ay) + ax:
                result = not result
            last = current
        return result

    canton_w, canton_h = 456 * .4, 240 * 7 / 13
    for row in range(9):
        for column in range(6 if row % 2 == 0 else 5):
            cx = (column * 2 + (1 if row % 2 == 0 else 2)) * canton_w / 12
            cy = 240 - (row + 1) * canton_h / 10
            radius = 240 * .0308
            points = [(cx + math.sin(i * math.pi / 5) * (radius if i % 2 == 0 else radius * .382),
                       cy + math.cos(i * math.pi / 5) * (radius if i % 2 == 0 else radius * .382)) for i in range(10)]
            for y in range(int(cy - radius) - 1, int(cy + radius) + 2):
                for x in range(int(cx - radius) - 1, int(cx + radius) + 2):
                    if inside_polygon(x + .5, y + .5, points):
                        pixel(16 + x, 176 + y, pure_white)

    artwork_records = []
    if args.include_nasa_logos:
        worm_path = args.artwork / 'nasa-worm-guide.png'
        meatball_path = args.artwork / 'nasa-meatball.png'
        if not worm_path.exists() or not meatball_path.exists():
            raise FileNotFoundError('Use the official artwork files specified in docs/sls-decals.md')
        worm_image = bpy.data.images.load(str(worm_path), check_existing=False)
        wp = list(worm_image.pixels)
        ww, wh = worm_image.size
        # White-on-black lower-left official variant: isolate the supplied
        # glyph through its luminance, preserving its exact drawn contours.
        wr = region('worm', (16, 456, 672, 190))
        for y in range(wr[3]):
            for x in range(wr[2]):
                sx = min(ww - 1, 143 + x)
                sy = max(0, wh - 1 - (746 - y))
                alpha = wp[(sy * ww + sx) * 4]
                pixel(wr[0] + x, wr[1] + y, (.8, .005, .025, alpha))
        meatball_image = bpy.data.images.load(str(meatball_path), check_existing=False)
        mp = list(meatball_image.pixels)
        mw, mh = meatball_image.size
        mr = region('meatball', (660, 32, 330, 276))
        for y in range(mr[3]):
            for x in range(mr[2]):
                sx, sy = min(mw - 1, int(x * mw / mr[2])), min(mh - 1, int(y * mh / mr[3]))
                pixel(mr[0] + x, mr[1] + y, mp[(sy * mw + sx) * 4:(sy * mw + sx) * 4 + 4])
        artwork_records = [dict(file='nasa-worm-guide.png', source='https://www.nasa.gov/wp-content/uploads/2023/07/nasa-logotype-protectedspace.png', operation='isolate official white glyph, use official red on the model'),
                           dict(file='nasa-meatball.png', source='https://www.nasa.gov/wp-content/themes/nasa/assets/images/nasa-logo.svg', operation='rasterize official SVG; preserve geometry and colors')]
    atlas = bpy.data.images.new('SLS-Livery-Review-Atlas', width=size, height=size, alpha=True)
    atlas.pixels.foreach_set(pixels)
    atlas.filepath_raw = str(args.output / 'SLS-Livery-Review-Atlas.png')
    atlas.file_format = 'PNG'
    atlas.save()
    atlas.pack()
    atlas.filepath = str((args.output / 'SLS-Livery-Review-Atlas.png').resolve())
    decal_mat = bpy.data.materials.new('SLS Livery Review Atlas')
    decal_mat.use_nodes = True
    nodes = decal_mat.node_tree.nodes
    bsdf = nodes.get('Principled BSDF')
    bsdf.inputs['Roughness'].default_value = .60
    tex = nodes.new('ShaderNodeTexImage')
    tex.image = atlas
    tex.interpolation = 'Linear'
    decal_mat.node_tree.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    decal_mat.node_tree.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    if hasattr(decal_mat, 'surface_render_method'):
        decal_mat.surface_render_method = 'DITHERED'
    decal_mat.use_backface_culling = True
    collection = bpy.data.collections.new('LIVERY REVIEW - source-based major external markings')
    scene.collection.children.link(collection)
    decal_objects, records = [], []
    bvh = {obj.name: BVHTree.FromPolygons([obj.matrix_world @ v.co for v in obj.data.vertices],
                                         [tuple(p.vertices) for p in obj.data.polygons], all_triangles=True)
           for obj in (core, upper, boosters[0][0], boosters[1][0])}

    def project(target_obj, center, angle, height):
        normal = Vector((math.cos(angle), math.sin(angle), 0))
        origin = Vector((center[0], center[1], height)) + normal * 15
        hit, hit_normal, _, _ = bvh[target_obj.name].ray_cast(origin, -normal, 20)
        if hit is None:
            raise ValueError(f'Decal ray missed {target_obj.name} at height {height:.3f}')
        return tuple(hit + hit_normal * .012)

    def patch(target_obj, center, angle, height, width, depth, atlas_name, name, reference,
              rotate=False, radius=None, confidence='photo-estimated major marking'):
        if radius is None:
            hit = Vector(project(target_obj, center, angle, height))
            radius = math.hypot(hit.x - center[0], hit.y - center[1])
        count_x = max(2, int(width / .10))
        count_y = max(2, int(depth / .25))
        # Keep sufficient curvature samples without exploding the draw mesh.
        count_x, count_y = min(count_x, 160), min(count_y, 48)
        verts, faces, vertex_uvs = [], [], []
        rx, ry, rw, rh = regions[atlas_name]
        for iy in range(count_y + 1):
            v = iy / count_y
            z = height + (v - .5) * depth
            for ix in range(count_x + 1):
                u = ix / count_x
                theta = angle + (u - .5) * width / radius
                verts.append(project(target_obj, center, theta, z))
                tu, tv = (v, 1 - u) if rotate else (u, v)
                vertex_uvs.append(((rx + .5 + tu * (rw - 1)) / size, (ry + .5 + tv * (rh - 1)) / size))
        for iy in range(count_y):
            for ix in range(count_x):
                a = iy * (count_x + 1) + ix
                faces.append((a, a + 1, a + count_x + 2, a + count_x + 1))
        mesh = bpy.data.meshes.new(name)
        mesh.from_pydata(verts, [], faces)
        mesh.materials.append(decal_mat)
        mesh.update()
        uv = mesh.uv_layers.new(name='Livery atlas')
        for poly in mesh.polygons:
            poly.use_smooth = True
            for loop in poly.loop_indices:
                uv.data[loop].uv = vertex_uvs[mesh.loops[loop].vertex_index]
        prefix = target_obj.name.split('__')[0]
        obj = bpy.data.objects.new(prefix + '__Decal_' + name, mesh)
        collection.objects.link(obj)
        obj['reference'] = reference
        obj['placement_quality'] = confidence
        decal_objects.append(obj)
        records.append(dict(name=obj.name, source_target=target_obj.name, height_m=height,
                            angle_degrees=math.degrees(angle), size_m=[width, depth],
                            artwork=atlas_name, reference=reference, confidence=confidence))
        return obj

    front = -math.pi / 2
    core_floor = min(v.co.z for v in core.data.vertices) + .025
    patch(core, (0, 0), 0, (core_floor + 7.0) / 2, 2 * math.pi * 4.2,
          7.0 - core_floor, 'white', 'Core_Aft_White_Paint', REFERENCES['aft_detail'], radius=4.2)
    # Front-view NASA photograph and the guide's Block 1 elevation establish
    # the large bands, checker targets and vertical roll markings. Their
    # meter positions below are photo-based estimates, not survey coordinates.
    for booster, center, outward in boosters:
        side = booster.name.split('__')[0]
        # Full circumferential segment boundary lines seen in both views.
        for i, z in enumerate((6.0, 9.0, 14.8, 19.3, 23.8, 28.4, 32.9, 37.4, 41.9, 46.4)):
            patch(booster, center, 0, z, 2 * math.pi * 1.90, .12, 'black', f'{side}_Band_{i}', REFERENCES['front'], radius=1.90)
        # Narrow dark nose-base band visible on the left booster in the front
        # reference. Do not invent a matching broad band on the right booster.
        if side == 'LeftSRB':
            patch(booster, center, 0, 49.2, 2 * math.pi * 1.90, .42, 'black', side + '_NoseBand', REFERENCES['front'], radius=1.90)
        for i, z in enumerate((12.0, 30.6, 44.4)):
            patch(booster, center, front, z, 3.30, .60, 'checker', f'{side}_Checker_{i}', REFERENCES['front'])
        for i, z in enumerate((17.1, 21.6, 26.1, 35.2, 39.7)):
            patch(booster, center, front + (.12 if i % 2 else -.12), z, .32, 2.8, 'black', f'{side}_RollBar_{i}', REFERENCES['front'])
        for i, z in enumerate((10.5, 45.8, 52.0)):
            patch(booster, center, front, z, .8 if z < 50 else .5, .8 if z < 50 else .5,
                  'target', f'{side}_Target_{i}', REFERENCES['front'])
        # Viewed from the aft reference, additional diagonal targets are on
        # the aft skirts. Do not mirror the entire front livery onto the back.
        patch(booster, center, math.pi / 2, 2.8, .8, .8, 'target', side + '_AftSkirtTarget', REFERENCES['aft_detail'])
        if args.include_nasa_logos:
            patch(booster, center, outward, 26.1, 2.53, 8.75, 'worm', side + '_NASA_Worm', REFERENCES['worm'], rotate=True)
    patch(core, (0, 0), 0, 46.2, 2 * math.pi * 4.2, .35, 'white', 'Core_Intertank_Band', REFERENCES['front'], radius=4.2)
    for i, z in enumerate((25.4, 64.0)):
        patch(core, (0, 0), front, z, .9, .9, 'target', f'Core_Target_{i}', REFERENCES['front'])
    patch(core, (0, 0), math.pi / 2, 5.3, .9, .9, 'target', 'Core_Aft_Engine_Target', REFERENCES['aft_detail'])
    patch(upper, (0, 0), front + .35, 74.2, 1.5, 1.5 / 1.9, 'flag', 'ICPS_US_Flag', REFERENCES['upper_detail'])
    patch(upper, (0, 0), front, 86.0, 1.25, 1.25 / 1.9, 'flag', 'LAS_US_Flag', REFERENCES['upper_illustration'])
    patch(upper, (0, 0), front, 76.4, .65, .65, 'target', 'Upper_Adapter_Target', REFERENCES['upper_detail'])
    # Flat letters are projected onto the existing shroud, preserving the
    # observed text rather than inventing a large USA/SLS wordmark on the tank.
    bpy.ops.object.select_all(action='DESELECT')
    font = bpy.data.curves.new('United States shroud text', 'FONT')
    font.body = 'United States'
    font.align_x = 'CENTER'
    font.size = 1
    font.resolution_u = 5
    text_obj = bpy.data.objects.new('UpperAssembly__Decal_United_States', font)
    collection.objects.link(text_obj)
    bpy.context.view_layer.objects.active = text_obj
    text_obj.select_set(True)
    bpy.ops.object.convert(target='MESH')
    text_obj = bpy.context.object
    local = [v.co.copy() for v in text_obj.data.vertices]
    xlo, xhi = min(v.x for v in local), max(v.x for v in local)
    ylo, yhi = min(v.y for v in local), max(v.y for v in local)
    text_scale = 1.65 / (xhi - xlo)
    for vertex, original in zip(text_obj.data.vertices, local):
        horizontal = (original.x - (xlo + xhi) / 2) * text_scale
        z = 85.35 + (original.y - (ylo + yhi) / 2) * text_scale
        vertex.co = project(upper, (0, 0), front + horizontal / 2.1, z)
    text_obj.data.update()
    # Text conversion can preserve a back-facing cap. Orient the actual
    # lettering toward its radial exterior before enabling face culling.
    import bmesh
    text_bm = bmesh.new()
    text_bm.from_mesh(text_obj.data)
    text_bm.normal_update()
    for face in text_bm.faces:
        radial = face.calc_center_median()
        radial.z = 0
        if face.normal.dot(radial) < 0:
            face.normal_flip()
    text_bm.to_mesh(text_obj.data)
    text_bm.free()
    text_obj.data.update()
    text_obj.data.materials.append(decal_mat)
    uv = text_obj.data.uv_layers.new(name='Livery atlas')
    for loop in uv.data:
        loop.uv = ((16 + 12) / size, (16 + 12) / size)
    decal_objects.append(text_obj)
    records.append(dict(name=text_obj.name, text='United States', height_m=85.35,
                        reference=REFERENCES['upper_illustration'], confidence='reference text, photo-estimated scale; Blender font is an approximation'))
    if args.include_nasa_logos:
        patch(upper, (0, 0), front, 80.0, 2.0, 2.0 * 92 / 110, 'meatball', 'Orion_NASA_Insignia', REFERENCES['upper_detail'])
        patch(upper, (0, 0), -.25, 82.5, 2.1, .594, 'worm', 'Orion_CMA_Worm', REFERENCES['upper_illustration'])

    for obj in originals:
        if initial_topology[obj.name] != (len(obj.data.vertices), len(obj.data.polygons)):
            raise AssertionError('Original topology changed: ' + obj.name)
        if initial_positions[obj.name] != tuple(tuple(v.co) for v in obj.data.vertices):
            raise AssertionError('Original vertices changed: ' + obj.name)
    manifest = dict(status='PRIVATE REVIEW; publication of NASA identifiers not cleared by this script',
                    include_nasa_logos=args.include_nasa_logos, source_geometry_unchanged=True,
                    references=REFERENCES, artwork=artwork_records, markings=records,
                    paint_changes=['core aft engine section white below 7m', 'LVSA foam orange below 72.15m',
                                   'slightly darker orange foam visualization'],
                    limitations=['Not a complete photogrammetric survey; small/internal targets omitted',
                                 'Mark dimensions/heights/azimuths estimated from cited photographs',
                                 'Source printable mesh differs in detail from flight hardware',
                                 'No unverified giant USA or SLS logo added to orange tank',
                                 'ESA and mission emblems are not supplied; separate artwork/permission review needed',
                                 'United States lettering uses approximate Blender font'],
                    unity='Use atlas RGB and alpha with URP/Lit alpha clipping; preserve UVs and group prefixes. Do not replace this material with the plain color palette.')
    (args.output / 'SLS-Livery-Review.json').write_text(json.dumps(manifest, indent=2) + '\n')
    bpy.ops.object.select_all(action='DESELECT')
    for obj in originals + decal_objects:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = core
    # Packed images may otherwise resolve as a basename against the input
    # blend directory, causing an exporter missing-source warning.
    atlas.filepath = str((args.output / 'SLS-Livery-Review-Atlas.png').resolve())
    if not pathlib.Path(bpy.path.abspath(atlas.filepath)).is_file():
        raise FileNotFoundError('Atlas PNG missing before OBJ export')
    bpy.ops.wm.obj_export(filepath=str(args.output / 'SLS-Block1-LiveryReview.obj'),
                          export_selected_objects=True, forward_axis='NEGATIVE_Z', up_axis='Y',
                          export_materials=True, export_normals=True, export_uv=True,
                          export_object_groups=True, export_triangulated_mesh=True, path_mode='RELATIVE')
    scene['livery_review_status'] = manifest['status']
    scene.render.filepath = str(args.output / 'SLS-Block1-LiveryReview.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(args.output / 'SLS-Block1-LiveryReview.blend'))
    if not args.no_render:
        bpy.ops.render.render(write_still=True)
        camera = scene.camera
        previous = camera.location.copy()
        camera.location = (-110, 230, 110)
        camera.rotation_euler = (Vector((0, 0, 48)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
        scene.render.filepath = str(args.output / 'SLS-Block1-LiveryReview-rear.png')
        bpy.ops.render.render(write_still=True)
        camera.location = previous
        camera.rotation_euler = (Vector((0, 0, 48)) - camera.location).to_track_quat('-Z', 'Y').to_euler()
    print('SLS_LIVERY_PRIVATE_REVIEW_READY', json.dumps(dict(markings=len(records), original_vertices_unchanged=True,
                                                          nasa_identifiers=args.include_nasa_logos)), flush=True)


if __name__ == '__main__':
    main()
