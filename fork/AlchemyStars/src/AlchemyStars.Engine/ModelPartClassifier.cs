using Cast.NET;
using Cast.NET.Nodes;

namespace AlchemyStars.Engine;

public enum ModelPartEvidence
{
    BilateralArmChains,
    SingleArmChain,
    ViewModelAnchors,
    WeaponRoot,
    WeaponAttachmentTags,
    WeaponMechanismBones,
    AttachmentRoot,
    FileNameHint,
    NoSkeleton,
}

public sealed record ModelPartClassification(
    ModelPartKind Kind,
    float Confidence,
    string RecommendedParentBone,
    int BoneCount,
    IReadOnlyList<ModelPartEvidence> Evidence);

/// <summary>
/// Classifies a CAST model from skeleton topology. File names are deliberately
/// only a weak tie-breaker; the result is a recommendation that callers may override.
/// </summary>
public static class ModelPartClassifier
{
    public static ModelPartClassification Classify(string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        var fullPath = Path.GetFullPath(filePath);
        if (!File.Exists(fullPath))
            throw new FileNotFoundException("Model part file was not found.", fullPath);
        if (!string.Equals(Path.GetExtension(fullPath), ".cast", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException("Model part classification reads CAST files only.");

        var cast = CastReader.Load(fullPath);
        var models = cast.RootNodes.SelectMany(DescendantsAndSelf).OfType<ModelNode>().ToArray();
        if (models.Length == 0)
            throw new InvalidDataException($"Model part has no CAST model: {fullPath}");

        var skeletons = models.Select(model => model.Skeleton).Where(skeleton => skeleton is not null).Cast<SkeletonNode>().ToArray();
        var bones = skeletons.SelectMany(skeleton => skeleton.Bones).ToArray();
        if (bones.Length == 0)
            return new(ModelPartKind.Attachment, 0.25f, string.Empty, 0, [ModelPartEvidence.NoSkeleton]);

        var names = bones.Select(bone => bone.Name).Where(name => !string.IsNullOrWhiteSpace(name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var rootNames = bones.Where(bone => bone.ParentIndex < 0).Select(bone => bone.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var fileStem = Path.GetFileNameWithoutExtension(fullPath);
        var evidence = new Dictionary<ModelPartKind, List<ModelPartEvidence>>
        {
            [ModelPartKind.ViewHands] = [],
            [ModelPartKind.Weapon] = [],
            [ModelPartKind.Attachment] = [],
        };
        var scores = new Dictionary<ModelPartKind, int>
        {
            [ModelPartKind.ViewHands] = 0,
            [ModelPartKind.Weapon] = 0,
            [ModelPartKind.Attachment] = 0,
        };
        void Add(ModelPartKind kind, int score, ModelPartEvidence signal)
        {
            scores[kind] += score;
            if (!evidence[kind].Contains(signal)) evidence[kind].Add(signal);
        }

        var leftArm = HasArmChain(names, true);
        var rightArm = HasArmChain(names, false);
        if (leftArm && rightArm) Add(ModelPartKind.ViewHands, 120, ModelPartEvidence.BilateralArmChains);
        else if (leftArm || rightArm) Add(ModelPartKind.ViewHands, 38, ModelPartEvidence.SingleArmChain);

        var viewAnchors = new[] { "tag_origin", "tag_view", "j_mainroot", "tag_weapon" }.Count(names.Contains);
        if (viewAnchors > 0) Add(ModelPartKind.ViewHands, Math.Min(32, viewAnchors * 8), ModelPartEvidence.ViewModelAnchors);

        var hasWeaponRoot = rootNames.Any(IsWeaponRoot);
        if (hasWeaponRoot) Add(ModelPartKind.Weapon, 90, ModelPartEvidence.WeaponRoot);
        var weaponTags = names.Count(IsWeaponAttachmentTag);
        if (weaponTags > 0) Add(ModelPartKind.Weapon, Math.Min(42, weaponTags * 4), ModelPartEvidence.WeaponAttachmentTags);
        var mechanismBones = names.Count(IsWeaponMechanismBone);
        if (mechanismBones > 0) Add(ModelPartKind.Weapon, Math.Min(24, mechanismBones * 3), ModelPartEvidence.WeaponMechanismBones);

        if (rootNames.Any(IsAttachmentRoot)) Add(ModelPartKind.Attachment, 82, ModelPartEvidence.AttachmentRoot);
        if (bones.Length <= 48) scores[ModelPartKind.Attachment] += 10;

        if (ContainsAny(fileStem, "viewhands", "view_hands", "vm_arms", "_arms_"))
            Add(ModelPartKind.ViewHands, 8, ModelPartEvidence.FileNameHint);
        if (ContainsAny(fileStem, "weapon", "_rec", "receiver"))
            Add(ModelPartKind.Weapon, 8, ModelPartEvidence.FileNameHint);
        if (ContainsAny(fileStem, "attachment", "att_", "_att_"))
            Add(ModelPartKind.Attachment, 8, ModelPartEvidence.FileNameHint);

        // A complete bilateral arm rig is conclusive even though it commonly
        // contains a wrist-helper j_gun and many weapon attachment tags.
        var kind = leftArm && rightArm
            ? ModelPartKind.ViewHands
            : scores.OrderByDescending(pair => pair.Value).ThenByDescending(pair => pair.Key == ModelPartKind.Attachment).First().Key;
        var orderedScores = scores.Values.OrderDescending().ToArray();
        var top = orderedScores[0];
        var gap = top - orderedScores[1];
        var confidence = kind switch
        {
            ModelPartKind.ViewHands when leftArm && rightArm => 0.99f,
            ModelPartKind.Weapon when hasWeaponRoot => Math.Clamp(0.9f + gap / 500f, 0.9f, 0.99f),
            ModelPartKind.Attachment when rootNames.Any(IsAttachmentRoot) => Math.Clamp(0.84f + gap / 500f, 0.84f, 0.96f),
            _ when top >= 55 && gap >= 25 => 0.78f,
            _ when top >= 35 && gap >= 12 => 0.64f,
            _ => 0.42f,
        };
        return new(kind, confidence, kind == ModelPartKind.Weapon ? "tag_weapon" : string.Empty,
            bones.Length, evidence[kind].ToArray());
    }

    private static bool HasArmChain(IReadOnlySet<string> names, bool left) =>
        names.Any(name => IsSideBone(name, "shoulder", left))
        && names.Any(name => IsSideBone(name, "elbow", left))
        && names.Any(name => IsSideBone(name, "wrist", left));

    private static bool IsSideBone(string name, string segment, bool left)
    {
        if (!name.Contains(segment, StringComparison.OrdinalIgnoreCase)) return false;
        return left
            ? name.EndsWith("_le", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_left", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_l", StringComparison.OrdinalIgnoreCase)
            : name.EndsWith("_ri", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_right", StringComparison.OrdinalIgnoreCase)
                || name.EndsWith("_r", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsWeaponAttachmentTag(string name) => name.StartsWith("tag_", StringComparison.OrdinalIgnoreCase)
        && ContainsAny(name, "align_gun", "barrel_attach", "brass", "mag_attach", "pistolgrip_attach",
            "reflex", "scope", "sight_", "stock_attach", "thermal", "holo");

    private static bool IsWeaponMechanismBone(string name) => name.StartsWith("j_", StringComparison.OrdinalIgnoreCase)
        && ContainsAny(name, "slide", "bolt", "trigger", "hammer", "mag", "clip", "ammo");

    private static bool IsWeaponRoot(string name) => name.Equals("j_gun", StringComparison.OrdinalIgnoreCase)
        || name.Equals("tag_weapon", StringComparison.OrdinalIgnoreCase)
        || name.Contains("weapon", StringComparison.OrdinalIgnoreCase)
        || name.Contains("gun", StringComparison.OrdinalIgnoreCase);

    private static bool IsAttachmentRoot(string name) => name.StartsWith("tag_", StringComparison.OrdinalIgnoreCase)
        && name.Contains("attach", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsAny(string value, params string[] fragments) =>
        fragments.Any(fragment => value.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<CastNode> DescendantsAndSelf(CastNode node)
    {
        yield return node;
        foreach (var child in node.Children)
            foreach (var descendant in DescendantsAndSelf(child))
                yield return descendant;
    }
}
