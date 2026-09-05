using System.Globalization;

namespace AlchemyStars.Avalonia;

internal static class SelfTest
{
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
                viewModel.ToggleLanguage();
                Require(!viewModel.IsChinese && viewModel.Text.ProductName == "Alchemy Stars", "Language toggle failed.");

                Require(viewModel.AddAnimationPaths([Path.Combine(testDirectory, "idle.cast")]) == 1, "Animation import routing failed.");
                Require(viewModel.SelectedAnimation?.OutputFolder == string.Empty, "New animation output folder must stay blank.");
                Require(viewModel.AddPartPaths([Path.Combine(testDirectory, "hands.cast"), Path.Combine(testDirectory, "weapon.cast")]) == 2, "Part import routing failed.");
                Require(viewModel.Parts[0].Type == ModelPartKind.ViewHands, "First imported model part should default to view hands.");
                Require(viewModel.Parts[1].Type == ModelPartKind.Weapon && viewModel.Parts[1].ParentBoneTag == "tag_weapon", "Weapon default parenting failed.");
                Require(viewModel.AddLayerPaths([Path.Combine(testDirectory, "sprint.cast")]) == 1, "Layer-priority import routing failed.");

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
