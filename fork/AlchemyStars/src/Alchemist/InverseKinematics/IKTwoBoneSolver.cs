using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;

namespace Alchemist.InverseKinematics
{

    /// <summary>
    /// 
    /// </summary>
    /// <param name=""></param>
    /// <param name="start"></param>
    /// <param name="mid"></param>
    /// <param name="end"></param>
    /// <param name="target"></param>
    /// <param name="poleVector"></param>
    public class IKTwoBoneSolver(
        string name,
        SkeletonBone start,
        SkeletonBone mid,
        SkeletonBone end,
        SkeletonBone target) : AnimationSamplerSolver(name)
    {
        /// <summary>
        /// Gets or Sets the start bone .
        /// </summary>
        public SkeletonBone StartBone { get; set; } = start;

        /// <summary>
        /// Gets or Sets the start middle bone .
        /// </summary>
        public SkeletonBone MiddleBone { get; set; } = mid;

        /// <summary>
        /// Gets or Sets the end bone .
        /// </summary>
        public SkeletonBone EndBone { get; set; } = end;

        /// <summary>
        /// Gets or Sets the target bone .
        /// </summary>
        public SkeletonBone TargetBone { get; set; } = target;

        /// <summary>
        /// Gets or Sets the default weight
        /// </summary>
        public float DefaultWeight { get; set; }

        /// <summary>
        /// Gets or Sets the Weights Cursor
        /// </summary>
        public int CurrentWeightsCursor { get; set; }

        /// <summary>
        /// Gets or Sets if the target bone's rotation is constrained to the target.
        /// </summary>
        public bool TargetConstrained { get; set; }

        /// <inheritdoc/>
        public override void Update(float time)
        {
            var cursor = CurrentWeightsCursor;
            var weight = AnimationHelper.GetWeight(Weights, time, 0.0f, 1.0f, ref cursor);

            CurrentWeightsCursor = cursor;

            if (weight == 0)
                return;

            var startPosition = StartBone.WorldTranslation;
            var middlePosition = MiddleBone.WorldTranslation;
            var endPosition = EndBone.WorldTranslation;
            var targetPosition = TargetBone.WorldTranslation;

            var upperLength = Vector3.Distance(startPosition, middlePosition);
            var lowerLength = Vector3.Distance(middlePosition, endPosition);
            var startToTarget = targetPosition - startPosition;
            var rawTargetDistance = startToTarget.Length();
            if (upperLength < 1e-5f || lowerLength < 1e-5f || rawTargetDistance < 1e-5f)
                return;

            // Preserve the animated bend plane while analytically placing the
            // middle joint on the circle shared by both limb segments.
            var targetDistance = Math.Clamp(
                rawTargetDistance,
                MathF.Abs(upperLength - lowerLength) + 1e-5f,
                upperLength + lowerLength - 1e-5f);
            var aimDirection = NormalizeOr(startToTarget, Vector3.UnitX);
            var currentUpperDirection = NormalizeOr(middlePosition - startPosition, Vector3.UnitY);
            var bendDirection = (middlePosition - startPosition)
                - Vector3.Dot(middlePosition - startPosition, aimDirection) * aimDirection;
            bendDirection = NormalizeOr(bendDirection, FindPerpendicular(aimDirection));

            var distanceAlongAim =
                ((targetDistance * targetDistance) + (upperLength * upperLength) - (lowerLength * lowerLength))
                / (2f * targetDistance);
            var bendHeight = MathF.Sqrt(MathF.Max(
                0f,
                (upperLength * upperLength) - (distanceAlongAim * distanceAlongAim)));
            var desiredMiddle = startPosition
                + aimDirection * distanceAlongAim
                + bendDirection * bendHeight;
            var desiredUpperDirection = NormalizeOr(desiredMiddle - startPosition, currentUpperDirection);

            var startDelta = RotationFromTo(
                currentUpperDirection,
                desiredUpperDirection,
                FindPerpendicular(currentUpperDirection));
            var desiredStartWorld = NormalizeSafe(startDelta * StartBone.WorldRotation);
            StartBone.WorldRotation = Quaternion.Slerp(StartBone.WorldRotation, desiredStartWorld, weight);
            StartBone.GenerateCurrentLocalTransform();
            StartBone.GenerateCurrentWorldTransforms();

            var currentLowerDirection = NormalizeOr(
                EndBone.WorldTranslation - MiddleBone.WorldTranslation,
                Vector3.UnitX);
            var desiredLowerDirection = NormalizeOr(
                targetPosition - MiddleBone.WorldTranslation,
                currentLowerDirection);
            var middleDelta = RotationFromTo(
                currentLowerDirection,
                desiredLowerDirection,
                FindPerpendicular(currentLowerDirection));
            var desiredMiddleWorld = NormalizeSafe(middleDelta * MiddleBone.WorldRotation);
            MiddleBone.WorldRotation = Quaternion.Slerp(MiddleBone.WorldRotation, desiredMiddleWorld, weight);
            MiddleBone.GenerateCurrentLocalTransform();
            MiddleBone.GenerateCurrentWorldTransforms();

            EndBone.WorldRotation = Quaternion.Slerp(EndBone.WorldRotation, TargetBone.WorldRotation, weight);
            EndBone.GenerateCurrentLocalTransform();
            EndBone.GenerateCurrentWorldTransforms();
        }

        private static Quaternion RotationFromTo(Vector3 from, Vector3 to, Vector3 fallbackAxis)
        {
            var dot = Math.Clamp(Vector3.Dot(from, to), -1f, 1f);
            if (dot > 1f - 1e-6f)
                return Quaternion.Identity;
            if (dot < -1f + 1e-6f)
                return Quaternion.CreateFromAxisAngle(fallbackAxis, MathF.PI);

            return NormalizeSafe(new Quaternion(Vector3.Cross(from, to), 1f + dot));
        }

        private static Vector3 NormalizeOr(Vector3 value, Vector3 fallback) =>
            value.LengthSquared() < 1e-10f || !IsFinite(value)
                ? Vector3.Normalize(fallback)
                : Vector3.Normalize(value);

        private static Vector3 FindPerpendicular(Vector3 value)
        {
            var perpendicular = Vector3.Cross(Vector3.UnitX, value);
            if (perpendicular.LengthSquared() < 1e-8f)
                perpendicular = Vector3.Cross(Vector3.UnitY, value);

            return NormalizeOr(perpendicular, Vector3.UnitZ);
        }

        private static Quaternion NormalizeSafe(Quaternion value) =>
            value.LengthSquared() < 1e-10f || !IsFinite(value)
                ? Quaternion.Identity
                : Quaternion.Normalize(value);

        private static bool IsFinite(Vector3 value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);

        private static bool IsFinite(Quaternion value) =>
            float.IsFinite(value.X) && float.IsFinite(value.Y)
            && float.IsFinite(value.Z) && float.IsFinite(value.W);
    }
}
