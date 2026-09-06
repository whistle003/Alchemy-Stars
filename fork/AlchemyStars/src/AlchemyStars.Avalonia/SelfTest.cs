using System.Globalization;
using System.Numerics;

namespace AlchemyStars.Avalonia;

internal static class SelfTest
{
    public static int RunPreview(string path, string? projectPath = null)
    {
        try
        {
            var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path));
            var store = new WorkspaceProjectStore();
            var request = projectPath is null ? null : store.CreateExportRequest(store.Load(projectPath));
            var scene = CastPreviewScene.Load(path, request?.Parts, request?.Options.MatchOldCallOfDuty ?? false);
            Require(scene.FrameCount > 1, "Expected an animated CAST for the preview regression.");
            var watch = System.Diagnostics.Stopwatch.StartNew();
            var first = CastPreviewRenderer.Render(scene, 0, 640, 400, PreviewCamera.Default, false);
            var another = CastPreviewRenderer.Render(scene, scene.FrameCount / 2, 640, 400, PreviewCamera.Default, false);
            var repeat = CastPreviewRenderer.Render(scene, 0, 640, 400, PreviewCamera.Default, false);
            var firstPerson = CastPreviewRenderer.Render(scene, 0, 640, 400, PreviewCamera.FirstPerson, false);
            var gpuFrame = CastPreviewRenderer.Prepare(scene, 0, 640, 400, PreviewCamera.FirstPerson, false);
            Require(gpuFrame.TriangleCount > 0 && gpuFrame.Width == 640 && gpuFrame.Height == 400,
                "GPU preview frame was not prepared.");
            Require(gpuFrame.TrianglePoints.Length == gpuFrame.TriangleColors.Length,
                "GPU preview vertex colors are incomplete.");
            Require(first.Count(pixel => pixel != CastPreviewRenderer.Background) > 50, "CAST geometry was not rasterized.");
            Require(!first.SequenceEqual(another), "Animation sampling did not change the preview.");
            Require(first.SequenceEqual(repeat), "Reverse frame scrubbing accumulated transforms.");
            Require(firstPerson.Count(pixel => pixel != CastPreviewRenderer.Background) > 50, "First-person CAST geometry was not rasterized.");
            var firstPersonGeometry = firstPerson.Select((pixel, index) => (pixel, index))
                .Where(item => item.pixel != CastPreviewRenderer.Background).Select(item => item.index).ToArray();
            var firstPersonBounds = new
            {
                Left = firstPersonGeometry.Min(index => index % 640),
                Top = firstPersonGeometry.Min(index => index / 640),
                Right = firstPersonGeometry.Max(index => index % 640),
                Bottom = firstPersonGeometry.Max(index => index / 640),
            };
            Require(firstPersonBounds.Left > 1 && firstPersonBounds.Top > 1 && firstPersonBounds.Right < 638 && firstPersonBounds.Bottom < 398,
                $"First-person weapon is clipped by the viewport: {firstPersonBounds}.");
            Require(first.Where(pixel => pixel != CastPreviewRenderer.Background).Distinct().Count() > 16,
                "Smooth clay shading did not produce enough tonal detail.");
            Require(CastPreviewRenderer.AntiAliasingSamples == 2, "CAST preview anti-aliasing is disabled.");
            Require(before.SequenceEqual(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path))), "Preview modified the CAST.");
            Console.WriteLine($"CAST preview: PASS ({scene.VertexCount} vertices, {scene.BoneCount} bones, {scene.FrameCount} frames; orbit and 90-degree first-person renders {watch.ElapsedMilliseconds} ms)");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }

    public static int Run()
    {
        try
        {
            var engine = new AnimationExportEngine();
            var capabilities = engine.Capabilities;
            Require(capabilities.Version == AnimationExportEngine.EngineVersion, "Engine version mismatch.");
            Require(Enum.GetValues<ExportFormat>().All(capabilities.OutputFormats.Contains), "An output format is missing.");
            Require(capabilities.SupportsAnimationOnlyCast, "Animation-only CAST is not advertised.");
            Require(capabilities.SupportsSelectiveBoneBake, "Selective bone baking is not advertised.");
            Require(capabilities.SupportsNativeAot, "Native AOT is not advertised.");
            var firstPersonView = CastPreviewRenderer.ResolveView(new CastPreviewScene(), 640, 400, PreviewCamera.FirstPerson);
            Require(Vector3.Distance(firstPersonView.Eye, Vector3.Zero) < 1e-5f, "First-person camera must use Maya's default origin.");
            Require(Vector3.Distance(firstPersonView.Forward, Vector3.UnitX) < 1e-5f, "Maya camera X/Z rotation did not produce forward +X.");
            Require(Vector3.Distance(firstPersonView.Right, -Vector3.UnitY) < 1e-5f && Vector3.Distance(firstPersonView.Up, Vector3.UnitZ) < 1e-5f,
                "Maya camera X/Z rotation produced an invalid view basis.");
            Require(MathF.Abs(firstPersonView.FocalLength - 320) < 1e-4f, "First-person horizontal FOV is not 90 degrees.");
            Require(MathF.Abs(firstPersonView.NearClip - 0.1f) < 1e-5f, "First-person camera does not use Maya's default near clip.");
            try
            {
                engine.Export(new AnimationExportRequest(
                    [],
                    [],
                    new AnimationExportOptions(
                        new IkChainSpec("a", "b", "c", "d"),
                        new IkChainSpec("a", "b", "c", "d"))));
                throw new InvalidOperationException("Empty export request was accepted.");
            }
            catch (ExportValidationException exception)
            {
                Require(exception.Code == ExportErrorCode.NoModelParts, "Structured export error code mismatch.");
            }

            var previousCulture = CultureInfo.CurrentUICulture;
            var testDirectory = Path.Combine(Path.GetTempPath(), $"AlchemyStars-AotSelfTest-{Guid.NewGuid():N}");
            Directory.CreateDirectory(testDirectory);
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                var preferences = new ApplicationPreferencesStore(Path.Combine(testDirectory, "settings.json"));
                var projectStore = new WorkspaceProjectStore();
                var viewModel = new MainWindowViewModel(engine, projectStore, preferences, new SelfTestFilePicker());
                Require(viewModel.IsChinese, "Chinese system language was not detected.");
                Require(viewModel.Text.ProductName == "炼金之星", "Chinese product name mismatch.");
                Require(viewModel.Text.ImportAnimationsMenu == "导入动画…"
                    && viewModel.Text.ImportLayersMenu == "导入动画层…"
                    && viewModel.Text.ImportPartsMenu == "导入模型部件…"
                    && viewModel.Text.FitSubjectMenu == "适应主体 (F)", "Chinese context menus are not localized.");
                viewModel.ToggleLanguage();
                Require(!viewModel.IsChinese && viewModel.Text.ProductName == "Alchemy Stars", "Language toggle failed.");
                Require(viewModel.Text.ImportAnimationsMenu == "Import animations…"
                    && viewModel.Text.ImportLayersMenu == "Import animation layers…"
                    && viewModel.Text.ImportPartsMenu == "Import model parts…"
                    && viewModel.Text.FitSubjectMenu == "Fit subject (F)", "English context menus are not localized.");
                viewModel.Preview.ToggleFirstPerson();
                Require(viewModel.Preview.IsFirstPerson
                    && viewModel.Preview.CameraModeLabel == "Return to orbit view / 1"
                    && viewModel.Preview.FirstPersonBadge == "FIRST PERSON · 90° FOV",
                    "First-person preview state or English accessibility text is invalid.");
                viewModel.Preview.ToggleFirstPerson();
                Require(!viewModel.Preview.IsFirstPerson, "First-person preview did not return to orbit mode.");

                Require(viewModel.AddAnimationPaths([Path.Combine(testDirectory, "idle.cast")]) == 1, "Animation import routing failed.");
                Require(viewModel.SelectedAnimation?.OutputFolder == string.Empty, "New animation output folder must stay blank.");
                Require(viewModel.AddPartPaths([Path.Combine(testDirectory, "hands.cast"), Path.Combine(testDirectory, "weapon.cast")]) == 2, "Part import routing failed.");
                Require(viewModel.Parts[0].Type == ModelPartKind.ViewHands, "First imported model part should default to view hands.");
                Require(viewModel.Parts[1].Type == ModelPartKind.Weapon && viewModel.Parts[1].ParentBoneTag == "tag_weapon", "Weapon default parenting failed.");
                Require(viewModel.AddLayerPaths([Path.Combine(testDirectory, "sprint.cast")]) == 1, "Layer-priority import routing failed.");

                var placements = AnimationTimelineLayout.Calculate([
                    new AnimationTimelineSpan(0, 60),
                    new AnimationTimelineSpan(10, 20),
                    new AnimationTimelineSpan(-5, 10),
                ]);
                Require(placements[0].DurationFrames == 60 && placements[1].DurationFrames == 20,
                    "Timeline bars do not preserve differing animation durations.");
                Require(placements[1].LeadingFrames == 15 && placements[2].LeadingFrames == 0,
                    "Timeline bars do not reflect positive and negative layer offsets.");

                var projectPath = Path.Combine(testDirectory, "roundtrip.aprj");
                projectStore.Save(viewModel.Workspace, projectPath);
                var roundtrip = projectStore.Load(projectPath);
                Require(roundtrip.Animations.Count == 1 && roundtrip.Animations[0].Layers.Count == 1, "AOT project round-trip lost animation layers.");
                Require(roundtrip.Parts.Count == 2 && roundtrip.Parts[1].ParentBoneTag == "tag_weapon", "AOT project round-trip lost model hierarchy settings.");

                var legacyProjectPath = Path.Combine(testDirectory, "legacy-reference.aprj");
                File.WriteAllText(legacyProjectPath, """
                    {"$id":"1","OutputFormat":".cast","Animations":{"$id":"2","$values":[]},"Parts":{"$id":"3","$values":[]}}
                    """);
                var legacy = projectStore.Load(legacyProjectPath);
                Require(legacy.Animations.Count == 0 && legacy.Parts.Count == 0, "Legacy reference-preserved project compatibility failed.");
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
                if (Directory.Exists(testDirectory))
                    Directory.Delete(testDirectory, true);
            }

            Console.WriteLine("Alchemy Stars Avalonia AOT self-test passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    public static int RunHawk(IReadOnlyList<string> arguments)
    {
        try
        {
            Require(arguments.Count == 6, "Hawk smoke requires: hands, weapon, idle, sprint loop, additive offset, output folder.");
            var outputFolder = Path.GetFullPath(arguments[5]);
            var request = new AnimationExportRequest(
                [
                    new ModelPartSpec(Path.GetFullPath(arguments[0]), ModelPartKind.ViewHands),
                    new ModelPartSpec(Path.GetFullPath(arguments[1]), ModelPartKind.Weapon, "tag_weapon"),
                ],
                [new AnimationExportJob(
                    Path.GetFullPath(arguments[2]),
                    "sat_vm_ar_hawk_sprint_engine_aot",
                    outputFolder,
                    Framerate: 30,
                    EnableLeftHandIk: true,
                    EnableRightHandIk: false,
                    Layers:
                    [
                        new AnimationLayerSpec(Path.GetFullPath(arguments[3]), AnimationLayerKind.Additive),
                        new AnimationLayerSpec(Path.GetFullPath(arguments[4]), AnimationLayerKind.Additive),
                    ])],
                new AnimationExportOptions(
                    new IkChainSpec("j_shoulder_le", "j_elbow_le", "j_wrist_le", "tag_ik_loc_le"),
                    new IkChainSpec("j_shoulder_ri", "j_elbow_ri", "j_wrist_ri", "tag_ik_loc_ri")));

            var result = new AnimationExportEngine().Export(request);
            Require(result.OutputFiles.Count == 1, "Hawk smoke did not produce exactly one output.");
            var output = Path.GetFullPath(result.OutputFiles[0]);
            Require(File.Exists(output) && new FileInfo(output).Length > 0, "Hawk smoke output is missing or empty.");
            Console.WriteLine(output);
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    public static int RunProject(IReadOnlyList<string> arguments)
    {
        try
        {
            Require(arguments.Count == 2, "Project smoke requires: project file and output folder.");
            var store = new WorkspaceProjectStore();
            var document = store.Load(Path.GetFullPath(arguments[0]));
            var outputFolder = Path.GetFullPath(arguments[1]);
            Directory.CreateDirectory(outputFolder);
            foreach (var animation in document.Animations)
                animation.OutputFolder = outputFolder;
            var result = new AnimationExportEngine().Export(store.CreateExportRequest(document));
            Require(result.OutputFiles.Count == document.Animations.Count, "Project smoke output count mismatch.");
            Require(result.OutputFiles.All(path => File.Exists(path) && new FileInfo(path).Length > 0), "Project smoke output is missing or empty.");
            foreach (var output in result.OutputFiles)
                Console.WriteLine(Path.GetFullPath(output));
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class SelfTestFilePicker : IWorkspaceFilePicker
    {
        public Task<IReadOnlyList<string>> PickFilesAsync(FilePickerPurpose purpose, bool allowMultiple) =>
            Task.FromResult<IReadOnlyList<string>>([]);
        public Task<string?> PickProjectDestinationAsync(string? currentPath) => Task.FromResult<string?>(null);
        public Task<string?> PickFolderAsync(string? currentPath) => Task.FromResult<string?>(null);
        public Task OpenUriAsync(Uri uri) => Task.CompletedTask;
    }
}
