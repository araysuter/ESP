"""Export reusable pieces from the assembled Blender handoff in Unity axes."""
import bpy, sys
from pathlib import Path
args=sys.argv[sys.argv.index('--')+1:] if '--' in sys.argv else []
if len(args)!=2: raise SystemExit('Expected assembled .blend file and output directory')
source,out=map(lambda x: Path(x).resolve(),args)
out.mkdir(exist_ok=True)
bpy.ops.wm.open_mainfile(filepath=str(source))
groups={
 'TowerLevel': lambda n: n=='Tower level 01',
 'TowerTop': lambda n: n=='Tower top',
 'CatchArms': lambda n: n in ('Left catch arm','Right catch arm'),
 'QuickDisconnect': lambda n: n=='Approximate ship QD arm',
 'LaunchMount': lambda n: n.startswith('Launch mount quadrant') or n.startswith('Approximate mount support'),
 'Foundation': lambda n: n=='Approximate tower foundation',
}
for group, test in groups.items():
    bpy.ops.object.select_all(action='DESELECT')
    matches=[o for o in bpy.context.scene.objects if o.type=='MESH' and test(o.name)]
    if not matches: raise ValueError(f'Missing group {group}')
    for o in matches:o.select_set(True)
    bpy.ops.wm.obj_export(filepath=str(out/f'{group}.obj'),export_selected_objects=True,
        forward_axis='NEGATIVE_Z',up_axis='Y',export_materials=True,apply_modifiers=True)
    print('MODULE',group,'objects',len(matches))
