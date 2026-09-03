using System.Numerics;
using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

internal static class CastComposer
{
    public static CastDocument Compose(
        CastDocument arms,
        CastDocument weapon,
        AnimationClip baseClip,
        SkeletonRig rig,
        IReadOnlyList<PoseFrame> frames,
        string animationName)
    {
        var output = new CastDocument();
        AddModelRoots(output, arms, "AlchemyStars_Viewhands");
        AddModelRoots(output, weapon, "AlchemyStars_Weapon");
        output.Roots.Add(CreateAnimationRoot(baseClip, rig, frames, animationName));
        FreshenHashes(output);
        return output;
    }

    private static void AddModelRoots(CastDocument target, CastDocument source, string modelName)
    {
        foreach (var sourceRoot in source.Roots)
        {
            var root = sourceRoot.CloneDeep();
            RemoveNodes(root, CastConstants.Animation);
            var models = root.DescendantsAndSelf().Where(x => x.Identifier == CastConstants.Model).ToArray();
            if (models.Length == 0)
            {
                continue;
            }

            for (var i = 0; i < models.Length; i++)
            {
                var suffix = models.Length == 1 ? string.Empty : $"_{i + 1}";
                models[i].SetProperty(new CastProperty("n", "s", modelName + suffix));
            }

            target.Roots.Add(root);
        }
    }

    private static void RemoveNodes(CastNode node, uint identifier)
    {
        node.Children.RemoveAll(x => x.Identifier == identifier);
        foreach (var child in node.Children)
        {
            RemoveNodes(child, identifier);
        }
    }

    private static CastNode CreateAnimationRoot(
        AnimationClip source,
        SkeletonRig rig,
        IReadOnlyList<PoseFrame> frames,
        string animationName)
    {
        var root = new CastNode(CastConstants.Root, 0);
        var metadata = source.SourceNode.DescendantsAndSelf()
            .FirstOrDefault(x => x.Identifier == CastConstants.Metadata)?.CloneDeep()
            ?? new CastNode(CastConstants.Metadata, 0);
        metadata.SetProperty(new CastProperty("s", "s", "Alchemy Stars 1.0"));
        root.Children.Add(metadata);

        var animation = new CastNode(CastConstants.Animation, 0);
        animation.SetProperty(new CastProperty("n", "s", animationName));
        animation.SetProperty(new CastProperty("fr", "f", new[] { source.Framerate }));
        animation.SetProperty(new CastProperty("lo", "b", new[] { source.Looping ? (byte)1 : (byte)0 }));
        root.Children.Add(animation);

        var frameValues = Enumerable.Range(source.FrameStart, frames.Count).ToArray();
        for (var boneIndex = 0; boneIndex < rig.Bones.Count; boneIndex++)
        {
            var boneName = rig.Bones[boneIndex].Name;
            animation.Children.Add(CreateQuaternionCurve(
                boneName,
                "rq",
                frameValues,
                frames.Select(x => x.Rotations[boneIndex])));
            animation.Children.Add(CreateScalarCurve(
                boneName,
                "tx",
                frameValues,
                frames.Select(x => x.Positions[boneIndex].X)));
            animation.Children.Add(CreateScalarCurve(
                boneName,
                "ty",
                frameValues,
                frames.Select(x => x.Positions[boneIndex].Y)));
            animation.Children.Add(CreateScalarCurve(
                boneName,
                "tz",
                frameValues,
                frames.Select(x => x.Positions[boneIndex].Z)));
        }

        foreach (var notification in source.SourceNode.ChildrenOfType(CastConstants.Notification))
        {
            animation.Children.Add(notification.CloneDeep());
        }

        return root;
    }

    private static CastNode CreateQuaternionCurve(
        string nodeName,
        string propertyName,
        IReadOnlyList<int> frames,
        IEnumerable<Quaternion> values)
    {
        var flattened = values
            .Select(SkeletonRig.NormalizeSafe)
            .SelectMany(static x => new[] { x.X, x.Y, x.Z, x.W })
            .ToArray();
        var curve = CreateCurveHeader(nodeName, propertyName, frames);
        curve.SetProperty(new CastProperty("kv", "4v", flattened));
        return curve;
    }

    private static CastNode CreateScalarCurve(
        string nodeName,
        string propertyName,
        IReadOnlyList<int> frames,
        IEnumerable<float> values)
    {
        var curve = CreateCurveHeader(nodeName, propertyName, frames);
        curve.SetProperty(new CastProperty("kv", "f", values.ToArray()));
        return curve;
    }

    private static CastNode CreateCurveHeader(string nodeName, string propertyName, IReadOnlyList<int> frames)
    {
        var curve = new CastNode(CastConstants.Curve, 0);
        curve.SetProperty(new CastProperty("nn", "s", nodeName));
        curve.SetProperty(new CastProperty("kp", "s", propertyName));
        curve.SetProperty(CreateFrameProperty(frames));
        curve.SetProperty(new CastProperty("m", "s", "absolute"));
        return curve;
    }

    private static CastProperty CreateFrameProperty(IReadOnlyList<int> frames)
    {
        var maximum = frames.Count == 0 ? 0 : frames.Max();
        return maximum switch
        {
            <= byte.MaxValue => new CastProperty("kb", "b", frames.Select(checked((Func<int, byte>)(x => (byte)x))).ToArray()),
            <= ushort.MaxValue => new CastProperty("kb", "h", frames.Select(checked((Func<int, ushort>)(x => (ushort)x))).ToArray()),
            _ => new CastProperty("kb", "i", frames.Select(checked((Func<int, uint>)(x => (uint)x))).ToArray()),
        };
    }

    private static void FreshenHashes(CastDocument document)
    {
        var nextHash = CastConstants.HashBase;
        foreach (var root in document.Roots)
        {
            var nodes = root.DescendantsAndSelf().ToArray();
            var remap = new Dictionary<ulong, ulong>();
            foreach (var node in nodes)
            {
                var newHash = nextHash++;
                remap.TryAdd(node.Hash, newHash);
                node.Hash = newHash;
            }

            foreach (var property in nodes.SelectMany(static x => x.Properties).Where(static x => x.Type == "l"))
            {
                var values = property.GetUInt64s();
                for (var i = 0; i < values.Length; i++)
                {
                    if (remap.TryGetValue(values[i], out var replacement))
                    {
                        values[i] = replacement;
                    }
                }
            }
        }
    }
}

