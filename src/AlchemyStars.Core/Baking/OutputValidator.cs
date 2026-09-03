using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

internal static class OutputValidator
{
    public static void Validate(CastDocument document, SkeletonRig rig, int expectedFrameStart, int expectedFrameEnd)
    {
        var animations = document.NodesOfType(CastConstants.Animation).ToArray();
        if (animations.Length != 1)
        {
            throw new InvalidDataException($"输出必须恰好有 1 个动画，实际为 {animations.Length}。");
        }

        var modelCount = document.NodesOfType(CastConstants.Model).Count();
        if (modelCount < 2)
        {
            throw new InvalidDataException($"输出应至少包含手臂和武器两个模型，实际为 {modelCount}。");
        }

        var curves = animations[0].ChildrenOfType(CastConstants.Curve).ToArray();
        const int transformCurveCount = 7; // rq + tx/ty/tz + sx/sy/sz
        var expectedCurveCount = checked(rig.Bones.Count * transformCurveCount);
        if (curves.Length != expectedCurveCount)
        {
            throw new InvalidDataException($"输出曲线数量应为 {expectedCurveCount}，实际为 {curves.Length}。");
        }

        var duplicates = curves
            .GroupBy(static x => (x.StringProperty("nn"), x.StringProperty("kp")))
            .Where(static x => x.Count() != 1)
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException("输出中存在重复的骨骼属性曲线。");
        }

        foreach (var curve in curves)
        {
            if (curve.StringProperty("m") != CastConstants.ModeAbsolute)
            {
                throw new InvalidDataException("输出仍包含非绝对模式曲线，无法保证 Maya 导入结果唯一。");
            }

            var frames = curve.Property("kb")?.GetFrameIndices()
                ?? throw new InvalidDataException("输出曲线缺少帧缓冲区。");
            if (frames.Count == 0 || frames[0] != expectedFrameStart || frames[^1] != expectedFrameEnd)
            {
                throw new InvalidDataException("输出曲线帧范围不一致。");
            }

            var values = curve.Property("kv")?.GetFloats()
                ?? throw new InvalidDataException("输出曲线缺少值缓冲区。");
            if (values.Any(static x => !float.IsFinite(x)))
            {
                throw new InvalidDataException("输出曲线包含 NaN 或 Infinity。");
            }

            if (curve.StringProperty("kp") == CastConstants.CurveRotation)
            {
                for (var i = 0; i < values.Length; i += 4)
                {
                    var lengthSquared = (values[i] * values[i]) + (values[i + 1] * values[i + 1])
                        + (values[i + 2] * values[i + 2]) + (values[i + 3] * values[i + 3]);
                    if (MathF.Abs(1f - lengthSquared) > 0.002f)
                    {
                        throw new InvalidDataException("输出含未归一化四元数。");
                    }
                }
            }
        }

        var hashes = document.Nodes().Select(static x => x.Hash).ToArray();
        if (hashes.Distinct().Count() != hashes.Length)
        {
            throw new InvalidDataException("输出 CAST 节点哈希不唯一。");
        }
    }
}
