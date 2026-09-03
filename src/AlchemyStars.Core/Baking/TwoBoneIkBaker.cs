using System.Numerics;

namespace AlchemyStars.Core.Baking;

internal static class TwoBoneIkBaker
{
    public static bool TryApply(PoseFrame pose, SkeletonRig rig, IkChainNames chain)
    {
        if (!rig.TryGetIndex(chain.Start, out var startIndex)
            || !rig.TryGetIndex(chain.Middle, out var middleIndex)
            || !rig.TryGetIndex(chain.End, out var endIndex)
            || !rig.TryGetIndex(chain.Target, out var targetIndex))
        {
            return false;
        }

        pose.RecalculateWorld(rig);
        var a = pose.WorldPositions[startIndex];
        var b = pose.WorldPositions[middleIndex];
        var c = pose.WorldPositions[endIndex];
        var target = pose.WorldPositions[targetIndex];

        var upperLength = Vector3.Distance(a, b);
        var lowerLength = Vector3.Distance(b, c);
        if (upperLength < 1e-5f || lowerLength < 1e-5f)
        {
            return false;
        }

        var rootToTarget = target - a;
        var rawTargetDistance = rootToTarget.Length();
        if (rawTargetDistance < 1e-5f)
        {
            return false;
        }

        var targetDistance = Math.Clamp(
            rawTargetDistance,
            MathF.Abs(upperLength - lowerLength) + 1e-5f,
            upperLength + lowerLength - 1e-5f);

        var aimDirection = NormalizeOr(rootToTarget, Vector3.UnitX);
        var currentUpperDirection = NormalizeOr(b - a, Vector3.UnitY);
        var bendDirection = (b - a) - (Vector3.Dot(b - a, aimDirection) * aimDirection);
        bendDirection = NormalizeOr(bendDirection, FindPerpendicular(aimDirection));

        var distanceAlongAim =
            ((targetDistance * targetDistance) + (upperLength * upperLength) - (lowerLength * lowerLength))
            / (2f * targetDistance);
        var bendHeightSquared = MathF.Max(
            0f,
            (upperLength * upperLength) - (distanceAlongAim * distanceAlongAim));
        var desiredMiddle = a
            + (aimDirection * distanceAlongAim)
            + (bendDirection * MathF.Sqrt(bendHeightSquared));
        var desiredUpperDirection = NormalizeOr(desiredMiddle - a, currentUpperDirection);

        var rootDelta = RotationFromTo(currentUpperDirection, desiredUpperDirection, FindPerpendicular(currentUpperDirection));
        var startWorld = SkeletonRig.NormalizeSafe(rootDelta * pose.WorldRotations[startIndex]);
        SetWorldRotation(pose, rig, startIndex, startWorld);
        pose.RecalculateWorld(rig);

        var currentLowerDirection = NormalizeOr(
            pose.WorldPositions[endIndex] - pose.WorldPositions[middleIndex],
            Vector3.UnitX);
        var desiredLowerDirection = NormalizeOr(
            target - pose.WorldPositions[middleIndex],
            currentLowerDirection);
        var middleDelta = RotationFromTo(
            currentLowerDirection,
            desiredLowerDirection,
            FindPerpendicular(currentLowerDirection));
        var middleWorld = SkeletonRig.NormalizeSafe(middleDelta * pose.WorldRotations[middleIndex]);
        SetWorldRotation(pose, rig, middleIndex, middleWorld);
        pose.RecalculateWorld(rig);

        SetWorldRotation(pose, rig, endIndex, pose.WorldRotations[targetIndex]);
        pose.RecalculateWorld(rig);
        return true;
    }

    private static void SetWorldRotation(PoseFrame pose, SkeletonRig rig, int index, Quaternion worldRotation)
    {
        var parentIndex = rig.Bones[index].ParentIndex;
        pose.Rotations[index] = parentIndex < 0
            ? SkeletonRig.NormalizeSafe(worldRotation)
            : SkeletonRig.NormalizeSafe(Quaternion.Inverse(pose.WorldRotations[parentIndex]) * worldRotation);
    }

    private static Quaternion RotationFromTo(Vector3 from, Vector3 to, Vector3 fallbackAxis)
    {
        var dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
        if (dot > 1f - 1e-6f)
        {
            return Quaternion.Identity;
        }

        if (dot < -1f + 1e-6f)
        {
            return Quaternion.CreateFromAxisAngle(fallbackAxis, MathF.PI);
        }

        var cross = Vector3.Cross(from, to);
        return SkeletonRig.NormalizeSafe(new Quaternion(cross, 1f + dot));
    }

    private static Vector3 NormalizeOr(Vector3 value, Vector3 fallback) =>
        value.LengthSquared() < 1e-10f || !IsFinite(value)
            ? Vector3.Normalize(fallback)
            : Vector3.Normalize(value);

    private static Vector3 FindPerpendicular(Vector3 value)
    {
        var perpendicular = Vector3.Cross(Vector3.UnitX, value);
        if (perpendicular.LengthSquared() < 1e-8f)
        {
            perpendicular = Vector3.Cross(Vector3.UnitY, value);
        }

        return NormalizeOr(perpendicular, Vector3.UnitZ);
    }

    private static bool IsFinite(Vector3 value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
}
