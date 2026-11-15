"""Bound the experimental Starbase Blender trial to 180 seconds and two threads.

No Blender validation has been completed. Run only after a compute/editor handoff.
"""
import argparse
import json
from pathlib import Path
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--blender", required=True, type=Path, help="Blender 5.2.3 executable")
    parser.add_argument("source", type=Path)
    parser.add_argument("output", type=Path, help="New directory outside the source modules")
    args = parser.parse_args()
    source, output = args.source.resolve(), args.output.resolve()
    if not source.is_dir() or not args.blender.is_file():
        parser.error("Source directory and Blender executable must exist")
    if output.exists() or output == source or source in output.parents:
        parser.error("Use a new output directory outside the source modules")
    command = [str(args.blender.resolve()), "--factory-startup", "--disable-autoexec",
               "--background", "--threads", "2", "--python-exit-code", "1", "--python",
               str(Path(__file__).with_name("qualify_blender.py").resolve()), "--", str(source), str(output)]
    try:
        subprocess.run(command, check=True, timeout=180)
    except subprocess.TimeoutExpired:
        raise SystemExit("Blender trial exceeded 180 seconds and was killed; partial output is unqualified")
    except subprocess.CalledProcessError as error:
        raise SystemExit(f"Blender trial failed with exit {error.returncode}; partial output is unqualified")
    report_path = output / "blender-qualification.json"
    if not report_path.is_file():
        raise SystemExit("Blender exited without a completed trial report")
    report = json.loads(report_path.read_text())
    if report.get("approved_for_runtime") is not False or len(report.get("modules", [])) != 6:
        raise SystemExit("Unexpected trial report; do not use the output")
    print("Trial artifacts recorded. Visual, Unity and packaged performance qualification remain required.")


if __name__ == "__main__":
    main()
