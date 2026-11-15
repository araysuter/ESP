"""Audit the published OBJ handoff without importing or changing any geometry.

Run on the designated compute host. This checks source data, not Unity rendering,
collision clearances, license permission, or runtime performance.
"""
import argparse
import hashlib
import json
import math
from pathlib import Path

MODULES = ("TowerLevel", "TowerTop", "CatchArms", "QuickDisconnect", "LaunchMount", "Foundation")


def audit(path):
    data = path.read_bytes()
    vertices, normals, texcoords, faces, libraries = [], [], [], [], []
    used_materials = set()
    for line in data.decode("utf-8").splitlines():
        fields = line.split()
        if not fields or fields[0].startswith("#"):
            continue
        kind, values = fields[0], fields[1:]
        if kind == "v":
            vertices.append(tuple(map(float, values[:3])))
        elif kind == "vn":
            normals.append(tuple(map(float, values[:3])))
        elif kind == "vt":
            texcoords.append(tuple(map(float, values)))
        elif kind == "f":
            # Resolve relative OBJ indices at the face, not at end of file.
            resolved = []
            for corner in values:
                parts = corner.split("/")
                indices = []
                for field, count in zip(parts, (len(vertices), len(texcoords), len(normals))):
                    if not field:
                        indices.append(None)
                        continue
                    index = int(field)
                    if index == 0 or not -count <= index <= count:
                        raise ValueError(f"{path.name}: invalid face index {corner}")
                    indices.append(index - 1 if index > 0 else count + index)
                resolved.append(indices[0])
            if len(resolved) < 3:
                raise ValueError(f"{path.name}: face has fewer than three vertices")
            faces.append(resolved)
        elif kind == "mtllib":
            libraries.append(" ".join(values))
        elif kind == "usemtl":
            used_materials.add(" ".join(values))
    if not vertices or any(not math.isfinite(n) for values in vertices + normals + texcoords for n in values):
        raise ValueError(f"{path.name}: missing or nonfinite geometry")
    defined_materials = set()
    material_hashes = {}
    for name in libraries:
        material_path = path.parent / name
        if material_path.resolve().parent != path.parent.resolve():
            raise ValueError(f"{path.name}: material escapes module directory")
        material_data = material_path.read_bytes()
        material_hashes[name] = hashlib.sha256(material_data).hexdigest()
        for line in material_data.decode("utf-8").splitlines():
            if line.startswith("newmtl "):
                defined_materials.add(line[7:].strip())
    if used_materials - defined_materials:
        raise ValueError(f"{path.name}: undefined materials {used_materials - defined_materials}")
    degenerate = 0
    for face in faces:
        for index in range(1, len(face) - 1):
            a, b, c = (vertices[i] for i in (face[0], face[index], face[index + 1]))
            u, v = ([b[i] - a[i] for i in range(3)], [c[i] - a[i] for i in range(3)])
            cross = (u[1]*v[2]-u[2]*v[1], u[2]*v[0]-u[0]*v[2], u[0]*v[1]-u[1]*v[0])
            degenerate += sum(n*n for n in cross) < 1e-20
    return {
        "name": path.stem, "sha256": hashlib.sha256(data).hexdigest(), "bytes": len(data),
        "vertices": len(vertices), "normals": len(normals), "texcoords": len(texcoords),
        "triangles": sum(len(face) - 2 for face in faces),
        "bounds_min_m": [min(v[i] for v in vertices) for i in range(3)],
        "bounds_max_m": [max(v[i] for v in vertices) for i in range(3)],
        "degenerate_triangles": degenerate, "material_sha256": material_hashes,
    }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("directory", type=Path)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    modules = [audit(args.directory / (name + ".obj")) for name in MODULES]
    report = {
        "schema": 1, "units": "metres", "up_axis": "Y", "level_instances": 23,
        "degenerate_cross_product_squared_threshold_m4": 1e-20,
        "modules": modules,
        "assembled_triangles": sum(m["triangles"] * (23 if m["name"] == "TowerLevel" else 1) for m in modules),
        "scope": "Source geometry only; Unity import, visual quality and runtime performance are not certified.",
    }
    result = json.dumps(report, indent=2) + "\n"
    if args.output:
        args.output.write_text(result)
    print(result, end="")


if __name__ == "__main__":
    main()
