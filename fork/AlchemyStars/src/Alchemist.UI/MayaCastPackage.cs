using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Alchemist.UI;

internal static class MayaCastPackage
{
    private const ulong HashBase = 0x534E495752545250;

    public static void Save(
        string outputPath,
        IEnumerable<Part> parts,
        SkeletonAnimation animation,
        Graphics3DTranslatorFactory translatorFactory)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new InvalidOperationException("Output path has no directory.");
        Directory.CreateDirectory(directory);

        var sources = PartOrdering.ForSkeletonMerge(parts)
            .Where(static part => !string.IsNullOrWhiteSpace(part.FilePath))
            .GroupBy(static part => Path.GetFullPath(part.FilePath), StringComparer.OrdinalIgnoreCase)
            .Select(static group => group.First())
            .ToArray();
        if (sources.Any(part => string.Equals(Path.GetFullPath(part.FilePath), fullOutputPath, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Output CAST cannot overwrite one of its source model files.");

        var animationPath = Path.Combine(directory, $".{Guid.NewGuid():N}.animation.cast");
        var packagePath = Path.Combine(directory, $".{Guid.NewGuid():N}.package.cast");
        try
        {
            translatorFactory.Save(animationPath, animation);
            var nextHash = HashBase;
            var sourceModels = new List<(ModelNode Model, Part Part)>();
            foreach (var part in sources)
            {
                var modelCast = CastReader.Load(Path.GetFullPath(part.FilePath));
                FreshenHashes(modelCast, ref nextHash);
                foreach (var root in modelCast.RootNodes)
                {
                    foreach (var model in DescendantsAndSelf(root).OfType<ModelNode>())
                    {
                        sourceModels.Add((model, part));
                    }
                }
            }
            if (sourceModels.Count == 0)
                throw new InvalidDataException("No source model nodes were found for the Maya CAST package.");

            var mergedModel = sourceModels[0].Model;
            foreach (var (model, part) in sourceModels.Skip(1))
            {
                MergeModel(mergedModel, model, part.ParentBoneTag);
            }

            var packageRoot = new CastNode(CastNodeIdentifier.Root);
            packageRoot.Hash = nextHash++;
            mergedModel.Parent = packageRoot;
            var animationCast = CastReader.Load(animationPath);
            FreshenHashes(animationCast, ref nextHash);
            foreach (var root in animationCast.RootNodes)
            {
                foreach (var animationNode in DescendantsAndSelf(root).OfType<AnimationNode>().ToArray())
                {
                    animationNode.Parent = packageRoot;
                }
            }

            var package = new Cast.NET.Cast([packageRoot]);
            Validate(package);
            CastWriter.Save(packagePath, package);
            Validate(CastReader.Load(packagePath));
            File.Move(packagePath, fullOutputPath, overwrite: true);
        }
        finally
        {
            DeleteIfPresent(animationPath);
            DeleteIfPresent(packagePath);
        }
    }

    private static void MergeModel(ModelNode destination, ModelNode source, string? requestedParentName)
    {
        var sourceSkeleton = source.Skeleton;
        if (sourceSkeleton is not null)
        {
            var destinationSkeleton = destination.Skeleton;
            if (destinationSkeleton is null)
            {
                sourceSkeleton.Parent = destination;
            }
            else
            {
                MergeSkeletonAndRemapModel(destinationSkeleton, sourceSkeleton, source, requestedParentName);
            }
        }

        foreach (var child in source.Children
            .Where(child => !ReferenceEquals(child, sourceSkeleton))
            .ToArray())
        {
            child.Parent = destination;
        }
    }

    private static void MergeSkeletonAndRemapModel(
        SkeletonNode destination,
        SkeletonNode source,
        ModelNode sourceModel,
        string? requestedParentName)
    {
        var destinationBones = destination.Bones.ToList();
        var sourceBones = source.Bones;
        if (sourceBones.Length == 0)
            return;
        var sourceParentIndices = sourceBones.Select(static bone => bone.ParentIndex).ToArray();

        var sourceRoots = sourceBones
            .Select((bone, index) => (bone, index))
            .Where(item => sourceParentIndices[item.index] < 0)
            .ToArray();
        if (sourceRoots.Length != 1)
            throw new InvalidDataException($"Source model skeleton must have exactly one root; found {sourceRoots.Length}.");
        var sourceRootPosition = sourceRoots[0].bone.WorldPosition;
        var sourceRootRotation = sourceRoots[0].bone.WorldRotation;

        var destinationByName = destinationBones
            .Select((bone, index) => (bone, index))
            .ToDictionary(static item => item.bone.Name, static item => item.index, StringComparer.OrdinalIgnoreCase);
        var sourceToDestination = new int[sourceBones.Length];
        var isNewBone = new bool[sourceBones.Length];
        for (var sourceIndex = 0; sourceIndex < sourceBones.Length; sourceIndex++)
        {
            var sourceBone = sourceBones[sourceIndex];
            if (destinationByName.TryGetValue(sourceBone.Name, out var existingIndex))
            {
                sourceToDestination[sourceIndex] = existingIndex;
                continue;
            }

            var destinationIndex = destinationBones.Count;
            destinationBones.Add(sourceBone);
            destinationByName.Add(sourceBone.Name, destinationIndex);
            sourceToDestination[sourceIndex] = destinationIndex;
            isNewBone[sourceIndex] = true;
        }

        int? requestedParentIndex = null;
        if (!string.IsNullOrWhiteSpace(requestedParentName))
        {
            if (!destinationByName.TryGetValue(requestedParentName, out var parentIndex))
                throw new InvalidDataException($"Parent bone '{requestedParentName}' was not found while merging a CAST model.");
            requestedParentIndex = parentIndex;
        }

        var mergedWorldTransforms = new Dictionary<int, (Vector3 Position, Quaternion Rotation)>();
        var newSourceByDestination = Enumerable.Range(0, sourceBones.Length)
            .Where(index => isNewBone[index])
            .ToDictionary(index => sourceToDestination[index]);
        var resolvingWorldTransforms = new HashSet<int>();
        (Vector3 Position, Quaternion Rotation) ResolveWorldTransform(int destinationIndex)
        {
            if (mergedWorldTransforms.TryGetValue(destinationIndex, out var cached))
                return cached;

            var destinationBone = destinationBones[destinationIndex];
            if (!newSourceByDestination.TryGetValue(destinationIndex, out var sourceIndex))
            {
                var existing = (destinationBone.WorldPosition, destinationBone.WorldRotation);
                mergedWorldTransforms[destinationIndex] = existing;
                return existing;
            }
            if (!resolvingWorldTransforms.Add(destinationIndex))
                throw new InvalidDataException($"Cyclic merged skeleton at bone '{destinationBone.Name}'.");

            var sourceBone = sourceBones[sourceIndex];
            var sourceParentIndex = sourceParentIndices[sourceIndex];
            var parentIndex = sourceParentIndex >= 0
                ? sourceToDestination[sourceParentIndex]
                : requestedParentIndex ?? -1;
            (Vector3 Position, Quaternion Rotation) result;
            if (parentIndex < 0)
            {
                result = (sourceBone.LocalPosition, sourceBone.LocalRotation);
            }
            else
            {
                var parent = ResolveWorldTransform(parentIndex);
                result = (
                    Vector3.Transform(sourceBone.LocalPosition, parent.Rotation) + parent.Position,
                    Quaternion.Normalize(parent.Rotation * sourceBone.LocalRotation));
            }

            mergedWorldTransforms[destinationIndex] = result;
            resolvingWorldTransforms.Remove(destinationIndex);
            return result;
        }

        var boneHashRemap = new Dictionary<ulong, ulong>();
        for (var sourceIndex = 0; sourceIndex < sourceBones.Length; sourceIndex++)
        {
            var sourceBone = sourceBones[sourceIndex];
            var destinationIndex = sourceToDestination[sourceIndex];
            var destinationBone = destinationBones[destinationIndex];
            if (sourceBone.Hash != 0 && destinationBone.Hash != 0)
                boneHashRemap[sourceBone.Hash] = destinationBone.Hash;

            if (!isNewBone[sourceIndex])
                continue;

            var sourceParentIndex = sourceParentIndices[sourceIndex];
            var parentIndex = sourceParentIndex >= 0
                ? sourceToDestination[sourceParentIndex]
                : requestedParentIndex ?? -1;
            sourceBone.AddValue("p", parentIndex < 0 ? uint.MaxValue : (uint)parentIndex);
            var world = ResolveWorldTransform(destinationIndex);
            sourceBone.AddValue("wp", world.Position);
            sourceBone.AddValue("wr", new Vector4(world.Rotation.X, world.Rotation.Y, world.Rotation.Z, world.Rotation.W));
        }

        RemapHashReferences(sourceModel, boneHashRemap);
        RemapMeshWeights(sourceModel.Meshes, sourceToDestination);

        var sourceRoot = sourceRoots[0];
        var destinationRoot = destinationBones[sourceToDestination[sourceRoot.index]];
        var rotation = Quaternion.Normalize(destinationRoot.WorldRotation * Quaternion.Inverse(sourceRootRotation));
        var translation = destinationRoot.WorldPosition - sourceRootPosition;
        TransformMeshes(sourceModel.Meshes, rotation, translation);

        foreach (var sourceIndex in Enumerable.Range(0, sourceBones.Length).Where(index => isNewBone[index]))
            sourceBones[sourceIndex].Parent = destination;
        foreach (var auxiliary in source.Children.Where(static child => child.Identifier != CastNodeIdentifier.Bone).ToArray())
            auxiliary.Parent = destination;
    }

    private static void RemapHashReferences(CastNode node, IReadOnlyDictionary<ulong, ulong> remap)
    {
        foreach (var property in DescendantsAndSelf(node)
            .SelectMany(static descendant => descendant.Properties.Values)
            .OfType<CastArrayProperty<ulong>>())
        {
            for (var i = 0; i < property.Values.Count; i++)
            {
                if (remap.TryGetValue(property.Values[i], out var replacement))
                    property.Values[i] = replacement;
            }
        }
    }

    private static void RemapMeshWeights(IEnumerable<MeshNode> meshes, IReadOnlyList<int> sourceToDestination)
    {
        foreach (var mesh in meshes)
        {
            if (mesh.VertexWeightBoneBuffer is null)
                continue;

            var mapped = mesh.VertexWeightBoneBuffer switch
            {
                CastArrayProperty<byte> values => values.Values.Select(value => (uint)sourceToDestination[value]).ToArray(),
                CastArrayProperty<ushort> values => values.Values.Select(value => (uint)sourceToDestination[value]).ToArray(),
                CastArrayProperty<uint> values => values.Values.Select(value => (uint)sourceToDestination[checked((int)value)]).ToArray(),
                _ => throw new InvalidDataException("Unsupported CAST mesh bone-index buffer type."),
            };
            var maximum = mapped.Length == 0 ? 0 : mapped.Max();
            mesh.Properties["wb"] = maximum switch
            {
                <= byte.MaxValue => new CastArrayProperty<byte>(mapped.Select(static value => (byte)value)),
                <= ushort.MaxValue => new CastArrayProperty<ushort>(mapped.Select(static value => (ushort)value)),
                _ => new CastArrayProperty<uint>(mapped),
            };
        }
    }

    private static void TransformMeshes(IEnumerable<MeshNode> meshes, Quaternion rotation, Vector3 translation)
    {
        foreach (var mesh in meshes)
        {
            for (var i = 0; i < mesh.VertexPositionBuffer.Values.Count; i++)
                mesh.VertexPositionBuffer.Values[i] = Vector3.Transform(mesh.VertexPositionBuffer.Values[i], rotation) + translation;
            TransformDirections(mesh.VertexNormalBuffer, rotation);
            TransformDirections(mesh.VertexTangentBuffer, rotation);
        }
    }

    private static void TransformDirections(CastArrayProperty<Vector3>? property, Quaternion rotation)
    {
        if (property is null)
            return;
        for (var i = 0; i < property.Values.Count; i++)
        {
            var transformed = Vector3.Transform(property.Values[i], rotation);
            property.Values[i] = transformed == Vector3.Zero ? Vector3.Zero : Vector3.Normalize(transformed);
        }
    }

    private static IEnumerable<CastNode> DescendantsAndSelf(CastNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        {
            foreach (var descendant in DescendantsAndSelf(child))
            {
                yield return descendant;
            }
        }
    }

    private static void FreshenHashes(Cast.NET.Cast cast, ref ulong nextHash)
    {
        foreach (var root in cast.RootNodes)
        {
            var nodes = DescendantsAndSelf(root).ToArray();
            var remap = new Dictionary<ulong, ulong>();
            foreach (var node in nodes)
            {
                var replacement = nextHash++;
                if (node.Hash != 0)
                {
                    remap.TryAdd(node.Hash, replacement);
                }
                node.Hash = replacement;
            }

            foreach (var property in nodes
                .SelectMany(static x => x.Properties.Values)
                .OfType<CastArrayProperty<ulong>>())
            {
                for (var i = 0; i < property.Values.Count; i++)
                {
                    if (remap.TryGetValue(property.Values[i], out var replacement))
                    {
                        property.Values[i] = replacement;
                    }
                }
            }
        }
    }

    private static void Validate(Cast.NET.Cast cast)
    {
        var nodes = cast.RootNodes.SelectMany(DescendantsAndSelf).ToArray();
        var animationCount = nodes.Count(static x => x.Identifier == CastNodeIdentifier.Animation);
        var modelCount = nodes.Count(static x => x.Identifier == CastNodeIdentifier.Model);
        if (animationCount != 1)
        {
            throw new InvalidDataException($"Maya CAST package must contain exactly one animation; found {animationCount}.");
        }
        if (modelCount != 1)
        {
            throw new InvalidDataException($"Maya CAST package must contain exactly one merged model; found {modelCount}.");
        }
        var model = nodes.OfType<ModelNode>().Single();
        var skeleton = model.Skeleton ?? throw new InvalidDataException("Merged Maya CAST model has no skeleton.");
        var bones = skeleton.Bones;
        if (bones.Count(static bone => bone.ParentIndex < 0) != 1)
            throw new InvalidDataException("Merged Maya CAST model must contain exactly one skeleton root.");
        var duplicateBoneNames = bones
            .GroupBy(static bone => bone.Name, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .ToArray();
        if (duplicateBoneNames.Length != 0)
            throw new InvalidDataException("Merged Maya CAST model contains duplicate bones: " + string.Join(", ", duplicateBoneNames));
        if (nodes.Select(static x => x.Hash).Distinct().Count() != nodes.Length)
        {
            throw new InvalidDataException("Maya CAST package contains duplicate node hashes.");
        }
    }

    private static void DeleteIfPresent(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}
