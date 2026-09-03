namespace AlchemyStars.Core.Baking;

public sealed record BakeReport
{
    public required string OutputPath { get; init; }
    public required string AnimationName { get; init; }
    public required int ModelCount { get; init; }
    public required int AnimationCount { get; init; }
    public required int BoneCount { get; init; }
    public required int AnimatedBoneCount { get; init; }
    public required int CurveCount { get; init; }
    public required int FrameStart { get; init; }
    public required int FrameEnd { get; init; }
    public required float Framerate { get; init; }
    public required bool LeftHandIkApplied { get; init; }
    public required bool RightHandIkApplied { get; init; }
    public required long FileSize { get; init; }
    public IReadOnlyList<string> Warnings { get; init; } = [];

    public int FrameCount => FrameEnd - FrameStart + 1;
}

public sealed record InputAnalysis
{
    public required int ArmsBoneCount { get; init; }
    public required int WeaponBoneCount { get; init; }
    public required int SharedBoneCount { get; init; }
    public required int CombinedBoneCount { get; init; }
    public required int AnimationTargetCount { get; init; }
    public required int MissingAnimationTargetCount { get; init; }
    public required int FrameStart { get; init; }
    public required int FrameEnd { get; init; }
    public required float Framerate { get; init; }
    public required int AdditiveFrameCount { get; init; }
    public required bool HasLeftHandIkChain { get; init; }
    public required bool HasRightHandIkChain { get; init; }
    public IReadOnlyList<string> MissingTargets { get; init; } = [];
}

