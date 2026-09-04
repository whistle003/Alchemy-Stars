using RedFox.Graphics3D;
using RedFox.Graphics3D.Skeletal;
using RedFox.Graphics3D.Translation;
using System.IO;
using System.Numerics;

namespace Alchemist.UI;

internal static class MergedSkeletonBuilder
{
    public static Skeleton Build(
        IEnumerable<Part> parts,
        bool matchOldCallOfDuty,
        Graphics3DTranslatorFactory translatorFactory)
    {
        var skeleton = new Skeleton("Alchemy Stars Merged Skeleton");
        var bonesByName = new Dictionary<string, SkeletonBone>(StringComparer.OrdinalIgnoreCase);

        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part.FilePath) || !File.Exists(part.FilePath))
                throw new FileNotFoundException("Model part could not be found.", part.FilePath);

            var loadedModel = translatorFactory.Load<Model>(part.FilePath);
            if (loadedModel.Skeleton is null || loadedModel.Skeleton.Bones.Count == 0)
                continue;

            _ = loadedModel.Skeleton.Bones.FirstOrDefault(bone => bone.Parent is null)
                ?? throw new InvalidDataException($"Model has no root bone: {part.FilePath}");
            var requestedParent = string.IsNullOrWhiteSpace(part.ParentBoneTag)
                ? null
                : bonesByName.GetValueOrDefault(part.ParentBoneTag);
            if (!string.IsNullOrWhiteSpace(part.ParentBoneTag) && requestedParent is null)
            {
                throw new InvalidDataException(
                    $"Parent bone '{part.ParentBoneTag}' was not found before loading: {part.FilePath}");
            }

            AddBones(skeleton, loadedModel.Skeleton, part, requestedParent, matchOldCallOfDuty, bonesByName);
        }

        if (skeleton.Bones.Count == 0)
            throw new InvalidDataException("No model part contained a usable skeleton.");

        skeleton.AssignBoneIndices();
        skeleton.GenerateGlobalTransforms();
        return skeleton;
    }

    private static void AddBones(
        Skeleton destination,
        Skeleton source,
        Part part,
        SkeletonBone? requestedParent,
        bool matchOldCallOfDuty,
        Dictionary<string, SkeletonBone> bonesByName)
    {
        var boneMap = new Dictionary<SkeletonBone, SkeletonBone>();
        foreach (var sourceBone in source.EnumerateHierarchy())
        {
            var boneName = sourceBone.Name;
            if (string.IsNullOrWhiteSpace(boneName))
                throw new InvalidDataException($"Model contains an unnamed bone: {part.FilePath}");
            if (bonesByName.TryGetValue(boneName, out var existingBone))
            {
                boneMap[sourceBone] = existingBone;
                continue;
            }

            var parent = sourceBone.Parent is null
                ? requestedParent
                : boneMap[sourceBone.Parent];
            var resetViewHands = part.Type == PartType.ViewHands && matchOldCallOfDuty;
            var mergedBone = new SkeletonBone(boneName)
            {
                Parent = parent,
                BaseLocalRotation = resetViewHands ? Quaternion.Identity : sourceBone.BaseLocalRotation,
                BaseLocalTranslation = resetViewHands ? Vector3.Zero : sourceBone.BaseLocalTranslation,
                BaseWorldRotation = sourceBone.BaseWorldRotation,
                BaseWorldTranslation = sourceBone.BaseWorldTranslation,
                BaseScale = sourceBone.BaseScale,
                Scale = sourceBone.Scale,
                CanAnimate = sourceBone.CanAnimate,
            };
            destination.AddBone(mergedBone);
            bonesByName.Add(boneName, mergedBone);
            boneMap[sourceBone] = mergedBone;
        }
    }
}
