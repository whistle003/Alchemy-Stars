using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using SkiaSharp;

namespace AlchemyStars.Avalonia;

public sealed class GpuCastPreviewControl : Control
{
    public static readonly StyledProperty<CastPreviewFrame?> FrameProperty =
        AvaloniaProperty.Register<GpuCastPreviewControl, CastPreviewFrame?>(nameof(Frame));

    static GpuCastPreviewControl() => AffectsRender<GpuCastPreviewControl>(FrameProperty);

    public CastPreviewFrame? Frame
    {
        get => GetValue(FrameProperty);
        set => SetValue(FrameProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (Frame is { TriangleCount: > 0 } frame && Bounds.Width > 0 && Bounds.Height > 0)
            context.Custom(new SkiaFrameDrawOperation(new Rect(Bounds.Size), frame));
    }

    private sealed class SkiaFrameDrawOperation(Rect bounds, CastPreviewFrame frame) : ICustomDrawOperation
    {
        public Rect Bounds { get; } = bounds;

        public void Render(ImmediateDrawingContext context)
        {
            var skia = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (skia is null) return;

            using var lease = skia.Lease();
            var canvas = lease.SkCanvas;
            canvas.Save();
            canvas.Scale((float)(Bounds.Width / frame.Width), (float)(Bounds.Height / frame.Height));

            var points = new SKPoint[frame.TrianglePoints.Length];
            var colors = new SKColor[frame.TriangleColors.Length];
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new SKPoint(frame.TrianglePoints[i].X, frame.TrianglePoints[i].Y);
                var packed = frame.TriangleColors[i];
                colors[i] = new SKColor(
                    (byte)((packed >> 16) & 0xFF),
                    (byte)((packed >> 8) & 0xFF),
                    (byte)(packed & 0xFF),
                    (byte)(packed >> 24));
            }

            using var vertices = SKVertices.CreateCopy(SKVertexMode.Triangles, points, colors);
            // DrawVertices modulates vertex colors by the paint color. White keeps
            // the prepared clay-lighting colors intact; Skia's default black would
            // turn the entire model into a silhouette.
            using var surfacePaint = new SKPaint { IsAntialias = true, Color = SKColors.White };
            canvas.DrawVertices(vertices, SKBlendMode.Modulate, surfacePaint);

            if (frame.BoneLines.Length > 0)
            {
                using var bonePaint = new SKPaint
                {
                    IsAntialias = true,
                    Color = new SKColor(0xFF64D8EC),
                    StrokeWidth = 1.35f,
                    Style = SKPaintStyle.Stroke,
                };
                for (var i = 0; i + 1 < frame.BoneLines.Length; i += 2)
                {
                    var a = frame.BoneLines[i];
                    var b = frame.BoneLines[i + 1];
                    canvas.DrawLine(a.X, a.Y, b.X, b.Y, bonePaint);
                }
            }

            canvas.Restore();
        }

        public bool HitTest(Point point) => Bounds.Contains(point);
        public bool Equals(ICustomDrawOperation? other) => false;
        public void Dispose() { }
    }
}
