using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;

namespace AlchemyStars.Avalonia;

public sealed class CastPreviewViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(40) };
    private readonly Stopwatch clock = new();
    private CastPreviewScene? scene;
    private WriteableBitmap? bitmap;
    private PreviewCamera camera = PreviewCamera.Default;
    private UiText text;
    private int generation, renderRevision, width = 640, height = 360;
    private bool rendering, disposed, loading, playing, showBones;
    private double frame, startFrame;
    private string source = string.Empty, error = string.Empty;

    public CastPreviewViewModel(UiText text)
    {
        this.text = text;
        timer.Tick += (_, _) =>
        {
            if (scene is null || !playing) return;
            Frame = (startFrame + clock.Elapsed.TotalSeconds * scene.Framerate) % scene.FrameCount;
        };
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public UiText Text { get => text; set { text = value; Changed(); RefreshLabels(); } }
    public WriteableBitmap? Image => bitmap;
    public bool HasScene => scene is not null;
    public bool IsLoading { get => loading; private set { loading = value; Changed(); Changed(nameof(Status)); } }
    public bool IsPlaying => playing;
    public bool CanPlay => scene is { FrameCount: > 1 };
    public int LastFrame => Math.Max(0, (scene?.FrameCount ?? 1) - 1);
    public double Frame { get => frame; set { frame = Math.Clamp(value, 0, LastFrame); Changed(); Changed(nameof(FrameLabel)); RequestRender(); } }
    public string FrameLabel => $"{(int)Frame} / {LastFrame}";
    public string Source => source;
    public string Status => IsLoading ? Text.PreviewLoading : error.Length > 0 ? error : scene is null ? Text.PreviewEmpty : (scene.UsesProjectSkeleton ? Text.ProjectSkeletonPreview + " · " : "") + string.Format(Text.PreviewStats, scene.VertexCount, scene.BoneCount, scene.Framerate);
    public string PlayLabel => playing ? Text.PausePreview : Text.PlayPreview;
    public bool ShowBones { get => showBones; set { showBones = value; Changed(); RequestRender(); } }

    public async Task LoadAsync(string path, string? label = null, IReadOnlyList<ModelPartSpec>? parts = null, bool legacy = false)
    {
        Clear();
        var version = generation;
        IsLoading = true;
        try
        {
            var loaded = await Task.Run(() => CastPreviewScene.Load(path, parts, legacy));
            if (version != generation || disposed) return;
            scene = loaded;
            renderRevision++;
            source = label ?? path;
            camera = PreviewCamera.Default;
            showBones = loaded.VertexCount == 0;
            Changed(nameof(Source)); Changed(nameof(ShowBones)); RefreshLabels();
            await RenderAsync();
        }
        catch (Exception exception)
        {
            if (version != generation || disposed) return;
            error = Text.PreviewFailed + ": " + exception.Message;
            Changed(nameof(Status));
        }
        finally { if (version == generation && !disposed) IsLoading = false; }
    }

    public void Clear()
    {
        generation++;
        renderRevision++;
        Pause();
        scene = null; frame = 0; source = string.Empty; error = string.Empty; IsLoading = false;
        var previous = bitmap; bitmap = null; Changed(nameof(Image)); previous?.Dispose();
        Changed(nameof(Source)); RefreshLabels();
    }

    public void SetViewportSize(double w, double h)
    {
        if (w < 1 || h < 1) return;
        // Cap software-rendering work while preserving the viewport's aspect ratio.
        var scale = Math.Min(1, Math.Min(960 / w, 640 / h));
        var newWidth = Math.Max(32, (int)(w * scale));
        var newHeight = Math.Max(32, (int)(h * scale));
        if (newWidth == width && newHeight == height) return;
        width = newWidth; height = newHeight; RequestRender();
    }
    public void TogglePlayback()
    {
        if (playing) { Pause(); return; }
        if (!CanPlay) return;
        playing = true; startFrame = Frame; clock.Restart(); timer.Start(); RefreshLabels();
    }
    public void Pause() { playing = false; timer.Stop(); clock.Stop(); RefreshLabels(); }
    public void Step(int delta) { Pause(); Frame = Math.Clamp(Math.Floor(Frame) + delta, 0, LastFrame); }
    public void Fit() { camera = PreviewCamera.Default; RequestRender(); }
    public void FitAll() { camera = PreviewCamera.Default with { AllGeometry = true, Zoom = 1 }; RequestRender(); }
    public void Orbit(double x, double y)
    {
        camera = camera with { Yaw = camera.Yaw + (float)x * 0.01f, Pitch = Math.Clamp(camera.Pitch + (float)y * 0.01f, -1.45f, 1.45f) };
        RequestRender();
    }
    public void Zoom(double amount) { camera = camera with { Zoom = Math.Clamp(camera.Zoom * MathF.Pow(1.15f, (float)amount), 0.15f, 8) }; RequestRender(); }
    private void RequestRender() { renderRevision++; _ = RenderAsync(); }

    // At most one scene is sampled/rasterized at a time; requests coalesce to the newest frame.
    private async Task RenderAsync()
    {
        if (rendering || disposed || scene is null) return;
        rendering = true;
        try
        {
            int revision;
            do
            {
                revision = renderRevision;
                var version = generation;
                var current = scene;
                if (current is null || disposed) break;
                var w = width; var h = height; var f = (float)frame; var view = camera; var bones = ShowBones;
                var pixels = await Task.Run(() => CastPreviewRenderer.Render(current, f, w, h, view, bones));
                if (disposed || version != generation) continue;
                var rendered = new WriteableBitmap(new PixelSize(w, h), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Opaque);
                using (var buffer = rendered.Lock())
                    for (var y = 0; y < h; y++) Marshal.Copy(pixels, y * w, buffer.Address + y * buffer.RowBytes, w);
                var previous = bitmap; bitmap = rendered; Changed(nameof(Image)); previous?.Dispose();
            } while (revision != renderRevision && !disposed);
        }
        catch (Exception exception) { Pause(); error = Text.PreviewFailed + ": " + exception.Message; Changed(nameof(Status)); }
        finally { rendering = false; }
    }
    private void RefreshLabels()
    {
        foreach (var name in new[] { nameof(HasScene), nameof(CanPlay), nameof(LastFrame), nameof(Frame), nameof(FrameLabel), nameof(Status), nameof(IsPlaying), nameof(PlayLabel) }) Changed(name);
    }
    private void Changed([CallerMemberName] string? property = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
    public void Dispose() { disposed = true; Clear(); timer.Stop(); }
}
