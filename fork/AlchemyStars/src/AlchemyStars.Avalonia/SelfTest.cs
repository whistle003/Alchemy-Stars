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
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("zh-CN");
                var viewModel = new MainWindowViewModel(engine);
                Require(viewModel.IsChinese, "Chinese system language was not detected.");
                Require(viewModel.ProductName == "炼金之星", "Chinese product name mismatch.");
                viewModel.ToggleLanguageCommand.Execute(null);
                Require(!viewModel.IsChinese && viewModel.ProductName == "Alchemy Stars", "Language toggle failed.");
                viewModel.RunContractCheckCommand.Execute(null);
                Require(viewModel.VerificationStatus.Contains("passed", StringComparison.OrdinalIgnoreCase), "Contract check failed.");
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousCulture;
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

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
