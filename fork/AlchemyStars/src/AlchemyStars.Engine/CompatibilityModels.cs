using System.Diagnostics;
using System.Globalization;

namespace Alchemist.UI;

internal enum PartType
{
    ViewHands,
    Weapon,
    Attachment,
}

internal enum AnimationLayerType
{
    Normal,
    Additive,
    Gesture,
    GesturePose,
}

internal sealed class Part
{
    public string FilePath { get; set; } = string.Empty;
    public string ParentBoneTag { get; set; } = string.Empty;
    public PartType Type { get; set; } = PartType.Attachment;
}

internal sealed class AnimationLayer
{
    public string Name { get; set; } = string.Empty;
    public int? Offset { get; set; }
    public AnimationLayerType Type { get; set; } = AnimationLayerType.Additive;
}

internal sealed class Animation
{
    public float OutputFramerate { get; set; } = 30;
    public string Name { get; set; } = string.Empty;
    public string OutputName { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;
    public bool EnableLeftHandIK { get; set; } = true;
    public bool EnableRightHandIK { get; set; } = true;
    public string LeftHandPoseFile { get; set; } = string.Empty;
    public string RightHandPoseFile { get; set; } = string.Empty;
    public string LeftIKTargetBoneName { get; set; } = string.Empty;
    public string RightIKTargetBoneName { get; set; } = string.Empty;
    public List<AnimationLayer> Layers { get; } = [];
}

internal static class Logging
{
    public static EngineLogger Logger { get; } = new();
}

internal sealed class EngineLogger
{
    public void Info(object? message) => Trace.WriteLine(message);
    public void Warn(object? message) => Trace.TraceWarning("{0}", message);
    public void Warn(object? message, Exception exception) => Trace.TraceWarning("{0}{1}{2}", message, Environment.NewLine, exception);
    public void Debug(object? message, Exception exception) => Trace.WriteLine($"{message}{Environment.NewLine}{exception}");
}

internal static class LocalizationManager
{
    private static readonly IReadOnlyDictionary<string, string> Messages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["MergeNeedsParent"] = "Choose a parent bone for this model part. A unique weapon attachment (tag_weapon) could not be resolved.",
        ["MergeUnknownParent"] = "Parent bone '{0}' was not found. Choose an existing bone in an earlier model part.",
        ["MergeAmbiguousAnimation"] = "Animation target '{0}' matches different bones. Choose unique bone names before exporting.",
        ["FbxMayaNotFound"] = "FBX export requires a local Autodesk Maya installation. mayapy.exe was not detected; install Maya 2025 or set ALCHEMY_STARS_MAYAPY to its path.",
        ["FbxConverterMissing"] = "The bundled FBX conversion script is missing. Reinstall Alchemy Stars.",
        ["FbxCastPluginMissing"] = "The bundled Maya CAST plug-in is missing. Reinstall Alchemy Stars.",
        ["FbxAsciiWorkspaceUnavailable"] = "No writable ASCII-only temporary folder is available for Maya FBX conversion.",
        ["FbxMayaStartFailed"] = "The local Maya FBX conversion process could not be started.",
        ["FbxMayaTimedOut"] = "Maya FBX conversion exceeded 10 minutes and was cancelled.",
        ["FbxMayaFailed"] = "Maya FBX conversion failed (exit code {0}).\n{1}",
    };

    public static string Get(string key) => Messages.TryGetValue(key, out var value) ? value : key;

    public static string Format(string key, params object?[] arguments) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), arguments);
}
