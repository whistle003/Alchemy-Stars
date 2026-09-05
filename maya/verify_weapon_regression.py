"""Compare weapon exports against separately imported source rigs in Maya 2025.

Run with mayapy <script> <weapon-regression.json>. No user scene is modified.
"""
import json
import bisect
import math
import shlex
import shutil
import sys
import tempfile
from pathlib import Path


def main():
    manifest = Path(sys.argv[1]).resolve()
    cases = json.loads(manifest.read_text(encoding="utf-8"))
    if len(sys.argv) > 3:
        artifacts = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))["artifacts"]
        project = json.loads(Path(sys.argv[3]).read_text(encoding="utf-8"))
        animation = project["Animations"][0]
        cases.append(dict(id="Hawk", hands=next(p["FilePath"] for p in project["Parts"] if p["Type"] == 0),
                          weapon=next(p["FilePath"] for p in project["Parts"] if p["Type"] == 1),
                          main=animation["Name"], layers=[l["Name"] for l in animation["Layers"]],
                          layerTypes=[l["Type"] for l in animation["Layers"]],
                          full=artifacts["sprintCast"], selective=artifacts["selectiveBakeCast"],
                          animationOnly=artifacts["animationOnlyCast"], smd=artifacts["sprintSmd"], fbx=artifacts["sprintFbx"]))
    sys.path.insert(0, str(Path(__file__).resolve().parents[1] / "third_party/cast/maya"))
    import maya.standalone
    maya.standalone.initialize(name="python")
    try:
        import maya.cmds as cmds
        import maya.mel as mel
        import maya.api.OpenMaya as om
        import cast
        import castplugin as plugin
        plugin.sceneSettings.update(importMerge=False, importReset=False, importAtTime=False,
                                    importSkin=True, importLooping=True, importIK=False, importConstraints=False)
        plugin.utilityCreateProgress = lambda *a, **k: None
        plugin.utilityStepProgress = lambda *a, **k: None
        plugin.utilityEndProgress = lambda *a, **k: None
        plugin.importMaterialNode = lambda *a, **k: "lambert1"

        def reset():
            cmds.file(new=True, force=True)
            cmds.currentUnit(time="ntsc", linear="cm", angle="deg")
            cmds.sets(renderable=True, noSurfaceShader=True, empty=True, name="lambert1SG")

        def models(path):
            return [m for r in cast.Cast.load(path).Roots() for m in r.ChildrenOfType(cast.Model)]

        def sample(frames):
            joints = {p.rsplit("|", 1)[-1]: p for p in cmds.ls(type="joint", long=True)}
            assert len(joints) == len(cmds.ls(type="joint")), "Ambiguous joint names"
            result = []
            for f in frames:
                cmds.currentTime(f)
                result.append({n: cmds.xform(p, q=True, ws=True, matrix=True) for n, p in joints.items()})
            return result

        def compare(left, right, names=None, tolerance=0.003):
            assert len(left) == len(right) and left, "Frame count mismatch"
            error = 0.0
            worst = None
            for frame, (a, b) in enumerate(zip(left, right)):
                if names is None:
                    assert a.keys() == b.keys(), "Joint identity mismatch"
                for n in names or a:
                    assert n in b, "Missing joint " + n
                    assert len(a[n]) == len(b[n]) == 16
                    assert all(math.isfinite(v) for v in a[n] + b[n]), "Non-finite matrix"
                    delta = max(abs(x-y) for x, y in zip(a[n], b[n]))
                    if delta > error: error, worst = delta, (frame, n)
            assert error < tolerance, "World matrix mismatch: " + str((error, worst))
            return error

        def mesh_samples(frames):
            shapes = cmds.ls(type="mesh", long=True, noIntermediate=True) or []
            result = []
            for f in frames:
                cmds.currentTime(f)
                row = []
                for shape in shapes:
                    selection = om.MSelectionList(); selection.add(shape)
                    points = om.MFnMesh(selection.getDagPath(0)).getPoints(om.MSpace.kWorld)
                    row.append([list(points[i])[:3] for i in (0, len(points)//2, len(points)-1)])
                result.append(row)
            return result

        reports = []
        for case in cases:
            reset(); plugin.importCast(case["full"])
            frames = list(range(int(cmds.playbackOptions(q=True, min=True)), int(cmds.playbackOptions(q=True, max=True))+1))
            baseline = sample(frames)
            mesh_frames = sorted(set((frames[0], frames[len(frames)//2], frames[-1])))
            baseline_mesh = mesh_samples(mesh_frames)
            assert cmds.listRelatives("j_gun__weapon", parent=True) == ["tag_weapon"]
            assert cmds.listRelatives("j_gun", parent=True) == ["j_wrist_ri"]
            root_error = compare(baseline, [{"j_gun__weapon": row["tag_weapon"]} for row in baseline], ["j_gun__weapon"], 1e-5)
            movement = max(abs(a-b) for row in baseline for a, b in zip(row["j_gun__weapon"], baseline[0]["j_gun__weapon"]))
            assert movement > 0.001, "Weapon has no motion"
            cmds.file(rename=str(Path(case["full"]).with_suffix(".ma")))
            cmds.file(save=True, type="mayaAscii", force=True)

            reset(); plugin.importCast(case["selective"])
            selective_error = compare(baseline, sample(frames), tolerance=1e-5)
            reset()
            for model in models(case["full"]): plugin.importModelNode(model, case["full"])
            plugin.importCast(case["animationOnly"])
            animation_error = compare(baseline, sample(frames), tolerance=1e-5)

            # Build the reference through Maya's model importer, independently of
            # the application's bone merge and mesh/skin remapping code.
            reset()
            for model in models(case["hands"]): plugin.importModelNode(model, case["hands"])
            for model in models(case["weapon"]):
                root = next(b for b in model.Skeleton().Bones() if b.ParentIndex() < 0)
                root.SetName("j_gun__weapon")
                plugin.importModelNode(model, case["weapon"])
            cmds.parent("j_gun__weapon", "tag_weapon", relative=True)
            plugin.importCast(case["animationOnly"])
            reference_error = compare(baseline, sample(frames))
            print(json.dumps(dict(id=case["id"], selectiveMatrixError=selective_error,
                                  animationOnlyMatrixError=animation_error, separateRigMatrixError=reference_error)), flush=True)
            reference_mesh = mesh_samples(mesh_frames)
            mesh_error = max(abs(x-y) for af, bf in zip(baseline_mesh, reference_mesh)
                             for am, bm in zip(af, bf) for av, bv in zip(am, bm) for x, y in zip(av, bv))
            assert len(baseline_mesh[0]) == len(reference_mesh[0])
            assert mesh_error < 0.005, "Reference skin/vertex mismatch: " + str(mesh_error)

            # Test original source curves, independently of the exported curves.
            # Maya's importer adds only at incoming key times; expand each source
            # to the full range so a one-frame offset affects the entire layer.
            plugin.utilityClearAnimation()
            for i, path in enumerate([case["main"]] + case["layers"]):
                animation = next(a for r in cast.Cast.load(path).Roots() for a in r.ChildrenOfType(cast.Animation))
                # Quantized CAST quaternions can have w=1 with nonzero xyz.
                # Maya's additive utilitySlerp then treats dot>=1 as no rotation.
                # Normalize before comparing the represented rotation.
                for curve in animation.Curves():
                    keys = curve.KeyFrameBuffer()
                    if not keys or curve.KeyPropertyName() not in ("rq", "tx", "ty", "tz", "sx", "sy", "sz"):
                        continue
                    values = curve.KeyValueBuffer()
                    if curve.KeyPropertyName() == "rq":
                        values = [om.MQuaternion(*values[o:o+4]).normal() for o in range(0, len(values), 4)]
                    sampled = []
                    for frame in frames:
                        high = min(bisect.bisect_left(keys, frame), len(keys)-1)
                        low = max(high-1, 0) if frame < keys[high] else high
                        weight = (frame-keys[low])/(keys[high]-keys[low]) if high != low else 0.0
                        if curve.KeyPropertyName() == "rq":
                            q = om.MQuaternion.slerp(values[low], values[high], weight)
                            sampled.append((q.x, q.y, q.z, q.w))
                        else:
                            sampled.append(values[low] + (values[high]-values[low])*weight)
                    curve.SetKeyFrameBuffer(frames)
                    if curve.KeyPropertyName() == "rq":
                        curve.SetVec4KeyValueBuffer(sampled)
                    else:
                        curve.SetFloatKeyValueBuffer(sampled)
                if i and case["layerTypes"][i-1] in (1, 3):
                    for curve in animation.Curves(): curve.SetMode("additive")
                plugin.importAnimationNode(animation, path)
            branch_names = ["tag_weapon", "j_gun__weapon"] + [
                p.rsplit("|", 1)[-1] for p in cmds.listRelatives("j_gun__weapon", allDescendents=True, type="joint", fullPath=True) or []]
            original_samples = sample(frames)
            source_error = compare(baseline, original_samples, branch_names)

            reset(); cmds.loadPlugin("fbxmaya", quiet=True)
            mel.eval('FBXImportFillTimeline -v true;')
            with tempfile.TemporaryDirectory(prefix="alchemy-stars-weapon-fbx-") as staging:
                staged = Path(staging) / "input.fbx"
                shutil.copy2(case["fbx"], staged)
                mel.eval('FBXImport -f ' + json.dumps(staged.as_posix()) + ';')
            fbx_error = compare(baseline, sample(frames))
            assert len(cmds.ls(type="skinCluster") or []) == len(baseline_mesh[0])

            # Reconstruct SMD joint transforms directly in Maya and compare them.
            reset()
            lines = Path(case["smd"]).read_text().splitlines()
            begin = lines.index("nodes") + 1; end = lines.index("end", begin)
            smd_nodes = {}
            for line in lines[begin:end]:
                index, name, parent = shlex.split(line)
                cmds.select(clear=True)
                node = cmds.joint(name=name)
                if int(parent) >= 0: cmds.parent(node, smd_nodes[int(parent)], relative=True)
                smd_nodes[int(index)] = node
            begin = lines.index("skeleton") + 1
            for line in lines[begin:]:
                if line == "end": break
                if line.startswith("time "):
                    frame = int(line.split()[1]); continue
                fields = line.split(); node = smd_nodes[int(fields[0])]
                values = [float(x) for x in fields[1:]]
                for attr, value in zip(("tx","ty","tz","rx","ry","rz"), values[:3] + [math.degrees(x) for x in values[3:]]):
                    cmds.setKeyframe(node, attribute=attr, t=frame, value=value)
            smd_error = compare(baseline, sample(frames))
            report = dict(id=case["id"], frames=len(frames), weaponWorldMotion=movement,
                          weaponAnchorError=root_error, selectiveMatrixError=selective_error,
                          animationOnlyMatrixError=animation_error, separateRigMatrixError=reference_error,
                          separateRigVertexError=mesh_error, originalAnimationWeaponMatrixError=source_error,
                          sourceQuaternionsNormalizedForMaya=True,
                          sourceLayersResampledForMaya=True,
                          fbxMatrixError=fbx_error, smdMatrixError=smd_error, passed=True)
            reports.append(report)
            print(json.dumps(report))
        manifest.with_name("weapon-regression.maya2025.json").write_text(json.dumps(reports, indent=2), encoding="utf-8")
    finally:
        maya.standalone.uninitialize()


if __name__ == "__main__":
    main()
