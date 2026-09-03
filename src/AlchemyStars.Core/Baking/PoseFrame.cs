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
        WorldMatrices = new Matrix4x4[rig.Bones.Count];
        RecalculateWorld(rig);
    }

    public Vector3[] Positions { get; }
    public Quaternion[] Rotations { get; }
    public Vector3[] Scales { get; }
    public Vector3[] WorldPositions { get; }
    public Quaternion[] WorldRotations { get; }
    public Matrix4x4[] WorldMatrices { get; }

    public void RecalculateWorld(SkeletonRig rig)
    {
        for (var i = 0; i < rig.Bones.Count; i++)
        {
            var parentIndex = rig.Bones[i].ParentIndex;
            var localRotation = SkeletonRig.NormalizeSafe(Rotations[i]);
            Rotations[i] = localRotation;
            var localMatrix = Matrix4x4.CreateScale(Scales[i])
                * Matrix4x4.CreateFromQuaternion(localRotation)
                * Matrix4x4.CreateTranslation(Positions[i]);

            if (parentIndex < 0)
            {
                WorldMatrices[i] = localMatrix;
                WorldPositions[i] = localMatrix.Translation;
                WorldRotations[i] = localRotation;
                continue;
            }

            WorldMatrices[i] = localMatrix * WorldMatrices[parentIndex];
            WorldPositions[i] = WorldMatrices[i].Translation;
            WorldRotations[i] = SkeletonRig.NormalizeSafe(WorldRotations[parentIndex] * localRotation);
        }
    }
}
