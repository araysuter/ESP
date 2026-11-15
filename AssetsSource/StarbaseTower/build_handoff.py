"""Create a visual-only 1:1 Starbase tower study from herbys' printable kit.

Placement of arm assemblies and simplified mount legs is approximate. See
HANDOFF.md. The source STL pieces remain unchanged alongside this script.
"""
import bpy, math, sys
from pathlib import Path
from mathutils import Vector

args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else []
if len(args) != 2:
    raise SystemExit('Expected source STL directory and output directory')
root, out = map(lambda x: Path(x).resolve(), args)
out.mkdir(exist_ok=True)
bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = 'METRIC'

def mat(name, rgba):
    m = bpy.data.materials.new(name)
    m.diffuse_color = rgba
    return m
steel = mat('Tower steel', (0.43, 0.46, 0.48, 1))
black = mat('Arms dark steel', (0.10, 0.12, 0.13, 1))
concrete = mat('Concrete', (0.4, 0.4, 0.37, 1))

def import_stl(filename, name, convert, material):
    bpy.ops.wm.stl_import(filepath=str(root / filename))
    o = bpy.context.selected_objects[0]
    o.name = name
    for v in o.data.vertices:
        v.co = convert(*v.co)
    o.data.update()
    o.data.materials.append(material)
    return o

def bounds(filename):
    # Import and then recenter because the source sections have unrelated origins.
    bpy.ops.wm.stl_import(filepath=str(root / filename))
    o=bpy.context.selected_objects[0]
    vs=o.data.vertices
    lo=[min(v.co[i] for v in vs) for i in range(3)]
    hi=[max(v.co[i] for v in vs) for i in range(3)]
    bpy.data.objects.remove(o, do_unlink=True)
    return lo,hi

def section(filename, name, z):
    lo,hi=bounds(filename)
    cx=(lo[0]+hi[0])/2; cz=(lo[2]+hi[2])/2
    o=import_stl(filename,name,lambda x,y,w: ((x-cx)*.1,36-(w-cz)*.1,z+(y-lo[1])*.1),steel)
    return o, (hi[1]-lo[1])*.1

floor,h=section('Individual_level_x23.stl','Tower level 01',0)
for i in range(1,23):
    o=floor.copy();o.data=floor.data;o.name=f'Tower level {i+1:02}'
    bpy.context.collection.objects.link(o);o.location.z=i*h
top,th=section('Top_level.stl','Tower top',23*h)

# Four quadrants were authored in the same coordinate system.  Preserve their
# shared seam positions, but move their center to the rocket axis at (0,0).
for i in range(4):
    import_stl(f'Platform{i}.stl',f'Launch mount quadrant {i}',
        lambda x,y,z: ((x+49.68)*.1,-(z+34.97)*.1,4+(y+20)*.1),steel)

# Source specifies PVC legs without dimensions: these six supports are visual
# stand-ins, with an explicitly approximate 4 m underside height.
for i in range(6):
    a=2*math.pi*i/6
    bpy.ops.mesh.primitive_cylinder_add(vertices=16, radius=1.45, depth=4, location=(7*math.cos(a),7*math.sin(a),2))
    o=bpy.context.object;o.name=f'Approximate mount support {i+1}';o.data.materials.append(black)

# Full printable chopstick alternatives. The pair share a common STL frame.
# Their position is an approximate static display pose near level 13.
for side in ('Left','Right'):
    import_stl(f'{side}_chopstick_full_chopsticks_alternative_1.stl',f'{side} catch arm',
        lambda x,y,z: ((x-203.5)*.1,29.2-(z+57.9)*.1,72+y*.1),black)

# Main ship quick-disconnect arm, positioned approximately at level 14.
qdlo,qdhi=bounds('QD_arm_main_tube_frame.stl')
qdcenter=(qdlo[0]+qdhi[0])/2
import_stl('QD_arm_main_tube_frame.stl','Approximate ship QD arm',
    lambda x,y,z: (7+(x-qdcenter)*.1,36-(z-qdlo[2])*.1,79+(y-qdlo[1])*.1),black)

# Foundation is a simple replacement for the source's unmodelled wood base.
bpy.ops.mesh.primitive_cube_add(size=1, location=(0,36,-1))
base=bpy.context.object;base.name='Approximate tower foundation';base.scale=(16,16,2)
bpy.ops.object.transform_apply(location=False,rotation=False,scale=True)
base.data.materials.append(concrete)

for o in scene.objects:
    if o.type=='MESH': o.select_set(True)
    else: o.select_set(False)
print('TOWER_METERS',h*23+th,'MESH_OBJECTS',sum(o.type=='MESH' for o in scene.objects))
bpy.ops.wm.save_as_mainfile(filepath=str(out/'ESP-Starbase-Tower.blend'))
bpy.ops.wm.obj_export(filepath=str(out/'ESP-Starbase-Tower.obj'),export_selected_objects=True,
    forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,apply_modifiers=True)
print('EXPORT_COMPLETE')
