using AlchemyStars.Core.Cast;

namespace AlchemyStars.Core.Baking;

public sealed class AlchemyStarsBaker
{
    public InputAnalysis Analyze(
        string armsModelPath,
        string weaponModelPath,
        string animationPath,
        string? additiveAnimationPath = null)
    {
        ValidateInputPath(armsModelPath, "手臂模型");
        ValidateInputPath(weaponModelPath, "武器模型");
        ValidateInputPath(animationPath, "主动画");
        if (!string.IsNullOrWhiteSpace(additiveAnimationPath))
        {
            ValidateInputPath(additiveAnimationPath, "Additive 动画");
        }

        var arms = CastIo.Load(armsModelPath);
        var weapon = CastIo.Load(weaponModelPath);
        var animation = AnimationClip.Load(CastIo.Load(animationPath), animationPath);
        var additive = string.IsNullOrWhiteSpace(additiveAnimationPath)
            ? null
            : AnimationClip.Load(CastIo.Load(additiveAnimationPath), additiveAnimationPath);
        var rig = SkeletonRig.FromModels(arms, weapon);
        var armsNames = GetBoneNames(arms);
        var weaponNames = GetBoneNames(weapon);
        var missingTargets = animation.TargetNames.Where(x => !rig.TryGetIndex(x, out _)).Order().ToArray();

        return new InputAnalysis
        {
            ArmsBoneCount = armsNames.Count,
            WeaponBoneCount = weaponNames.Count,
            SharedBoneCount = armsNames.Intersect(weaponNames, StringComparer.Ordinal).Count(),
            CombinedBoneCount = rig.Bones.Count,
            AnimationTargetCount = animation.TargetNames.Count,
            MissingAnimationTargetCount = missingTargets.Length,
            MissingTargets = missingTargets,
            FrameStart = animation.FrameStart,
            FrameEnd = animation.FrameEnd,
            Framerate = animation.Framerate,
            AdditiveFrameCount = additive is null ? 0 : additive.FrameEnd - additive.FrameStart + 1,
            HasLeftHandIkChain = rig.CanSolveChain(IkChainNames.LeftHand),
            HasRightHandIkChain = rig.CanSolveChain(IkChainNames.RightHand),
        };
    }

