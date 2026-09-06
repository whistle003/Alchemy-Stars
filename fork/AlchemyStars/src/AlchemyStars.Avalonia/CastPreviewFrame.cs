using System.Numerics;

namespace AlchemyStars.Avalonia;

// Immutable, renderer-neutral frame data. The scene sampler produces this once;
// the interactive Skia adapter consumes it without owning or mutating the CAST scene.
public sealed class CastPreviewFrame
{
    internal CastPreviewFrame(int width, int height, Vector2[] trianglePoints, uint[] triangleColors, Vector2[] boneLines)
    {
        Width = width;
        Height = height;
        TrianglePoints = trianglePoints;
        TriangleColors = triangleColors;
        BoneLines = boneLines;
    }

    public int Width { get; }
    public int Height { get; }
    public int TriangleCount => TrianglePoints.Length / 3;
    public int BoneLineCount => BoneLines.Length / 2;
    internal Vector2[] TrianglePoints { get; }
    internal uint[] TriangleColors { get; }
    internal Vector2[] BoneLines { get; }
}
