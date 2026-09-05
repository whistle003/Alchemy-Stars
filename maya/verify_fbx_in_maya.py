"""Verify an Alchemy Stars FBX export in headless Maya 2025.

Usage:
    mayapy.exe verify_fbx_in_maya.py output.fbx [scene.ma] [report.json]
"""

from __future__ import annotations

import json
import shutil
import sys
import tempfile
from pathlib import Path


def top_joint(cmds, joint: str) -> str:
    current = joint
    while True:
        parents = cmds.listRelatives(current, parent=True, fullPath=True, type="joint") or []
        if not parents:
            return current
        current = parents[0]


def main() -> int:
    if len(sys.argv) < 2:
        raise SystemExit("Usage: mayapy verify_fbx_in_maya.py <output.fbx> [scene.ma] [report.json]")

    fbx_path = Path(sys.argv[1]).resolve()
    scene_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else None
    report_path = Path(sys.argv[3]).resolve() if len(sys.argv) > 3 else fbx_path.with_suffix(".maya2025.json")
    if not fbx_path.is_file():
        raise FileNotFoundError(f"FBX output not found: {fbx_path}")

    import maya.standalone

    maya.standalone.initialize(name="python")
    try:
        import maya.cmds as cmds
        import maya.mel as mel

        cmds.file(new=True, force=True)
        cmds.loadPlugin("fbxmaya", quiet=True)
        cmds.currentUnit(time="ntsc")
        mel.eval("FBXImportFillTimeline -v true;")
        with tempfile.TemporaryDirectory(prefix="alchemy-stars-fbx-verify-") as staging_directory:
            import_path = fbx_path
            if any(ord(character) > 127 for character in str(fbx_path)):
                import_path = Path(staging_directory) / "input.fbx"
                shutil.copy2(fbx_path, import_path)
            mel.eval(f"FBXImport -f {json.dumps(import_path.as_posix())};")

        joints = cmds.ls(type="joint", long=True) or []
        meshes = cmds.ls(type="mesh", long=True, noIntermediate=True) or []
        skin_clusters = cmds.ls(type="skinCluster") or []
        skinning_methods = [int(cmds.getAttr(f"{cluster}.skinningMethod")) for cluster in skin_clusters]
        gun_roots = cmds.ls("j_gun__weapon", type="joint", long=True) or []
        skeleton_roots = sorted({top_joint(cmds, joint) for joint in joints})
        minimum = float(cmds.playbackOptions(query=True, minTime=True))
        maximum = float(cmds.playbackOptions(query=True, maxTime=True))
        expected_key_count = int(maximum - minimum) + 1
        animated_attributes = (
            "translateX", "translateY", "translateZ",
            "rotateX", "rotateY", "rotateZ",
        )
        gun_key_count = 0
        gun_matrix_delta = 0.0
        gun_key_times = []
        if len(gun_roots) == 1:
            gun_key_count = sum(
                int(cmds.keyframe(gun_roots[0], attribute=attribute, query=True, keyframeCount=True) or 0)
                for attribute in animated_attributes
            )
            gun_key_times = sorted({
                float(value)
                for attribute in animated_attributes
                for value in (cmds.keyframe(gun_roots[0], attribute=attribute, query=True, timeChange=True) or [])
            })
            reference = None
            for frame in range(int(minimum), int(maximum) + 1):
                cmds.currentTime(frame, edit=True)
                matrix = [float(value) for value in cmds.xform(gun_roots[0], query=True, worldSpace=True, matrix=True)]
                if reference is None:
                    reference = matrix
                gun_matrix_delta = max(
                    gun_matrix_delta,
                    max(abs(current - initial) for current, initial in zip(matrix, reference)),
                )

        checks = {
            "maya2025": str(cmds.about(version=True)).startswith("2025"),
            "singleMergedSkeleton": len(joints) == 215 and len(gun_roots) == 1 and len(skeleton_roots) == 1,
            "weaponParent": (cmds.listRelatives("j_gun__weapon", parent=True) or []) == ["tag_weapon"],
            "allSkinnedMeshesImported": len(meshes) == 21 and len(skin_clusters) == 21,
            "allSkinClustersUseDqs": len(skin_clusters) == len(meshes) and all(method == 1 for method in skinning_methods),
            "playbackRange": minimum == 0.0 and maximum == 66.0,
            "weaponAnimationPresent": (
                len(gun_roots) == 1
                and gun_key_count == expected_key_count * len(animated_attributes)
                and gun_key_times == [float(frame) for frame in range(67)]
                and gun_matrix_delta > 0.001
            ),
        }
        report = {
            "mayaVersion": cmds.about(version=True),
            "fbx": str(fbx_path),
            "jointCount": len(joints),
            "meshCount": len(meshes),
            "skinClusterCount": len(skin_clusters),
            "skinningMethods": skinning_methods,
            "jGunJointCount": len(gun_roots),
            "jGunTransformKeyCount": gun_key_count,
            "jGunKeyTimes": gun_key_times,
            "maximumJGunWorldMatrixDelta": gun_matrix_delta,
            "skeletonRoots": skeleton_roots,
            "playbackRange": [minimum, maximum],
            "timeUnit": cmds.currentUnit(query=True, time=True),
            "checks": checks,
            "passed": all(checks.values()),
        }

        if scene_path is not None:
            scene_path.parent.mkdir(parents=True, exist_ok=True)
            cmds.file(rename=str(scene_path))
            cmds.file(save=True, type="mayaAscii", force=True)
            report["savedScene"] = str(scene_path)
        report_path.parent.mkdir(parents=True, exist_ok=True)
        report_path.write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print(json.dumps(report, ensure_ascii=False, indent=2))
        return 0 if report["passed"] else 2
    finally:
        maya.standalone.uninitialize()


if __name__ == "__main__":
    raise SystemExit(main())
