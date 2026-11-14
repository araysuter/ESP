"""Partition exported OBJ faces for repository transport without changing geometry.

Run after prepare_starship_v3.py: python3 split_vehicle_obj.py OUTPUT_DIR
Blender's --python invocation is also supported. Source files remain in OUTPUT_DIR.
Each piece preserves vertex/UV/normal text exactly, remaps indices, and retains
object/group/material boundaries. Unity combines pieces by stage and material.
"""
import hashlib
import json
from pathlib import Path
import re
import sys


def split(directory, faces_per_part=100000):
    directory = Path(directory)
    manifest_path = directory / 'ESP-Starship-import.json'
    manifest = json.loads(manifest_path.read_text())
    source_files = manifest['model_files']
    if any('-Part-' in name for name in source_files):
        raise ValueError('Already partitioned; regenerate original stage exports first')
    output_files = []
    total_faces = 0
    source_digest = hashlib.sha256()
    result_digest = hashlib.sha256()
    for filename in source_files:
        if Path(filename).name != filename or not filename.endswith('.obj'):
            raise ValueError('Unexpected source filename')
        source = directory / filename
        arrays = {'v': [], 'vt': [], 'vn': []}
        libraries = []
        state = {'o': '', 'g': '', 'usemtl': '', 's': ''}
        faces = []

        def flush():
            nonlocal total_faces
            if not faces:
                return
            maps = {key: {} for key in arrays}
            referenced = {key: [] for key in arrays}
            remapped = []
            for face_state, indices in faces:
                converted = []
                for index in indices:
                    fields = []
                    for kind, value in zip(('v', 'vt', 'vn'), index):
                        if value is None:
                            fields.append('')
                            continue
                        if value not in maps[kind]:
                            maps[kind][value] = len(referenced[kind]) + 1
                            referenced[kind].append(arrays[kind][value])
                        fields.append(str(maps[kind][value]))
                    converted.append('/'.join(fields))
                remapped.append((face_state, converted))
            name = f'ESP-Starship-Part-{len(output_files):03d}.obj'
            with (directory / name).open('w') as stream:
                stream.write('# Source-faithful OBJ partition; no geometry simplification\n')
                for lib in libraries:
                    stream.write(lib + '\n')
                for kind in ('v', 'vt', 'vn'):
                    for line in referenced[kind]:
                        stream.write(line + '\n')
                previous = {}
                for face_state, indices in remapped:
                    for key, value in face_state.items():
                        if value and previous.get(key) != value:
                            stream.write(key + ' ' + value + '\n')
                    previous = face_state
                    stream.write('f ' + ' '.join(indices) + '\n')
                    # Verify the emitted indices resolve to the exact same data.
                    values = [face_state]
                    for index in indices:
                        values.append([referenced[k][int(v)-1] if v else None
                                       for k, v in zip(('v', 'vt', 'vn'), index.split('/'))])
                    result_digest.update(json.dumps(values, sort_keys=True).encode())
            if (directory / name).stat().st_size > 20000000:
                raise ValueError('Partition exceeds transport budget: ' + name)
            output_files.append(name)
            total_faces += len(faces)
            faces.clear()

        with source.open() as stream:
            for raw in stream:
                line = raw.strip()
                if not line or line.startswith('#'):
                    continue
                key, _, payload = line.partition(' ')
                if key in arrays:
                    arrays[key].append(line)
                elif key == 'mtllib':
                    if Path(payload).name != payload:
                        raise ValueError('Material library must be a sibling file')
                    libraries.append(line)
                elif key in state:
                    state[key] = payload
                elif key == 'f':
                    indices = []
                    for token in payload.split():
                        values = token.split('/')
                        values += [''] * (3 - len(values))
                        resolved = []
                        for kind, value in zip(('v', 'vt', 'vn'), values):
                            n = int(value) if value else None
                            n = (n - 1 if n > 0 else len(arrays[kind]) + n) if n is not None else None
                            if n is not None and not 0 <= n < len(arrays[kind]):
                                raise ValueError('OBJ index outside source array')
                            resolved.append(n)
                        indices.append(resolved)
                    values = [dict(state)] + [[arrays[k][n] if n is not None else None
                                               for k, n in zip(('v', 'vt', 'vn'), idx)] for idx in indices]
                    source_digest.update(json.dumps(values, sort_keys=True).encode())
                    faces.append((dict(state), indices))
                    if len(faces) == faces_per_part:
                        flush()
                elif key not in ('l',):
                    raise ValueError('Unreviewed OBJ statement: ' + key)
        flush()
    if source_digest.digest() != result_digest.digest():
        raise AssertionError('Partition changed face attributes')
    manifest['model_files'] = output_files
    manifest['transport_partition'] = {'original_model_files': source_files, 'faces': total_faces,
                                       'face_attribute_sha256': source_digest.hexdigest(),
                                       'geometry_uv_normal_text_unchanged': True}
    manifest_path.write_text(json.dumps(manifest, indent=2) + '\n')
    print('V3_OBJ_PARTITION_VERIFIED', len(output_files), total_faces, source_digest.hexdigest())


if __name__ == '__main__':
    args = sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else sys.argv[1:]
    if len(args) != 1:
        raise SystemExit('Expected exported model directory')
    split(args[0])