    public BakeReport Bake(BakeRequest request, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateInputPath(request.ArmsModelPath, "手臂模型");
        ValidateInputPath(request.WeaponModelPath, "武器模型");
        ValidateInputPath(request.BaseAnimationPath, "主动画");
        if (!string.IsNullOrWhiteSpace(request.AdditiveAnimationPath))
        {
            ValidateInputPath(request.AdditiveAnimationPath, "Additive 动画");
        }

        if (string.IsNullOrWhiteSpace(request.AnimationName))
        {
            throw new ArgumentException("输出动画名称不能为空。", nameof(request));
        }

        progress?.Report(2);
        var arms = CastIo.Load(request.ArmsModelPath);
        var weapon = CastIo.Load(request.WeaponModelPath);
        var baseClip = AnimationClip.Load(CastIo.Load(request.BaseAnimationPath), request.BaseAnimationPath);
        var additiveClip = string.IsNullOrWhiteSpace(request.AdditiveAnimationPath)
            ? null
            : AnimationClip.Load(CastIo.Load(request.AdditiveAnimationPath), request.AdditiveAnimationPath);
        var rig = SkeletonRig.FromModels(arms, weapon);
        progress?.Report(10);

        var warnings = new List<string>();
        var missingBaseTargets = baseClip.TargetNames.Where(x => !rig.TryGetIndex(x, out _)).Order().ToArray();
        if (missingBaseTargets.Length > 0)
        {
            warnings.Add($"主动画有 {missingBaseTargets.Length} 个目标不在合并骨架中，已跳过。");
        }

        if (additiveClip is not null)
        {
            var missingLayerTargets = additiveClip.TargetNames.Where(x => !rig.TryGetIndex(x, out _)).Count();
            if (missingLayerTargets > 0)
            {
                warnings.Add($"Additive 层有 {missingLayerTargets} 个目标不在合并骨架中，已跳过。");
            }
        }

        var leftRequested = request.EnableLeftHandIk;
        var rightRequested = request.EnableRightHandIk;
        var leftAvailable = rig.CanSolveChain(request.LeftHandIk);
        var rightAvailable = rig.CanSolveChain(request.RightHandIk);
        if (leftRequested && !leftAvailable)
        {
            warnings.Add("左手 IK 链不完整，或目标位于被求解链的后代，已安全跳过左手 IK。");
        }

        if (rightRequested && !rightAvailable)
        {
            warnings.Add("右手 IK 目标位于右臂链后代（j_gun 由右手腕驱动），为避免循环依赖已安全跳过右手 IK。");
        }

        var frameCount = checked(baseClip.FrameEnd - baseClip.FrameStart + 1);
        var frames = new List<PoseFrame>(frameCount);
        var leftApplied = false;
        var rightApplied = false;
        for (var frame = baseClip.FrameStart; frame <= baseClip.FrameEnd; frame++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pose = new PoseFrame(rig);
            baseClip.Apply(pose, rig, frame, forceAdditive: false);
            additiveClip?.Apply(pose, rig, frame, forceAdditive: true);
            pose.RecalculateWorld(rig);

            if (leftRequested && leftAvailable)
            {
                leftApplied |= TwoBoneIkBaker.TryApply(pose, rig, request.LeftHandIk);
            }

            if (rightRequested && rightAvailable)
            {
                rightApplied |= TwoBoneIkBaker.TryApply(pose, rig, request.RightHandIk);
            }

            frames.Add(pose);
            var completed = frame - baseClip.FrameStart + 1;
            progress?.Report(10 + (int)(completed / (double)frameCount * 65));
        }

        var output = CastComposer.Compose(arms, weapon, baseClip, rig, frames, request.AnimationName);
        OutputValidator.Validate(output, rig, baseClip.FrameStart, baseClip.FrameEnd);
        progress?.Report(80);
        CastIo.Save(output, request.OutputPath);
        progress?.Report(92);

        var roundTrip = CastIo.Load(request.OutputPath);
        OutputValidator.Validate(roundTrip, rig, baseClip.FrameStart, baseClip.FrameEnd);
        var outputAnimation = roundTrip.NodesOfType(CastConstants.Animation).Single();
        var curveCount = outputAnimation.ChildrenOfType(CastConstants.Curve).Count();
        progress?.Report(100);

        return new BakeReport
        {
            OutputPath = Path.GetFullPath(request.OutputPath),
            AnimationName = request.AnimationName,
            ModelCount = roundTrip.NodesOfType(CastConstants.Model).Count(),
            AnimationCount = roundTrip.NodesOfType(CastConstants.Animation).Count(),
            BoneCount = rig.Bones.Count,
            AnimatedBoneCount = baseClip.TargetNames.Count(x => rig.TryGetIndex(x, out _)),
            CurveCount = curveCount,
            FrameStart = baseClip.FrameStart,
            FrameEnd = baseClip.FrameEnd,
            Framerate = baseClip.Framerate,
            LeftHandIkApplied = leftApplied,
            RightHandIkApplied = rightApplied,
            FileSize = new FileInfo(request.OutputPath).Length,
            Warnings = warnings,
        };
    }

    private static HashSet<string> GetBoneNames(CastDocument document)
    {
        return document.NodesOfType(CastConstants.Bone)
            .Select(x => x.StringProperty("n"))
            .Where(static x => !string.IsNullOrWhiteSpace(x))
            .Select(static x => x!)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static void ValidateInputPath(string path, string label)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException($"{label}路径不能为空。", nameof(path));
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"找不到{label}：{path}", path);
        }

        if (!string.Equals(Path.GetExtension(path), ".cast", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"{label}必须是 .cast 文件：{path}", nameof(path));
        }
    }
}
