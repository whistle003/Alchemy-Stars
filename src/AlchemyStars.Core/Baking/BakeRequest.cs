namespace AlchemyStars.Core.Baking;

public sealed record BakeRequest
{
    public required string ArmsModelPath { get; init; }
    public required string WeaponModelPath { get; init; }
    public required string BaseAnimationPath { get; init; }
    public string? AdditiveAnimationPath { get; init; }
    public required string OutputPath { get; init; }
    public string AnimationName { get; init; } = "alchemy_stars_animation";
    public bool EnableLeftHandIk { get; init; } = true;
    public bool EnableRightHandIk { get; init; } = true;
    public IkChainNames LeftHandIk { get; init; } = IkChainNames.LeftHand;
    public IkChainNames RightHandIk { get; init; } = IkChainNames.RightHand;
}

public sealed record IkChainNames(string Start, string Middle, string End, string Target)
{
    public static IkChainNames LeftHand { get; } = new(
        "j_shoulder_le", "j_elbow_le", "j_wrist_le", "tag_ik_loc_le");

    public static IkChainNames RightHand { get; } = new(
        "j_shoulder_ri", "j_elbow_ri", "j_wrist_ri", "tag_ik_loc_ri");
}

