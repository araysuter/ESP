"""Run with Blender --background --python build_rocket.py -- <output-directory>.
Original ESP placeholder. Blender Z up; OBJ export converts to Unity Y up.
"""
import bpy, math, pathlib, sys
out=pathlib.Path(sys.argv[sys.argv.index('--')+1]); out.mkdir(parents=True,exist_ok=True)
bpy.ops.object.select_all(action='SELECT'); bpy.ops.object.delete(use_global=False)
bpy.context.scene.unit_settings.system='METRIC'
bpy.context.scene.unit_settings.scale_length=1.0

def cone(name,z,depth,r1,r2):
 bpy.ops.mesh.primitive_cone_add(vertices=48,radius1=r1,radius2=r2,depth=depth,location=(0,0,z))
 ob=bpy.context.object; ob.name=name
 for p in ob.data.polygons: p.use_smooth=len(p.vertices)==4
 return ob
cone('Booster',10,20,2,2)
cone('Interstage',20.6,1.2,2,2)
cone('UpperStage',26.2,10,2,2)
cone('Nose',34.2,6,2,0)
for i in range(5):
 a=i*math.tau/5
 ob=cone('Engine_'+str(i),-0.6,1.2,0.55,0.32);ob.location.x=math.cos(a)*1.15;ob.location.y=math.sin(a)*1.15
for i in range(4):
 a=i*math.tau/4
 bpy.ops.mesh.primitive_cube_add(size=1,location=(math.cos(a)*2.1,math.sin(a)*2.1,3))
 ob=bpy.context.object;ob.name='Fin_'+str(i);ob.scale=(2,.12,4);ob.rotation_euler.z=a
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
bpy.ops.wm.save_as_mainfile(filepath=str(out/'ESP-Rocket.blend'))
bpy.ops.wm.obj_export(filepath=str(out/'ESP-Rocket.obj'),forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=False,export_object_groups=True,export_triangulated_mesh=True)
print('ESP_ASSET_EXPORTED objects='+str(len(bpy.data.objects)))
