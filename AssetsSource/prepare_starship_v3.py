"""Source-faithful Clarence365 V3 adaptation, texture baking and Unity export.

blender --disable-autoexec --background --python prepare_starship_v3.py -- \
  SOURCE.blend OUTPUT_DIR --texture-dir TEXTURES

No rocket geometry is generated. Existing meshes are copied, evaluated, UV-atlased
for PBR baking, uniformly scaled, and grouped. Original meshes/materials/UVs remain
in an archived scene in the saved editable blend. Source filename 'V4' is the
artist's asset revision; the vehicle depicted is Block 3, not Starship V4.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path
import re
import shutil
import sys

SOURCE_SHA256 = '5a8003c070f9c0211f622d11c1fab421e04ab4c1f3787c1d5d030b87df7195f6'
SOURCE_URL = 'https://sketchfab.com/3d-models/spacex-starship-block-3-6f6c6f88a3eb4b4d822fdca66733fbb2'
ROOTS = {'Starship Block 3 V4': 'Ship', 'Superheavy Block 3 V4': 'Booster'}


def safe(name):
    return re.sub('[^A-Za-z0-9_]+', '_', name).strip('_')


def texture_key(name):
    name = re.sub(r'\.\d{3}$', '', name.replace('\\', '/').rsplit('/', 1)[-1])
    return re.sub('[^a-z0-9]', '', Path(name).stem.casefold()) + Path(name).suffix.casefold().replace('.jpeg', '.jpg')


def main():
    import bpy
    import numpy as np
    from mathutils import Matrix, Vector
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('source', type=Path);parser.add_argument('output', type=Path)
    parser.add_argument('--texture-dir', type=Path, required=True)
    parser.add_argument('--body-resolution', type=int, default=2048)
    parser.add_argument('--detail-resolution', type=int, default=512)
    parser.add_argument('--no-render', action='store_true')
    parser.add_argument('--no-bake', action='store_true', help='Geometry/anchor diagnostic only; not a shipping export')
    args = parser.parse_args(sys.argv[sys.argv.index('--')+1:])
    source, output = args.source.resolve(), args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    digest = hashlib.sha256(source.read_bytes()).hexdigest()
    if digest != SOURCE_SHA256:
        raise ValueError('Unexpected source revision; inspect it before adaptation')
    bpy.ops.wm.open_mainfile(filepath=str(source), load_ui=False, use_scripts=False)
    original = bpy.context.scene
    original.name = 'SOURCE ORIGINAL - Clarence365 Block 3 CC BY 4.0'
    frame = original.frame_current
    textures = {texture_key(p.name): p.resolve() for p in args.texture_dir.rglob('*') if p.is_file()}
    relinks = []
    for img in bpy.data.images:
        path = textures.get(texture_key(img.filepath)) or textures.get(texture_key(img.name))
        if path and not img.packed_file:
            img.filepath = str(path);img.reload()
            if len(img.pixels): _ = img.pixels[0]
            relinks.append({'image': img.name, 'file': path.name, 'loaded': bool(img.has_data)})
    used = {n.image for m in bpy.data.materials if m.use_nodes for n in m.node_tree.nodes if getattr(n, 'image', None)}
    missing = [img.name for img in used if not img.has_data]
    if missing: raise ValueError('Material images missing: ' + ', '.join(missing))
    print('V3_TEXTURES_OK', json.dumps(relinks), flush=True)
    for name in ROOTS:
        if original.objects.get(name) is None: raise ValueError('Missing reviewed source root: '+name)
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    sources = []
    for obj in original.objects:
        if obj.type != 'MESH' or obj.hide_render: continue
        root = obj
        while root.parent: root = root.parent
        if root.name not in ROOTS: raise ValueError('Unreviewed render mesh: '+obj.name)
        sources.append((obj, ROOTS[root.name]))
    if len(sources) != 29:
        # Inventory is pinned, but a diagnostic is more useful than silently
        # dropping a newly discovered part. Expected count is verified below.
        print('V3_SOURCE_MESH_COUNT', len(sources), flush=True)

    def mesh_world(obj):
        mesh = bpy.data.meshes.new_from_object(obj.evaluated_get(deps), preserve_all_data_layers=True, depsgraph=deps)
        points = np.array([obj.matrix_world @ v.co for v in mesh.vertices], dtype=np.float64)
        return mesh, points
    source_geometry = {obj.name: mesh_world(obj) for obj, _ in sources}
    booster = original.objects['Superheavy Block 3 V4']
    bp = source_geometry[booster.name][1]
    # Fit the dominant cylindrical tank skin at mid-height, not overall fin or
    # piping bounds. The fixed 9 m target is applied as a single uniform scale.
    center_guess = np.array(booster.matrix_world.translation[:2], dtype=np.float64)
    band = bp[(bp[:,2] > 5) & (bp[:,2] < 11)]
    radii = np.linalg.norm(band[:,:2] - center_guess, axis=1)
    bins = np.round(radii / .002).astype(int)
    mode = np.bincount(bins).argmax() * .002
    skin = band[np.abs(radii-mode) < .004,:2]
    def fit_circle(xy):
        matrix = np.column_stack([2*xy[:,0], 2*xy[:,1], np.ones(len(xy))])
        cx,cy,k = np.linalg.lstsq(matrix, np.sum(xy*xy,axis=1), rcond=None)[0]
        r = math.sqrt(max(0,k+cx*cx+cy*cy))
        error = float(np.max(np.abs(np.linalg.norm(xy-[cx,cy],axis=1)-r)))
        return np.array([cx,cy]),r,error
    center, barrel_radius, barrel_error = fit_circle(skin)
    if not (.85 < barrel_radius < 1.0) or barrel_error > .01:
        raise ValueError('Source barrel fit requires inspection: '+str((barrel_radius,barrel_error)))
    scale = 4.5 / barrel_radius
    all_points = np.concatenate([points for _,points in source_geometry.values()])
    base = float(all_points[:,2].min())
    shift = Vector((-float(center[0]), -float(center[1]), -base))
    transform = Matrix.Scale(scale,4) @ Matrix.Translation(shift)
    height = float((all_points[:,2].max()-base)*scale)

    def nozzle_loops(obj, expected):
        mesh, points = source_geometry[obj.name]
        floor = float(points[:,2].min())
        # Source vacuum bells are slightly non-level. Expand the inspected lip
        # band until a COMPLETE connected circumference is present; fit its
        # plane rather than assuming that the lowest six vertices define a lip.
        attempts=[]
        for tolerance in (.00015,.0003,.0006,.0012,.0024,.0048,.0096,.0192,.0384):
            eligible=set(np.flatnonzero(points[:,2] <= floor+tolerance).tolist())
            adjacency={i:[] for i in eligible}
            for edge in mesh.edges:
                a,b=edge.vertices
                if a in eligible and b in eligible:
                    adjacency[a].append(b);adjacency[b].append(a)
            remaining=set(eligible);circles=[]
            while remaining:
                seed=remaining.pop();stack=[seed];component=[seed]
                while stack:
                    for j in adjacency[stack.pop()]:
                        if j in remaining:
                            remaining.remove(j);stack.append(j);component.append(j)
                if len(component)<16:continue
                xyz=points[component]
                origin=xyz.mean(axis=0)
                _,_,axes=np.linalg.svd(xyz-origin,full_matrices=False)
                local=(xyz-origin)@axes[:2].T
                c,r,e=fit_circle(local)
                angles=np.sort(np.arctan2(local[:,1]-c[1],local[:,0]-c[0]))
                max_gap=float(np.diff(np.r_[angles,angles[0]+2*math.pi]).max())
                planar=float(np.abs((xyz-origin)@axes[2]).max())
                if .025 < r < .4 and e < max(.0004,r*.025) and max_gap < .45 and planar < max(.0004,r*.035):
                    center3=origin+c@axes[:2]
                    normal=axes[2] if axes[2,2]<0 else -axes[2]
                    circles.append({'center':center3,'radius':r,'error':e,'plane_error':planar,
                                    'normal':normal,'vertices':len(component),'max_angle_gap':max_gap})
            groups=[]
            for circle in circles:
                match=next((g for g in groups if np.linalg.norm(g[0]['center']-circle['center'])<.008),None)
                if match is None:groups.append([circle])
                else:match.append(circle)
            attempts.append({'band':tolerance,'vertices':len(eligible),'openings':len(groups)})
            if len(groups)==expected:break
        diagnostic={'object':obj.name,'floor':floor,'attempts':attempts,
                    'loops':[{k:(v.tolist() if isinstance(v,np.ndarray) else v) for k,v in c.items()} for c in circles],
                    'openings':len(groups),'expected':expected}
        print('V3_NOZZLE_SCAN',json.dumps(diagnostic),flush=True);scans.append(diagnostic)
        (output/'V3-nozzle-scan.json').write_text(json.dumps(scans,indent=2)+'\n')
        if len(groups)!=expected:
            raise ValueError('Nozzle loop count mismatch for '+obj.name+'; inspect V3-nozzle-scan.json')
        result=[]
        for group in groups:
            ring=min(group,key=lambda c:c['radius'])
            result.append(ring)
        return sorted(result,key=lambda r:math.atan2(r['center'][1]-center[1],r['center'][0]-center[0]))
    scans=[];anchors=[]
    for obj,stage in sources:
        count=20 if obj.name=='Superheavy Block 3 V4' else 1 if obj.name.startswith('Raptor 3') else 0
        if not count: continue
        for number,ring in enumerate(nozzle_loops(obj,count)):
            p=transform@Vector(ring['center'])
            normal=ring['normal'];radius=ring['radius']
            anchors.append({'name':stage+'__'+safe(obj.name)+'_nozzle_'+str(number+1), 'stage':stage,
                            'source_object':obj.name,'unity_position_m':[float(p.x),float(p.z),float(-p.y)],
                            'opening_radius_m':radius*scale,
                            'unity_exhaust_direction':[float(normal[0]),float(normal[2]),float(-normal[1])],
                            'engine_kind':'Vacuum' if 'Vaccum' in obj.name else 'SeaLevel'})
    counts={stage:sum(a['stage']==stage for a in anchors) for stage in ROOTS.values()}
    if counts!={'Ship':6,'Booster':33}:raise ValueError('Incorrect measured source engine counts: '+str(counts))
    manifest={'source_url':SOURCE_URL,'author':'Clarence365','license':'https://creativecommons.org/licenses/by/4.0/',
              'source_sha256':digest,'blender_version':bpy.app.version_string,'source_frame':frame,
              'height_m':height,'barrel_diameter_m':9,'source_barrel_radius':barrel_radius,
              'barrel_fit_max_error_source_units':barrel_error,'meters_per_source_unit':scale,
              'ship_group_base_m':float((source_geometry['Starship Block 3 V4'][1][:,2].min()-base)*scale),
              'nozzle_anchors':anchors,'visual_engine_counts':counts,'texture_relinks':relinks,
              'materials':[],'parts':[],
              'modifications':['Supplied image path recovery','Uniform scale from measured tank barrel to 9 m',
                               'Existing stage hierarchy mapped to Booster__/Ship__ groups',
                               'Existing material inputs baked to PBR maps on a separate runtime UV layer'],
              'limitations':['Artist model, not a SpaceX engineering survey.',
                             'Source S45/RBXX markings retained; not matched to a named flown mission.',
                             'Static PBR bake omits unused source reentry-emission controls.',
                             'Exact fin steering angles and source dimensions beyond barrel diameter remain artistic.']}
    (output/'ESP-Starship-import.json').write_text(json.dumps(manifest,indent=2)+'\n')
    print('V3_GEOMETRY_VERIFIED',json.dumps({'height_m':height,'counts':counts,'scale':scale}),flush=True)
    if args.no_bake:return

    # Archive source exactly as authored apart from recovered file paths. The
    # adapted scene contains evaluated copies and editable original UV layers.
    scene=bpy.data.scenes.new('ESP Clarence365 V3 runtime adaptation');bpy.context.window.scene=scene
    scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
    scene.frame_set(frame);scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=1
    scene.render.bake.use_clear=True;scene.render.bake.margin=8
    scene.render.bake.use_selected_to_active=False
    runtime=[];textures_out=output/'Textures';textures_out.mkdir(exist_ok=True)
    def select(obj):
        bpy.ops.object.select_all(action='DESELECT');obj.select_set(True);bpy.context.view_layer.objects.active=obj
    def image(name,resolution,noncolor):
        img=bpy.data.images.new(name,width=resolution,height=resolution,alpha=True)
        img.colorspace_settings.name='Non-Color' if noncolor else 'sRGB'
        img.file_format='PNG';img.filepath_raw=str(textures_out/(name+'.png'))
        return img
    for source_obj,stage in sources:
        mesh=source_geometry[source_obj.name][0]
        name=stage+'__'+safe(source_obj.name)
        obj=bpy.data.objects.new(name,mesh);scene.collection.objects.link(obj)
        obj.matrix_world=source_obj.matrix_world.copy();obj['source_object']=source_obj.name
        select(obj)
        originals=list(mesh.materials);mesh.materials.clear()
        mats=[]
        source_uv=mesh.uv_layers.active.name if mesh.uv_layers.active else None
        if source_uv is None:raise ValueError('Source UV layer absent: '+source_obj.name)
        for mat in originals:
            copy=mat.copy();copy.name='BAKE_'+name+'_'+safe(mat.name)
            mesh.materials.append(copy);mats.append(copy)
            # Lock implicit image coordinates to the original UV layer before
            # adding a second layer for the destination atlas.
            uv=copy.node_tree.nodes.new('ShaderNodeUVMap');uv.uv_map=source_uv
            for node in list(copy.node_tree.nodes):
                if node.type=='TEX_IMAGE' and not node.inputs['Vector'].is_linked:
                    copy.node_tree.links.new(uv.outputs['UV'],node.inputs['Vector'])
        mesh.uv_layers.new(name='ESP_RuntimeUV');mesh.uv_layers.active_index=len(mesh.uv_layers)-1
        bpy.ops.object.mode_set(mode='EDIT');bpy.ops.mesh.select_all(action='SELECT')
        bpy.ops.uv.smart_project(angle_limit=math.radians(66),island_margin=.012)
        bpy.ops.object.mode_set(mode='OBJECT')
        # Texture sampling remains on original UV; bake destination is explicit.
        mesh.uv_layers[source_uv].active_render=True
        resolution=args.body_resolution if source_obj.name in ROOTS else args.detail_resolution
        if 'Flap' in source_obj.name:resolution=max(1024,resolution)
        base_img=image('V3_'+safe(source_obj.name)+'_BaseColor',resolution,False)
        packed_img=image('V3_'+safe(source_obj.name)+'_MetallicSmoothness',min(resolution,1024),True)
        normal_img=image('V3_'+safe(source_obj.name)+'_Normal',min(resolution,1024),True)
        states=[]
        for mat in mats:
            nodes=mat.node_tree.nodes;links=mat.node_tree.links
            output_node=next(n for n in nodes if n.type=='OUTPUT_MATERIAL' and n.is_active_output)
            original_surface=output_node.inputs['Surface'].links[0].from_socket
            bsdf=nodes.get('Principled BSDF')
            if bsdf is None:raise ValueError('Unreviewed material shader: '+mat.name)
            target=nodes.new('ShaderNodeTexImage');target.image=base_img;nodes.active=target
            emit=nodes.new('ShaderNodeEmission');emit.inputs['Strength'].default_value=1
            states.append((mat,output_node,original_surface,bsdf,target,emit))
        def connect_input(mat,source_socket,target_socket):
            if source_socket.is_linked:mat.node_tree.links.new(source_socket.links[0].from_socket,target_socket)
            else:target_socket.default_value=source_socket.default_value
        # Baking unlit material inputs avoids lighting and metal/diffuse bias.
        for mat,out,old,bsdf,target,emit in states:
            connect_input(mat,bsdf.inputs['Base Color'],emit.inputs['Color'])
            mat.node_tree.links.new(emit.outputs[0],out.inputs['Surface'])
        bpy.ops.object.bake(type='EMIT',uv_layer='ESP_RuntimeUV');base_img.save()
        for mat,out,old,bsdf,target,emit in states:
            target.image=packed_img
            combine=mat.node_tree.nodes.new('ShaderNodeCombineXYZ')
            connect_input(mat,bsdf.inputs['Metallic'],combine.inputs['X'])
            invert=mat.node_tree.nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
            connect_input(mat,bsdf.inputs['Roughness'],invert.inputs[1])
            mat.node_tree.links.new(invert.outputs[0],combine.inputs['Z'])
            mat.node_tree.links.new(combine.outputs[0],emit.inputs['Color'])
        bpy.ops.object.bake(type='EMIT',uv_layer='ESP_RuntimeUV')
        pixels=np.empty(len(packed_img.pixels),dtype=np.float32);packed_img.pixels.foreach_get(pixels)
        pixels=pixels.reshape(-1,4);pixels[:,3]=pixels[:,2];pixels[:,1]=1;pixels[:,2]=0
        packed_img.pixels.foreach_set(pixels.ravel());packed_img.save()
        for mat,out,old,bsdf,target,emit in states:
            target.image=normal_img;mat.node_tree.links.new(old,out.inputs['Surface'])
        bpy.ops.object.bake(type='NORMAL',normal_space='TANGENT',uv_layer='ESP_RuntimeUV');normal_img.save()
        material_name='V3_'+safe(source_obj.name)
        mat=bpy.data.materials.new(material_name);mat.use_nodes=True
        shader=mat.node_tree.nodes.get('Principled BSDF');nodes=mat.node_tree.nodes;links=mat.node_tree.links
        uv=nodes.new('ShaderNodeUVMap');uv.uv_map='ESP_RuntimeUV'
        def tex(img):
            node=nodes.new('ShaderNodeTexImage');node.image=img;links.new(uv.outputs['UV'],node.inputs['Vector']);return node
        base_tex=tex(base_img);links.new(base_tex.outputs['Color'],shader.inputs['Base Color'])
        packed_tex=tex(packed_img);separate=nodes.new('ShaderNodeSeparateXYZ');links.new(packed_tex.outputs['Color'],separate.inputs[0])
        links.new(separate.outputs['X'],shader.inputs['Metallic'])
        invert=nodes.new('ShaderNodeMath');invert.operation='SUBTRACT';invert.inputs[0].default_value=1
        links.new(packed_tex.outputs['Alpha'],invert.inputs[1]);links.new(invert.outputs[0],shader.inputs['Roughness'])
        normal_tex=tex(normal_img);normal=nodes.new('ShaderNodeNormalMap');normal.uv_map='ESP_RuntimeUV'
        links.new(normal_tex.outputs['Color'],normal.inputs['Color']);links.new(normal.outputs['Normal'],shader.inputs['Normal'])
        # Source polygons and normals remain; only their runtime material indices
        # point to the atlas whose pixels were sampled from original materials.
        mesh.materials.clear();mesh.materials.append(mat)
        for poly in mesh.polygons:poly.material_index=0
        world=transform@obj.matrix_world
        mesh.transform(world)
        if world.determinant()<0:mesh.flip_normals()
        mesh.update();obj.matrix_world=Matrix.Identity(4)
        mesh.uv_layers.active=mesh.uv_layers['ESP_RuntimeUV'];mesh.uv_layers['ESP_RuntimeUV'].active_render=True
        mesh.calc_loop_triangles();runtime.append(obj)
        record={'name':material_name,'base_color_texture':'Textures/'+base_img.name+'.png',
                'metallic_smoothness_texture':'Textures/'+packed_img.name+'.png',
                'normal_texture':'Textures/'+normal_img.name+'.png',
                'base_color':[1,1,1,1],'metallic':1,'smoothness':1,
                'source_materials':[m.name for m in originals],
                'channels':'baseColor sRGB; metallicSmoothness linear R=metallic A=smoothness; normal tangent +Y'}
        manifest['materials'].append(record)
        manifest['parts'].append({'name':name,'stage':stage,'source_object':source_obj.name,
                                  'triangles':len(mesh.loop_triangles),'original_uv':source_uv,'runtime_uv':'ESP_RuntimeUV'})
        (output/'ESP-Starship-import.json').write_text(json.dumps(manifest,indent=2)+'\n')
        print('V3_PART_BAKED',name,len(mesh.loop_triangles),flush=True)
    manifest['triangles']=sum(p['triangles'] for p in manifest['parts'])
    for img in used:
        if not img.packed_file:img.pack()
    for obj in runtime:obj.select_set(True)
    # OBJ exports exactly the mapped flight meshes; source archive and studio are
    # separate scenes and therefore cannot leak into the runtime asset.
    bpy.ops.object.select_all(action='DESELECT')
    for obj in runtime:obj.select_set(True)
    bpy.context.view_layer.objects.active=runtime[0]
    bpy.ops.wm.obj_export(filepath=str(output/'ESP-Starship.obj'),export_selected_objects=True,
                          forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,
                          export_uv=True,export_normals=True,export_object_groups=True,export_triangulated_mesh=True,path_mode='RELATIVE')
    manifest['export_axes']='Blender (x,y,z) to Unity (x,z,-y), metres, common bottom-center frame'
    manifest['runtime_ready']=True
    (output/'ESP-Starship-import.json').write_text(json.dumps(manifest,indent=2)+'\n')
    # Studio preview uses the same baked material graphs as the runtime export.
    world=bpy.data.worlds.new('V3 studio');world.use_nodes=True
    world.node_tree.nodes['Background'].inputs['Color'].default_value=(.13,.16,.20,1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value=.6;scene.world=world
    target=Vector((0,0,height*.5))
    for name,position,power,size,color in [('Key',(-65,-85,110),360000,65,(1,.94,.85)),('Fill',(90,-20,80),180000,80,(.72,.85,1)),('Rim',(0,85,110),420000,65,(.90,.95,1))]:
        light=bpy.data.lights.new(name,'AREA');light.energy=power;light.size=size;light.color=color
        ob=bpy.data.objects.new(name,light);scene.collection.objects.link(ob);ob.location=position
        ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
    camera_data=bpy.data.cameras.new('V3 preview');camera=bpy.data.objects.new('V3 preview',camera_data)
    scene.collection.objects.link(camera);camera.location=(220,-170,90)
    camera.rotation_euler=(target-camera.location).to_track_quat('-Z','Y').to_euler()
    camera_data.type='ORTHO';camera_data.ortho_scale=height*1.14;camera_data.clip_end=2000;scene.camera=camera
    scene.cycles.samples=24;scene.cycles.use_denoising=True
    scene.render.resolution_x=900;scene.render.resolution_y=1200;scene.render.resolution_percentage=100
    scene.view_settings.view_transform='AgX';scene.render.image_settings.file_format='PNG'
    scene.render.filepath=str(output/'ESP-Starship-preview.png')
    bpy.ops.wm.save_as_mainfile(filepath=str(output/'ESP-Starship.blend'))
    if not args.no_render:bpy.ops.render.render(write_still=True)
    print('V3_ADAPTATION_COMPLETE',json.dumps({'height_m':height,'triangles':manifest['triangles'],'engines':counts}),flush=True)


if __name__=='__main__':main()
