using System.Numerics;
using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

internal sealed class AnimationClip
{
    private AnimationClip(CastNode sourceNode, IReadOnlyList<CurveTrack> tracks)
    {
        SourceNode = sourceNode;
        Tracks = tracks;
        Framerate = sourceNode.Property("fr")?.GetFloats().FirstOrDefault() ?? 30f;
        Looping = sourceNode.Property("lo")?.GetBytes().FirstOrDefault() > 0;

        var frames = tracks.SelectMany(static x => x.Frames).ToArray();
        FrameStart = frames.Length == 0 ? 0 : frames.Min();
        FrameEnd = frames.Length == 0 ? 0 : frames.Max();
    }

    public CastNode SourceNode { get; }
    public IReadOnlyList<CurveTrack> Tracks { get; }
    public float Framerate { get; }
    public bool Looping { get; }
    public int FrameStart { get; }
    public int FrameEnd { get; }

    public IReadOnlySet<string> TargetNames => Tracks.Select(static x => x.NodeName).ToHashSet(StringComparer.Ordinal);

    public static AnimationClip Load(CastDocument document, string pathForErrors)
    {
        var animations = document.NodesOfType(CastConstants.Animation).ToArray();
        if (animations.Length != 1)
        {
            throw new InvalidDataException($"动画文件必须恰好包含 1 个动画节点，实际为 {animations.Length}：{pathForErrors}");
        }

        var animation = animations[0];
        var tracks = animation.ChildrenOfType(CastConstants.Curve)
            .Select(CurveTrack.FromNode)
            .ToArray();
        if (tracks.Length == 0)
        {
            throw new InvalidDataException($"动画没有曲线：{pathForErrors}");
        }

        var duplicates = tracks
            .GroupBy(static x => (x.NodeName, x.PropertyName))
            .Where(static x => x.Count() > 1)
            .Select(static x => $"{x.Key.NodeName}.{x.Key.PropertyName}")
            .ToArray();
        if (duplicates.Length > 0)
        {
            throw new InvalidDataException($"动画存在重复曲线：{string.Join(", ", duplicates.Take(8))}");
        }

        return new AnimationClip(animation, tracks);
    }

    public void Apply(PoseFrame pose, SkeletonRig rig, float frame, bool forceAdditive)
    {
        foreach (var track in Tracks)
        {
            if (!rig.TryGetIndex(track.NodeName, out var index))
            {
                continue;
            }

            var mode = forceAdditive ? "additive" : track.Mode;
            var weight = Math.Clamp(track.AdditiveBlendWeight, 0f, 1f);

            switch (track.PropertyName)
            {
                case "rq":
                {
                    var sampled = track.SampleQuaternion(frame);
                    pose.Rotations[index] = mode switch
                    {
                        "relative" => SkeletonRig.NormalizeSafe(rig.Bones[index].RestRotation * sampled),
                        "additive" => SkeletonRig.NormalizeSafe(
                            pose.Rotations[index] * Quaternion.Slerp(Quaternion.Identity, sampled, weight)),
                        _ => SkeletonRig.NormalizeSafe(sampled),
                    };
                    break;
                }
                case "tx": ApplyScalar(ref pose.Positions[index].X, rig.Bones[index].RestPosition.X, track.SampleScalar(frame), mode, weight, scale: false); break;
                case "ty": ApplyScalar(ref pose.Positions[index].Y, rig.Bones[index].RestPosition.Y, track.SampleScalar(frame), mode, weight, scale: false); break;
                case "tz": ApplyScalar(ref pose.Positions[index].Z, rig.Bones[index].RestPosition.Z, track.SampleScalar(frame), mode, weight, scale: false); break;
                case "sx": ApplyScalar(ref pose.Scales[index].X, rig.Bones[index].RestScale.X, track.SampleScalar(frame), mode, weight, scale: true); break;
                case "sy": ApplyScalar(ref pose.Scales[index].Y, rig.Bones[index].RestScale.Y, track.SampleScalar(frame), mode, weight, scale: true); break;
                case "sz": ApplyScalar(ref pose.Scales[index].Z, rig.Bones[index].RestScale.Z, track.SampleScalar(frame), mode, weight, scale: true); break;
            }
        }
    }

    private static void ApplyScalar(ref float current, float rest, float sampled, string mode, float weight, bool scale)
    {
        current = mode switch
        {
            "relative" when scale => rest * sampled,
            "relative" => rest + sampled,
            "additive" when scale => current * Lerp(1f, sampled, weight),
            "additive" => current + sampled * weight,
            _ => sampled,
        };
    }

