using System.Numerics;
using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

internal sealed class SkeletonRig
{
    private readonly Dictionary<string, int> _indices = new(StringComparer.Ordinal);

    public List<RigBone> Bones { get; } = [];

    public static SkeletonRig FromModels(params CastDocument[] documents)
    {
        var rig = new SkeletonRig();
        foreach (var document in documents)
        {
            foreach (var model in document.NodesOfType(CastConstants.Model))
            {
                var skeleton = model.ChildOfType(CastConstants.Skeleton);
                if (skeleton is null)
                {
                    continue;
                }

                rig.AddSkeleton(skeleton);
            }
        }

        if (rig.Bones.Count == 0)
        {
            throw new InvalidDataException("模型中没有可用骨架。");
        }

        return rig;
    }

    public bool TryGetIndex(string name, out int index) => _indices.TryGetValue(name, out index);

    public bool ContainsChain(IkChainNames chain) =>
        _indices.ContainsKey(chain.Start)
        && _indices.ContainsKey(chain.Middle)
        && _indices.ContainsKey(chain.End)
        && _indices.ContainsKey(chain.Target);

    public bool CanSolveChain(IkChainNames chain)
    {
        if (!TryGetIndex(chain.Start, out var start)
            || !TryGetIndex(chain.Middle, out var middle)
            || !TryGetIndex(chain.End, out var end)
            || !TryGetIndex(chain.Target, out var target))
        {
            return false;
        }

        return IsDescendantOf(middle, start)
            && IsDescendantOf(end, middle)
            && !IsDescendantOf(target, start);
    }

    private bool IsDescendantOf(int index, int potentialAncestor)
    {
        var cursor = index;
        while (cursor >= 0)
        {
            if (cursor == potentialAncestor)
            {
                return true;
            }

            cursor = Bones[cursor].ParentIndex;
        }

        return false;
    }

    private void AddSkeleton(CastNode skeleton)
    {
        var sourceBones = skeleton.ChildrenOfType(CastConstants.Bone).ToArray();
        var sourceToRig = new int[sourceBones.Length];

        for (var i = 0; i < sourceBones.Length; i++)
        {
            var node = sourceBones[i];
            var name = node.StringProperty("n");
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidDataException($"骨架中的第 {i} 根骨骼没有名称。");
            }

            if (_indices.TryGetValue(name, out var existingIndex))
            {
                sourceToRig[i] = existingIndex;
                continue;
            }

            var sourceParent = ReadParentIndex(node);
            if (sourceParent >= i || sourceParent < -1)
            {
                throw new InvalidDataException($"骨骼 {name} 的父索引 {sourceParent} 无效。CAST 骨骼必须按父级优先排列。");
            }

            var parentIndex = sourceParent < 0 ? -1 : sourceToRig[sourceParent];
            var position = ReadVector3(node.Property("lp"), Vector3.Zero);
            var rotation = NormalizeSafe(ReadQuaternion(node.Property("lr"), Quaternion.Identity));
            var scale = ReadVector3(node.Property("s"), Vector3.One);

            var bone = new RigBone(name, parentIndex, position, rotation, scale);
            sourceToRig[i] = Bones.Count;
            _indices.Add(name, Bones.Count);
            Bones.Add(bone);
        }
    }

    private static int ReadParentIndex(CastNode bone)
    {
        var property = bone.Property("p");
        if (property is null)
        {
            return -1;
        }

        var unsigned = property.GetUInt32s().Single();
        return unchecked((int)unsigned);
    }

    private static Vector3 ReadVector3(CastProperty? property, Vector3 fallback)
    {
        if (property is null)
        {
            return fallback;
        }

        var values = property.GetFloats();
        if (values.Length < 3)
        {
            throw new InvalidDataException($"属性 {property.Name} 缺少 Vector3 分量。");
        }

        return new Vector3(values[0], values[1], values[2]);
    }

    private static Quaternion ReadQuaternion(CastProperty? property, Quaternion fallback)
    {
        if (property is null)
        {
            return fallback;
        }

        var values = property.GetFloats();
        if (values.Length < 4)
        {
            throw new InvalidDataException($"属性 {property.Name} 缺少 Quaternion 分量。");
        }

        return new Quaternion(values[0], values[1], values[2], values[3]);
    }

    internal static Quaternion NormalizeSafe(Quaternion value)
    {
        if (!IsFinite(value) || value.LengthSquared() < 1e-12f)
        {
            return Quaternion.Identity;
        }

        return Quaternion.Normalize(value);
    }

    internal static bool IsFinite(Quaternion value) =>
        float.IsFinite(value.X) && float.IsFinite(value.Y)
        && float.IsFinite(value.Z) && float.IsFinite(value.W);
}

internal sealed record RigBone(
    string Name,
    int ParentIndex,
    Vector3 RestPosition,
    Quaternion RestRotation,
    Vector3 RestScale);
