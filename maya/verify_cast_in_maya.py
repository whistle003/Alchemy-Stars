"""Headless Maya 2025 acceptance test for an Alchemy Stars CAST package.

Usage:
    mayapy.exe maya/verify_cast_in_maya.py output.cast [scene.ma] [report.json]
"""

from __future__ import annotations

import json
import math
import sys
from pathlib import Path


def main() -> int:
    if len(sys.argv) < 2:
        raise SystemExit("Usage: mayapy verify_cast_in_maya.py <output.cast> [scene.ma] [report.json]")

    cast_path = Path(sys.argv[1]).resolve()
    scene_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else None
    report_path = Path(sys.argv[3]).resolve() if len(sys.argv) > 3 else cast_path.with_suffix(".maya2025.json")
    project_root = Path(__file__).resolve().parents[1]
    plugin_dir = project_root / "third_party" / "cast" / "maya"
    plugin_path = plugin_dir / "castplugin.py"

    if not cast_path.is_file():
        raise FileNotFoundError(f"CAST output not found: {cast_path}")
    if not plugin_path.is_file():
        raise FileNotFoundError(f"Bundled Maya plugin not found: {plugin_path}")

    sys.path.insert(0, str(plugin_dir))
    import maya.standalone

    maya.standalone.initialize(name="python")
    try:
        import maya.cmds as cmds
        import castplugin

        castplugin.sceneSettings["importMerge"] = True
        castplugin.sceneSettings["importReset"] = False
        castplugin.sceneSettings["importAtTime"] = False
        castplugin.sceneSettings["importSkin"] = True
        castplugin.sceneSettings["importLooping"] = True

        # The official plugin routes progress through Maya's main-window MEL
        # globals. mayapy has no main window, so keep the importer itself and
        # replace only its presentation-only progress hooks.
        castplugin.utilityCreateProgress = lambda status="", maximum=0: None
        castplugin.utilityStepProgress = lambda instance, status="": None
        castplugin.utilityEndProgress = lambda instance: None
        castplugin.importMaterialNode = lambda path, material: "lambert1"

        cmds.file(new=True, force=True)
        cmds.loadPlugin("shaderFXPlugin", quiet=True)
        if not cmds.objExists("lambert1SG"):
            cmds.sets(renderable=True, noSurfaceShader=True, empty=True, name="lambert1SG")
        castplugin.importCast(str(cast_path))

        joints = cmds.ls(type="joint", long=True) or []
        # Skin clusters create one intermediate *Orig shape per visible mesh.
        meshes = cmds.ls(type="mesh", long=True, noIntermediate=True) or []
        animation_curves = cmds.ls(type=["animCurveTA", "animCurveTL", "animCurveTU"]) or []
        gun_roots = cmds.ls("j_gun", type="joint", long=True) or []
        wrist_keys = cmds.keyframe("j_wrist_le", attribute="rotateX", query=True, keyframeCount=True) or 0
        minimum = float(cmds.playbackOptions(query=True, minTime=True))
        maximum = float(cmds.playbackOptions(query=True, maxTime=True))

        ik_distances = {"left": [], "right": []}
        reach_residuals = {"left": [], "right": []}
        chain_samples = {}
        for frame in range(int(minimum), int(maximum) + 1):
            cmds.currentTime(frame, edit=True)
            for side, shoulder, elbow, wrist, target in (
                ("left", "j_shoulder_le", "j_elbow_le", "j_wrist_le", "tag_ik_loc_le"),
                ("right", "j_shoulder_ri", "j_elbow_ri", "j_wrist_ri", "tag_ik_loc_ri"),
            ):
                shoulder_position = cmds.xform(shoulder, query=True, worldSpace=True, translation=True)
                elbow_position = cmds.xform(elbow, query=True, worldSpace=True, translation=True)
                wrist_position = cmds.xform(wrist, query=True, worldSpace=True, translation=True)
                target_position = cmds.xform(target, query=True, worldSpace=True, translation=True)
                ik_distances[side].append(math.dist(wrist_position, target_position))
                upper_length = math.dist(shoulder_position, elbow_position)
                lower_length = math.dist(elbow_position, wrist_position)
                target_distance = math.dist(shoulder_position, target_position)
                residual = max(
                    0.0,
                    target_distance - (upper_length + lower_length),
                    abs(upper_length - lower_length) - target_distance,
                )
                reach_residuals[side].append(residual)
                if frame == int(minimum):
                    chain_samples[side] = {
                        "shoulder": shoulder_position,
                        "elbow": elbow_position,
                        "wrist": wrist_position,
                        "target": target_position,
                        "upperLength": upper_length,
                        "lowerLength": lower_length,
                        "shoulderToTarget": target_distance,
                        "theoreticalResidual": residual,
                    }
        max_ik_error = {side: max(values) for side, values in ik_distances.items()}
        max_reach_residual = {side: max(values) for side, values in reach_residuals.items()}

        checks = {
            "singleMergedSkeleton": len(joints) == 214 and len(gun_roots) == 1,
            "allMeshesImported": len(meshes) == 21,
            "animationCurvesCreated": len(animation_curves) >= 1284,
            "wristHasEveryFrame": int(wrist_keys) == 67,
            "playbackRange": minimum == 0.0 and maximum == 66.0,
            "leftIkReachesPhysicalOptimum": all(
                actual <= residual + 0.05
                for actual, residual in zip(ik_distances["left"], reach_residuals["left"])
            ),
            "rightHandAnimationPresent": int(
                cmds.keyframe("j_wrist_ri", attribute="rotateX", query=True, keyframeCount=True) or 0
            ) == 67,
        }
        report = {
            "mayaVersion": cmds.about(version=True),
            "cast": str(cast_path),
            "jointCount": len(joints),
            "meshCount": len(meshes),
            "animationCurveCount": len(animation_curves),
            "jGunJointCount": len(gun_roots),
            "leftWristRotateXKeys": int(wrist_keys),
            "maximumIkPositionError": max_ik_error,
            "maximumTheoreticalReachResidual": max_reach_residual,
            "frameZeroChains": chain_samples,
            "playbackRange": [minimum, maximum],
            "timeUnit": cmds.currentUnit(query=True, time=True),
            "headlessMaterialImportSkipped": True,
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