    private static float Lerp(float a, float b, float t) => a + ((b - a) * t);
}

internal sealed class CurveTrack
{
    private CurveTrack(
        string nodeName,
        string propertyName,
        string mode,
        float additiveBlendWeight,
        int[] frames,
        float[] values,
        int components)
    {
        NodeName = nodeName;
        PropertyName = propertyName;
        Mode = mode;
        AdditiveBlendWeight = additiveBlendWeight;
        Frames = frames;
        Values = values;
        Components = components;
    }

    public string NodeName { get; }
    public string PropertyName { get; }
    public string Mode { get; }
    public float AdditiveBlendWeight { get; }
    public int[] Frames { get; }
    public float[] Values { get; }
    public int Components { get; }

    public static CurveTrack FromNode(CastNode node)
    {
        var nodeName = node.StringProperty("nn");
        var propertyName = node.StringProperty("kp");
        var frameProperty = node.Property("kb");
        var valueProperty = node.Property("kv");
        if (string.IsNullOrWhiteSpace(nodeName)
            || string.IsNullOrWhiteSpace(propertyName)
            || frameProperty is null
            || valueProperty is null)
        {
            throw new InvalidDataException("动画曲线缺少 nn、kp、kb 或 kv 属性。");
        }

        var frames = frameProperty.GetFrameIndices().ToArray();
        var values = valueProperty.Type switch
        {
            "b" => valueProperty.GetBytes().Select(static x => (float)x).ToArray(),
            "h" => valueProperty.GetUInt16s().Select(static x => (float)x).ToArray(),
            "i" => valueProperty.GetUInt32s().Select(static x => (float)x).ToArray(),
            "f" or "2v" or "3v" or "4v" => valueProperty.GetFloats().ToArray(),
            _ => throw new InvalidDataException($"曲线 {nodeName}.{propertyName} 使用了不支持的值类型：{valueProperty.Type}"),
        };
        var components = propertyName == "rq" ? 4 : 1;
        if (frames.Length == 0 || values.Length != frames.Length * components)
        {
            throw new InvalidDataException(
                $"曲线 {nodeName}.{propertyName} 的帧数和值数量不匹配：{frames.Length} 帧，{values.Length} 个值。");
        }

        for (var i = 1; i < frames.Length; i++)
        {
            if (frames[i] <= frames[i - 1])
            {
                throw new InvalidDataException($"曲线 {nodeName}.{propertyName} 的帧索引没有严格递增。");
            }
        }

        var mode = node.StringProperty("m") ?? "absolute";
        if (mode is not ("absolute" or "relative" or "additive"))
        {
            throw new InvalidDataException($"曲线 {nodeName}.{propertyName} 的模式无效：{mode}");
        }

        var blend = node.Property("ab")?.GetFloats().FirstOrDefault() ?? 1f;
        return new CurveTrack(nodeName, propertyName, mode, blend, frames, values, components);
    }

    public float SampleScalar(float frame)
    {
        var (first, second, amount) = FindPair(frame);
        return Values[first] + ((Values[second] - Values[first]) * amount);
    }

    public Quaternion SampleQuaternion(float frame)
    {
        var (first, second, amount) = FindPair(frame);
        var firstOffset = first * 4;
        var secondOffset = second * 4;
        var a = SkeletonRig.NormalizeSafe(new Quaternion(
            Values[firstOffset], Values[firstOffset + 1], Values[firstOffset + 2], Values[firstOffset + 3]));
        var b = SkeletonRig.NormalizeSafe(new Quaternion(
            Values[secondOffset], Values[secondOffset + 1], Values[secondOffset + 2], Values[secondOffset + 3]));
        return SkeletonRig.NormalizeSafe(Quaternion.Slerp(a, b, amount));
    }

    private (int First, int Second, float Amount) FindPair(float frame)
    {
        if (Frames.Length == 1 || frame <= Frames[0])
        {
            return (0, 0, 0f);
        }

        var last = Frames.Length - 1;
        if (frame >= Frames[last])
        {
            return (last, last, 0f);
        }

        var next = Array.BinarySearch(Frames, checked((int)MathF.Ceiling(frame)));
        if (next < 0)
        {
            next = ~next;
        }

        var previous = Math.Max(0, next - 1);
        if (Frames[next] == Frames[previous])
        {
            return (previous, next, 0f);
        }

        var amount = (frame - Frames[previous]) / (Frames[next] - Frames[previous]);
        return (previous, next, amount);
    }
}
