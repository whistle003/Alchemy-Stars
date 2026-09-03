using System.Numerics;
using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

internal sealed class AnimationClip
{
    private AnimationClip(
        CastNode sourceNode,
        IReadOnlyList<CurveTrack> tracks,
        IReadOnlyList<CurveModeOverride> modeOverrides)
    {
        SourceNode = sourceNode;
        Tracks = tracks;
        ModeOverrides = modeOverrides;
        Framerate = sourceNode.Property("fr")?.GetFloats().FirstOrDefault() ?? 30f;
        Looping = sourceNode.Property("lo")?.GetBytes().FirstOrDefault() > 0;

        var frames = tracks.SelectMany(static x => x.Frames).ToArray();
        FrameStart = frames.Length == 0 ? 0 : frames.Min();
        FrameEnd = frames.Length == 0 ? 0 : frames.Max();
    }

    public CastNode SourceNode { get; }
    public IReadOnlyList<CurveTrack> Tracks { get; }
    public IReadOnlyList<CurveModeOverride> ModeOverrides { get; }
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

        var modeOverrides = animation.ChildrenOfType(CastConstants.CurveModeOverride)
            .Select(CurveModeOverride.FromNode)
            .ToArray();

        return new AnimationClip(animation, tracks, modeOverrides);
    }

    public void Apply(PoseFrame pose, SkeletonRig rig, float frame, bool forceAdditive)
    {
        foreach (var track in Tracks)
        {
            if (!rig.TryGetIndex(track.NodeName, out var index))
            {
                continue;
            }

            var mode = forceAdditive
                ? CastConstants.ModeAdditive
                : ResolveMode(track, rig, index);
            var weight = Math.Clamp(track.AdditiveBlendWeight, 0f, 1f);

            switch (track.PropertyName)
            {
                case CastConstants.CurveRotation:
                {
                    var sampled = track.SampleQuaternion(frame);
                    pose.Rotations[index] = mode switch
                    {
                        CastConstants.ModeRelative => SkeletonRig.NormalizeSafe(rig.Bones[index].RestRotation * sampled),
                        CastConstants.ModeAdditive => SkeletonRig.NormalizeSafe(
                            pose.Rotations[index] * Quaternion.Slerp(Quaternion.Identity, sampled, weight)),
                        _ => SkeletonRig.NormalizeSafe(sampled),
                    };
                    break;
                }
                case CastConstants.CurveTranslateX: ApplyScalar(ref pose.Positions[index].X, rig.Bones[index].RestPosition.X, track.SampleScalar(frame), mode, weight, scale: false); break;
                case CastConstants.CurveTranslateY: ApplyScalar(ref pose.Positions[index].Y, rig.Bones[index].RestPosition.Y, track.SampleScalar(frame), mode, weight, scale: false); break;
                case CastConstants.CurveTranslateZ: ApplyScalar(ref pose.Positions[index].Z, rig.Bones[index].RestPosition.Z, track.SampleScalar(frame), mode, weight, scale: false); break;
                case CastConstants.CurveScaleX: ApplyScalar(ref pose.Scales[index].X, rig.Bones[index].RestScale.X, track.SampleScalar(frame), mode, weight, scale: true); break;
                case CastConstants.CurveScaleY: ApplyScalar(ref pose.Scales[index].Y, rig.Bones[index].RestScale.Y, track.SampleScalar(frame), mode, weight, scale: true); break;
                case CastConstants.CurveScaleZ: ApplyScalar(ref pose.Scales[index].Z, rig.Bones[index].RestScale.Z, track.SampleScalar(frame), mode, weight, scale: true); break;
            }
        }
    }

    private string ResolveMode(CurveTrack track, SkeletonRig rig, int boneIndex)
    {
        var channel = track.PropertyName switch
        {
            CastConstants.CurveRotation => CurveChannel.Rotation,
            CastConstants.CurveScaleX or CastConstants.CurveScaleY or CastConstants.CurveScaleZ => CurveChannel.Scale,
            CastConstants.CurveTranslateX or CastConstants.CurveTranslateY or CastConstants.CurveTranslateZ => CurveChannel.Translation,
            _ => CurveChannel.Other,
        };
        if (channel == CurveChannel.Other || ModeOverrides.Count == 0)
        {
            return track.Mode;
        }

        // Match the official CAST importer's hierarchy semantics: overrides apply
        // to descendants, and the first override encountered from root to parent wins.
        var ancestors = new Stack<string>();
        for (var parent = rig.Bones[boneIndex].ParentIndex; parent >= 0; parent = rig.Bones[parent].ParentIndex)
        {
            ancestors.Push(rig.Bones[parent].Name);
        }

        foreach (var ancestor in ancestors)
        {
            var modeOverride = ModeOverrides.FirstOrDefault(x =>
                string.Equals(x.NodeName, ancestor, StringComparison.Ordinal) && x.AppliesTo(channel));
            if (modeOverride is not null)
            {
                return modeOverride.Mode;
            }
        }

        return track.Mode;
    }

    private static void ApplyScalar(ref float current, float rest, float sampled, string mode, float weight, bool scale)
    {
        current = mode switch
        {
            CastConstants.ModeRelative when scale => rest * sampled,
            CastConstants.ModeRelative => rest + sampled,
            CastConstants.ModeAdditive when scale => current * Lerp(1f, sampled, weight),
            CastConstants.ModeAdditive => current + sampled * weight,
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
        var components = propertyName == CastConstants.CurveRotation ? 4 : 1;
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

        var mode = node.StringProperty("m") ?? CastConstants.ModeAbsolute;
        if (!CurveModeOverride.IsSupportedMode(mode))
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

internal enum CurveChannel
{
    Other,
    Translation,
    Rotation,
    Scale,
}

internal sealed record CurveModeOverride(
    string NodeName,
    string Mode,
    bool Translation,
    bool Rotation,
    bool Scale)
{
    public static CurveModeOverride FromNode(CastNode node)
    {
        var nodeName = node.StringProperty("nn");
        var mode = node.StringProperty("m");
        if (string.IsNullOrWhiteSpace(nodeName) || string.IsNullOrWhiteSpace(mode))
        {
            throw new InvalidDataException("曲线模式覆盖缺少 nn 或 m 属性。");
        }

        if (!IsSupportedMode(mode))
        {
            throw new InvalidDataException($"曲线模式覆盖 {nodeName} 的模式无效：{mode}");
        }

        return new CurveModeOverride(
            nodeName,
            mode,
            ReadFlag(node, "ot"),
            ReadFlag(node, "or"),
            ReadFlag(node, "os"));
    }

    public bool AppliesTo(CurveChannel channel) => channel switch
    {
        CurveChannel.Translation => Translation,
        CurveChannel.Rotation => Rotation,
        CurveChannel.Scale => Scale,
        _ => false,
    };

    public static bool IsSupportedMode(string mode) =>
        mode is CastConstants.ModeAbsolute or CastConstants.ModeRelative or CastConstants.ModeAdditive;

    private static bool ReadFlag(CastNode node, string propertyName)
    {
        var property = node.Property(propertyName);
        return property is not null
            && property.Type == "b"
            && property.GetBytes().FirstOrDefault() > 0;
    }
}
