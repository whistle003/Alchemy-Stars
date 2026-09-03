namespace AlchemyStars.Core.Cast;

public static class CastConstants
{
    public const uint Magic = 0x74736163;
    public const uint Version = 1;

    public static readonly uint Root = FourCc("root");
    public static readonly uint Model = FourCc("modl");
    public static readonly uint Animation = FourCc("anim");
    public static readonly uint Curve = FourCc("curv");
    public static readonly uint CurveModeOverride = FourCc("CMOV");
    public static readonly uint Notification = FourCc("ntif");
    public static readonly uint Skeleton = FourCc("skel");
    public static readonly uint Bone = FourCc("bone");
    public static readonly uint Metadata = FourCc("meta");

    public const ulong HashBase = 0x534E495752545250;

    public const string CurveRotation = "rq";
    public const string CurveTranslateX = "tx";
    public const string CurveTranslateY = "ty";
    public const string CurveTranslateZ = "tz";
    public const string CurveScaleX = "sx";
    public const string CurveScaleY = "sy";
    public const string CurveScaleZ = "sz";

    public const string ModeAbsolute = "absolute";
    public const string ModeRelative = "relative";
    public const string ModeAdditive = "additive";

    public static uint FourCc(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        if (value.Length != 4)
        {
            throw new ArgumentException("FourCC 必须正好包含 4 个 ASCII 字符。", nameof(value));
        }

        return (uint)value[0]
             | ((uint)value[1] << 8)
             | ((uint)value[2] << 16)
             | ((uint)value[3] << 24);
    }

    public static string ToFourCc(uint value) => new(
    [
        (char)(value & 0xFF),
        (char)((value >> 8) & 0xFF),
        (char)((value >> 16) & 0xFF),
        (char)((value >> 24) & 0xFF),
    ]);
}
