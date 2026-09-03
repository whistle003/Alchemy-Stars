using System.Numerics;

namespace AlchemyStars.Core.Baking;

internal sealed class PoseFrame
{
    public PoseFrame(SkeletonRig rig)
    {
        Positions = rig.Bones.Select(static x => x.RestPosition).ToArray();
        Rotations = rig.Bones.Select(static x => x.RestRotation).ToArray();
        Scales = rig.Bones.Select(static x => x.RestScale).ToArray();
        WorldPositions = new Vector3[rig.Bones.Count];
        WorldRotations = new Quaternion[rig.Bones.Count];
        RecalculateWorld(rig);
    }

    public Vector3[] Positions { get; }
    public Quaternion[] Rotations { get; }
    public Vector3[] Scales { get; }
    public Vector3[] WorldPositions { get; }
    public Quaternion[] WorldRotations { get; }

    public void RecalculateWorld(SkeletonRig rig)
    {
        for (var i = 0; i < rig.Bones.Count; i++)
        {
            var parentIndex = rig.Bones[i].ParentIndex;
            var localRotation = SkeletonRig.NormalizeSafe(Rotations[i]);
            Rotations[i] = localRotation;

            if (parentIndex < 0)
            {
                WorldPositions[i] = Positions[i];
                WorldRotations[i] = localRotation;
                continue;
            }

            WorldPositions[i] = Vector3.Transform(Positions[i], WorldRotations[parentIndex]) + WorldPositions[parentIndex];
            WorldRotations[i] = SkeletonRig.NormalizeSafe(WorldRotations[parentIndex] * localRotation);
        }
    }
}

