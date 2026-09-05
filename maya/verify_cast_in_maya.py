"""Headless Maya 2025 acceptance test for an Alchemy Stars CAST package.

Usage:
    mayapy.exe maya/verify_cast_in_maya.py output.cast [scene.ma] [report.json] [--selective]
"""

from __future__ import annotations

import hashlib
import json
import math
import sys
from pathlib import Path


def is_visible(cmds, node: str) -> bool:
    """Return whether a DAG shape and every ancestor are visible."""
    current = node
    while current:
        if cmds.attributeQuery("visibility", node=current, exists=True):
            if not bool(cmds.getAttr(f"{current}.visibility")):
                return False
        parents = cmds.listRelatives(current, parent=True, fullPath=True) or []
        current = parents[0] if parents else ""
    return True


def top_joint(cmds, joint: str) -> str:
    current = joint
    while True:
        parents = cmds.listRelatives(current, parent=True, fullPath=True, type="joint") or []
        if not parents:
            return current
        current = parents[0]


def main() -> int:
    if len(sys.argv) < 2:
        raise SystemExit("Usage: mayapy verify_cast_in_maya.py <output.cast> [scene.ma] [report.json] [--selective]")

    cast_path = Path(sys.argv[1]).resolve()
    scene_path = Path(sys.argv[2]).resolve() if len(sys.argv) > 2 else None
    report_path = Path(sys.argv[3]).resolve() if len(sys.argv) > 3 else cast_path.with_suffix(".maya2025.json")
    selective_bake = "--selective" in sys.argv[4:]
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

        # Match a normal GUI import with Import Merge disabled. Alchemy Stars
        # output must already be one physical model and must not depend on an
        # importer preference to avoid duplicate skeletons or ambiguous tracks.
        castplugin.sceneSettings["importMerge"] = False
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
        visible_meshes = [mesh for mesh in meshes if is_visible(cmds, mesh)]
        animation_curves = cmds.ls(type=["animCurveTA", "animCurveTL", "animCurveTU"]) or []
        gun_roots = cmds.ls("j_gun__weapon", type="joint", long=True) or []
        wrist_keys = cmds.keyframe("j_wrist_le", attribute="rotateX", query=True, keyframeCount=True) or 0
        minimum = float(cmds.playbackOptions(query=True, minTime=True))
        maximum = float(cmds.playbackOptions(query=True, maxTime=True))

        ik_distances = {"left": [], "right": []}
        reach_residuals = {"left": [], "right": []}
        chain_samples = {}
        gun_reference_matrix = None
        gun_matrix_deltas = []
        world_animation_digest = hashlib.sha256()
        for frame in range(int(minimum), int(maximum) + 1):
            cmds.currentTime(frame, edit=True)
            for joint in sorted(joints):
                world_matrix = cmds.xform(joint, query=True, worldSpace=True, matrix=True)
                sample = f"{frame}|{joint}|" + ",".join(
                    f"{float(value):.4f}" for value in world_matrix
                ) + "\n"
                world_animation_digest.update(sample.encode("utf-8"))
            if len(gun_roots) == 1:
                gun_matrix = [
                    float(value)
                    for value in cmds.xform(gun_roots[0], query=True, worldSpace=True, matrix=True)
                ]
                if gun_reference_matrix is None:
                    gun_reference_matrix = gun_matrix
                gun_matrix_deltas.append(
                    max(abs(current - initial) for current, initial in zip(gun_matrix, gun_reference_matrix))
                )
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

        expected_key_times = [float(frame) for frame in range(int(minimum), int(maximum) + 1)]
        animated_transform_attributes = (
            "translateX", "translateY", "translateZ",
            "rotateX", "rotateY", "rotateZ",
        )
        gun_transform_key_count = 0
        if len(gun_roots) == 1:
            gun_transform_key_count = sum(
                int(cmds.keyframe(gun_roots[0], attribute=attribute, query=True, keyframeCount=True) or 0)
                for attribute in animated_transform_attributes
            )
        maximum_gun_matrix_delta = max(gun_matrix_deltas, default=0.0)
        incomplete_channels = []
        partially_keyed_channels = []
        for joint in joints:
            for attribute in animated_transform_attributes:
                key_times = cmds.keyframe(
                    joint,
                    attribute=attribute,
                    query=True,
                    timeChange=True,
                ) or []
                if [float(value) for value in key_times] != expected_key_times:
                    incomplete_channels.append(f"{joint}.{attribute}")
                    if key_times:
                        partially_keyed_channels.append(f"{joint}.{attribute}")

        skeleton_roots = sorted({top_joint(cmds, joint) for joint in joints})
        time_unit = cmds.currentUnit(query=True, time=True)

        full_curve_count = len(joints) * len(animated_transform_attributes)
        checks = {
            "maya2025": str(cmds.about(version=True)).startswith("2025"),
            "singleMergedSkeleton": len(joints) == 215 and len(gun_roots) == 1 and len(skeleton_roots) == 1,
            "weaponParent": (cmds.listRelatives("j_gun__weapon", parent=True) or []) == ["tag_weapon"],
            "handHelperParent": (cmds.listRelatives("j_gun", parent=True) or []) == ["j_wrist_ri"],
            "allMeshesImportedAndVisible": len(meshes) == 21 and len(visible_meshes) == len(meshes),
            "animationCurvesCreated": (
                0 < len(animation_curves) < full_curve_count
                if selective_bake
                else len(animation_curves) >= full_curve_count
            ),
            "everyRetainedTransformChannelKeyedEveryFrame": (
                not partially_keyed_channels
                if selective_bake
                else not incomplete_channels
            ),
            "thirtyFps": time_unit == "ntsc",
            "playbackRange": minimum == 0.0 and maximum == 66.0,
            "leftIkReachesPhysicalOptimum": all(
                actual <= residual + 0.05
                for actual, residual in zip(ik_distances["left"], reach_residuals["left"])
            ),
            "weaponAnimationPresent": (
                len(gun_roots) == 1
                and (selective_bake or gun_transform_key_count == len(expected_key_times) * len(animated_transform_attributes))
                and maximum_gun_matrix_delta > 0.001
            ),
            "rightHandAnimationPresent": int(
                cmds.keyframe("j_wrist_ri", attribute="rotateX", query=True, keyframeCount=True) or 0
            ) == 67,
        }
        report = {
            "mayaVersion": cmds.about(version=True),
            "validationMode": "selective" if selective_bake else "full",
            "cast": str(cast_path),
            "jointCount": len(joints),
            "meshCount": len(meshes),
            "visibleMeshCount": len(visible_meshes),
            "animationCurveCount": len(animation_curves),
            "jGunJointCount": len(gun_roots),
            "jGunTransformKeyCount": gun_transform_key_count,
            "maximumJGunWorldMatrixDelta": maximum_gun_matrix_delta,
            "skeletonRoots": skeleton_roots,
            "leftWristRotateXKeys": int(wrist_keys),
            "incompleteTransformChannelCount": len(incomplete_channels),
            "incompleteTransformChannelExamples": incomplete_channels[:10],
            "partiallyKeyedTransformChannelCount": len(partially_keyed_channels),
            "jointWorldAnimationDigest4dp": world_animation_digest.hexdigest().upper(),
            "maximumIkPositionError": max_ik_error,
            "maximumTheoreticalReachResidual": max_reach_residual,
            "frameZeroChains": chain_samples,
            "playbackRange": [minimum, maximum],
            "timeUnit": time_unit,
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
