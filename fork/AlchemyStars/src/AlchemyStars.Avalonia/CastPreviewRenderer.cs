using System.Numerics;

namespace AlchemyStars.Avalonia;

internal readonly record struct PreviewCamera(float Yaw, float Pitch, float Zoom, bool AllGeometry = false)
{
    public static PreviewCamera Default => new(-0.9f, 0.3f, 1.2f);
}

// Deterministic shaded/z-buffered preview, kept off the UI thread and bounded in pixel size.
// No render-driver or reflection dependency is added to the Native AOT distribution.
internal static class CastPreviewRenderer
{
    internal const int Background = unchecked((int)0xFF10151C);
    public static int[] Render(CastPreviewScene scene, float frame, int width, int height, PreviewCamera camera, bool bones)
    {
        scene.Sample(frame);
        var pixels = new int[checked(width * height)];
        Array.Fill(pixels, Background);
        var depth = new float[pixels.Length];
        Array.Fill(depth, float.PositiveInfinity);
        var direction = new Vector3(MathF.Cos(camera.Yaw) * MathF.Cos(camera.Pitch), MathF.Sin(camera.Yaw) * MathF.Cos(camera.Pitch), MathF.Sin(camera.Pitch));
        var radius = camera.AllGeometry ? scene.AllRadius : scene.Radius;
        var center = camera.AllGeometry ? scene.AllCenter : scene.Center;
        var distance = radius * 3.2f / camera.Zoom;
        var eye = center + direction * distance;
        var forward = Vector3.Normalize(center - eye);
        var right = Vector3.Normalize(Vector3.Cross(forward, Vector3.UnitZ));
        var up = Vector3.Cross(right, forward);
        var focal = Math.Min(width, height) * 1.15f;
        Vector3 Project(Vector3 point)
        {
            var delta = point - eye;
            var z = Vector3.Dot(delta, forward);
            if (z < Math.Max(0.001f, scene.Radius * 0.001f)) return new(float.NaN);
            return new(width * 0.5f + Vector3.Dot(delta, right) * focal / z,
                height * 0.5f - Vector3.Dot(delta, up) * focal / z, z);
        }
        foreach (var surface in scene.Surfaces)
        {
            var world = scene.Skin(surface);
            var projected = world.Select(Project).ToArray();
            foreach (var (ia, ib, ic) in surface.Mesh.Faces)
            {
                var a = projected[ia]; var b = projected[ib]; var c = projected[ic];
                if (!CastPreviewScene.IsFinite(a) || !CastPreviewScene.IsFinite(b) || !CastPreviewScene.IsFinite(c)) continue;
                var normal = Vector3.Cross(world[ib] - world[ia], world[ic] - world[ia]);
                if (normal.LengthSquared() < 1e-12f) continue;
                normal = Vector3.Normalize(normal);
                // Two-sided clay shading keeps thin sleeves and small weapon parts visible.
                var light = Vector3.Normalize(-forward + up * 0.7f - right * 0.4f);
                var shade = 0.25f + 0.75f * MathF.Abs(Vector3.Dot(normal, light));
                var gray = (int)(shade * 205);
                var color = unchecked((int)0xFF000000) | (gray << 16) | ((gray + 5) << 8) | (gray + 9);
                Triangle(a, b, c, color, width, height, pixels, depth);
            }
        }
        if (bones || scene.VertexCount == 0)
            foreach (var skeleton in scene.Skeletons)
                foreach (var bone in skeleton.Bones)
                {
                    var p = Project(bone.WorldTranslation);
                    if (bone.Parent is { } parent) Line(Project(parent.WorldTranslation), p, width, height, pixels);
                }
        return pixels;
    }

    private static void Triangle(Vector3 a, Vector3 b, Vector3 c, int color, int width, int height, int[] pixels, float[] depth)
    {
        var area = Edge(a, b, c.X, c.Y);
        if (MathF.Abs(area) < 0.001f) return;
        var minX = (int)Math.Clamp(MathF.Min(a.X, MathF.Min(b.X, c.X)), 0, width - 1);
        var maxX = (int)Math.Clamp(MathF.Ceiling(MathF.Max(a.X, MathF.Max(b.X, c.X))), 0, width - 1);
        var minY = (int)Math.Clamp(MathF.Min(a.Y, MathF.Min(b.Y, c.Y)), 0, height - 1);
        var maxY = (int)Math.Clamp(MathF.Ceiling(MathF.Max(a.Y, MathF.Max(b.Y, c.Y))), 0, height - 1);
        for (var y = minY; y <= maxY; y++)
            for (var x = minX; x <= maxX; x++)
            {
                var wa = Edge(b, c, x + 0.5f, y + 0.5f) / area;
                var wb = Edge(c, a, x + 0.5f, y + 0.5f) / area;
                var wc = 1 - wa - wb;
                if (wa < 0 || wb < 0 || wc < 0) continue;
                var z = 1 / (wa / a.Z + wb / b.Z + wc / c.Z);
                var index = y * width + x;
                if (z >= depth[index]) continue;
                depth[index] = z;
                pixels[index] = color;
            }
    }

    private static float Edge(Vector3 a, Vector3 b, float x, float y) => (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X);

    private static void Line(Vector3 a, Vector3 b, int width, int height, int[] pixels)
    {
        if (!CastPreviewScene.IsFinite(a) || !CastPreviewScene.IsFinite(b)) return;
        // Clip to the viewport before stepping, so offscreen helper bones cannot stall rendering.
        var delta = b - a;
        var low = 0f; var high = 1f;
        bool Clip(float p, float q)
        {
            if (MathF.Abs(p) < 1e-7f) return q >= 0;
            var r = q / p;
            if (p < 0) low = Math.Max(low, r); else high = Math.Min(high, r);
            return low <= high;
        }
        if (!Clip(-delta.X, a.X) || !Clip(delta.X, width - 1 - a.X) || !Clip(-delta.Y, a.Y) || !Clip(delta.Y, height - 1 - a.Y)) return;
        b = a + delta * high; a += delta * low;
        var steps = Math.Max(1, (int)MathF.Ceiling(MathF.Max(MathF.Abs(b.X - a.X), MathF.Abs(b.Y - a.Y))));
        for (var i = 0; i <= steps; i++)
        {
            var point = Vector3.Lerp(a, b, (float)i / steps);
            var x = (int)point.X; var y = (int)point.Y;
            if ((uint)x < width && (uint)y < height) pixels[y * width + x] = unchecked((int)0xFF64D8EC);
        }
    }
}
