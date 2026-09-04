using Alchemist.InverseKinematics;
using Alchemist.UI;
using Cast.NET;
using Cast.NET.Nodes;
using RedFox.Graphics3D.Skeletal;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json;
using System.Numerics;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;

if (args.Contains("--drop-routing-only", StringComparer.OrdinalIgnoreCase))
{
    TestLayerDropTargetPriority();
    Console.WriteLine("Animation-layer drop routing passed.");
    return 0;
}

var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output"));
var exampleDirectory = args.Length > 1
    ? Path.GetFullPath(args[1])
    : Path.GetDirectoryName(FindFile("Example", "manifest.json"))!;
var exampleManifest = LoadExampleManifest(exampleDirectory);
var standardExamples = exampleManifest.StandardExamples.ToDictionary(example => example.Id, StringComparer.OrdinalIgnoreCase);
var improvedExamples = exampleManifest.ImprovedExamples.ToDictionary(example => example.Id, StringComparer.OrdinalIgnoreCase);

var checks = new List<object>();
var failures = new List<string>();
var sprintProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-sprint"].Path);
var weaponFirstSprintProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-sprint"].Path);
var weaponFirstParts = weaponFirstSprintProject.Parts
    .OrderBy(part => part.Type == PartType.ViewHands ? 1 : 0)
    .ToArray();
weaponFirstSprintProject.Parts.Clear();
foreach (var part in weaponFirstParts)
{
    if (part.Type == PartType.Weapon)
        part.ParentBoneTag = string.Empty;
    weaponFirstSprintProject.Parts.Add(part);
}
var idleProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-idle"].Path);
var smdProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-sprint"].Path);
var fbxProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-sprint"].Path);
var batchProject = LoadExampleProject(exampleDirectory, improvedExamples["hawk-batch"].Path);
var mp5BaseProject = LoadExampleProject(exampleDirectory, standardExamples["mp5-base"].Path);
var mp5GripProject = LoadExampleProject(exampleDirectory, standardExamples["mp5-grip"].Path);
var sprintTemplate = sprintProject.Animations.Single();
var idleTemplate = idleProject.Animations.Single();
string? sprintOutput = null;
string? weaponFirstSprintOutput = null;
string? idleOutput = null;
string? smdOutput = null;
string? fbxOutput = null;

Run("Animation.Clone preserves all per-animation settings", () => TestAnimationClone(sprintTemplate));
Run("Export format choices include CAST, SEAnim, FBX, and SMD", () =>
{
    var formats = new MainViewModel(_ => { }, string.Empty).OutputFormats;
    Assert(formats.SequenceEqual([".cast", ".fbx", ".smd", ".seanim"]),
        "Export formats must be ordered as CAST, FBX, SMD, and SEAnim.");
});
Run("System language detection and explicit language choices resolve predictably", () =>
{
    Assert(LocalizationManager.ResolveCulture("system", CultureInfo.GetCultureInfo("zh-TW")) == "zh-CN",
        "A Chinese system culture should select the Chinese interface.");
    Assert(LocalizationManager.ResolveCulture("system", CultureInfo.GetCultureInfo("fr-FR")) == "en-US",
        "A non-Chinese system culture should select the English interface.");
    Assert(LocalizationManager.ResolveCulture("en-US", CultureInfo.GetCultureInfo("zh-CN")) == "en-US",
        "An explicit English choice should override the system culture.");
});
Run("Context animation import adds every selected animation", TestContextAnimationImport);
Run("Context layer import targets only the requested animation", TestContextLayerImport);
Run("External drop over animation layers prioritizes the hovered animation", TestLayerDropTargetPriority);
Run("Context model import adds every selected model part", TestContextPartImport);
Run("Sprint example restores layer and part ownership", () => TestExampleProject(sprintProject, expectedLayerCount: 2));
Run("Idle example restores part ownership", () => TestExampleProject(idleProject, expectedLayerCount: 0));
Run("Batch example matches the standalone projects", () => TestBatchExample(batchProject, sprintProject, idleProject));
Run("Standard MP5 examples load in the current project format", () =>
{
    TestStandardExample(mp5BaseProject, standardExamples["mp5-base"]);
    TestStandardExample(mp5GripProject, standardExamples["mp5-grip"]);
});
Run("Standard MP5 examples remain byte-identical to the source", () => TestStandardExampleHashes(exampleDirectory, exampleManifest.StandardExamples));
Run("Hawk sprint follows the upstream idle-plus-two-additive-layers pattern", () => TestHawkSprintPattern(sprintProject, mp5BaseProject));
Run("Example manifest is complete", () => TestExampleManifest(exampleDirectory, exampleManifest));

var requiredFiles = sprintProject.Parts.Select(part => part.FilePath)
    .Concat(sprintProject.Animations.Select(animation => animation.Name))
    .Concat(sprintProject.Animations.SelectMany(animation => animation.Layers.Select(layer => layer.Name)))
    .Concat(idleProject.Animations.Select(animation => animation.Name))
    .Distinct(StringComparer.OrdinalIgnoreCase)
    .ToArray();
if (requiredFiles.All(File.Exists))
{
    Directory.CreateDirectory(outputDirectory);
    var parts = sprintProject.Parts.ToList();
    var skeleton = AnimationConverter.LoadSkeletonFromParts(parts, matchOldCallOfDuty: false);

    Run("View hands and weapon share one merged skeleton", () => TestMergedSkeleton(skeleton));
    Run("Unsafe right-hand IK target is rejected", () => TestRightHandIkCycle(skeleton));

    Run("Sprint and additive offset bake into one Maya CAST", () =>
    {
        sprintProject.Animations.Single().OutputFolder = outputDirectory;
        var outputs = sprintProject.ExportAnimations();
        Assert(outputs.Count == 1, "Sprint example should export exactly one file.");
        sprintOutput = Path.GetFullPath(outputs.Single());
        ValidateMayaPackage(sprintOutput, sprintProject.Parts);
    });

    Run("Weapon-first part order exports one CAST for Maya validation", () =>
    {
        var weaponFirstOutputDirectory = Path.Combine(outputDirectory, "weapon-first");
        weaponFirstSprintProject.Animations.Single().OutputFolder = weaponFirstOutputDirectory;
        var outputs = weaponFirstSprintProject.ExportAnimations();
        Assert(outputs.Count == 1, "Weapon-first sprint example should export exactly one file.");
        weaponFirstSprintOutput = Path.GetFullPath(outputs.Single());
        ValidateMayaPackage(weaponFirstSprintOutput, weaponFirstSprintProject.Parts);
    });

    Run("Idle bakes into a separate one-animation Maya CAST", () =>
    {
        idleProject.Animations.Single().OutputFolder = outputDirectory;
        var outputs = idleProject.ExportAnimations();
        Assert(outputs.Count == 1, "Idle example should export exactly one file.");
        idleOutput = Path.GetFullPath(outputs.Single());
        ValidateMayaPackage(idleOutput, idleProject.Parts);
    });

    Run("Hawk sprint exports a complete SMD animation", () =>
    {
        smdProject.OutputFormat = ".smd";
        smdProject.Animations.Single().OutputFolder = Path.Combine(outputDirectory, "smd");
        var outputs = smdProject.ExportAnimations();
        Assert(outputs.Count == 1, "SMD export should create exactly one file.");
        smdOutput = Path.GetFullPath(outputs.Single());
        ValidateSmdAnimation(smdOutput, expectedBoneCount: skeleton.Bones.Count, expectedFrameCount: 67);
    });

    if (MayaFbxExporter.FindMayapy() is not null)
    {
        Run("Hawk sprint exports an FBX through the local Maya", () =>
        {
            fbxProject.OutputFormat = ".fbx";
            fbxProject.Animations.Single().OutputFolder = Path.Combine(outputDirectory, "fbx-中文路径");
            var outputs = fbxProject.ExportAnimations();
            Assert(outputs.Count == 1, "FBX export should create exactly one file.");
            fbxOutput = Path.GetFullPath(outputs.Single());
            Assert(File.Exists(fbxOutput) && new FileInfo(fbxOutput).Length > 0, "FBX output is missing or empty.");
        });
    }

    Run("Batch example exports two valid one-animation Maya CAST files", () =>
    {
        var batchOutputDirectory = Path.Combine(outputDirectory, "batch-example");
        foreach (var animation in batchProject.Animations)
            animation.OutputFolder = batchOutputDirectory;

        var outputs = batchProject.ExportAnimations();
        Assert(outputs.Count == 2, "Batch example should export exactly two files.");
        Assert(outputs.Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "Batch example output paths must be unique.");
        foreach (var output in outputs)
            ValidateMayaPackage(output, batchProject.Parts);
    });
}
else
{
    failures.Add("Required user CAST fixtures are missing: " + string.Join(", ", requiredFiles.Where(x => !File.Exists(x))));
}

var report = new
{
    generatedAtUtc = DateTimeOffset.UtcNow,
    outputDirectory,
    artifacts = new
    {
        sprintCast = sprintOutput,
        weaponFirstSprintCast = weaponFirstSprintOutput,
        idleCast = idleOutput,
        sprintSmd = smdOutput,
        sprintFbx = fbxOutput,
    },
    passed = failures.Count == 0,
    checks,
    failures,
};
var reportJson = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
Directory.CreateDirectory(outputDirectory);
File.WriteAllText(Path.Combine(outputDirectory, "acceptance-report.json"), reportJson);
Console.WriteLine(reportJson);
return failures.Count == 0 ? 0 : 1;

void Run(string name, Action test)
{
    try
    {
        test();
        checks.Add(new { name, passed = true });
    }
    catch (Exception ex)
    {
        checks.Add(new { name, passed = false, error = ex.Message });
        failures.Add($"{name}: {ex}");
    }
}

static void TestAnimationClone(Animation template)
{
    var source = template.Clone();
    source.EnableLeftHandIK = false;
    source.EnableRightHandIK = true;
    source.LeftIKTargetBoneName = "left_override";
    source.RightIKTargetBoneName = "right_override";
    source.Layers[0].Offset = 7;
    source.Layers[1].Offset = -2;

    var clone = source.Clone();
    Assert(!clone.EnableLeftHandIK && clone.EnableRightHandIK, "IK flags were not cloned independently.");
    Assert(clone.LeftIKTargetBoneName == "left_override" && clone.RightIKTargetBoneName == "right_override", "IK overrides were lost.");
    Assert(clone.Layers.Count == 2 && clone.Layers[0].Offset == 7 && clone.Layers[1].Offset == -2, "Layer count, order, or offsets were lost.");
    Assert(clone.Layers.All(layer => ReferenceEquals(layer.Owner, clone)), "Cloned layer ownership was not restored.");
}

static void TestContextLayerImport()
{
    var target = new Animation("base.cast");
    var other = new Animation("other.cast");
    var added = MainViewModel.AddLayerFiles([target], ["offset.cast", "gesture.cast"]);

    Assert(added == 2, "Both selected layer files should be imported into the target animation.");
    Assert(target.Layers.Select(layer => layer.Name).SequenceEqual(["offset.cast", "gesture.cast"]), "Imported layers are missing or out of order.");
    Assert(target.Layers.All(layer => ReferenceEquals(layer.Owner, target)), "Imported layer ownership was not assigned to the target animation.");
    Assert(other.Layers.Count == 0, "A non-target animation was modified by context import.");
}

static void TestContextAnimationImport()
{
    var viewModel = new MainViewModel(_ => { }, string.Empty);
    var added = viewModel.AddAnimationFiles(["sprint.cast", "idle.cast"]);

    Assert(added == 2, "Both selected animation files should be imported.");
    Assert(viewModel.Animations.Select(animation => animation.Name).SequenceEqual(["sprint.cast", "idle.cast"]), "Imported animations are missing or out of order.");
}

static void TestContextPartImport()
{
    var viewModel = new MainViewModel(_ => { }, string.Empty);
    var added = viewModel.AddPartFiles(["hands.cast", "weapon.cast"]);

    Assert(added == 2, "Both selected model files should be imported.");
    Assert(viewModel.Parts.Select(part => part.FilePath).SequenceEqual(["hands.cast", "weapon.cast"]), "Imported model parts are missing or out of order.");
    Assert(viewModel.Parts.All(part => ReferenceEquals(part.Owner, viewModel)), "Imported model part ownership was not assigned to the current project.");
}

static void TestLayerDropTargetPriority()
{
    Exception? failure = null;
    var thread = new Thread(() =>
    {
        var previousViewModel = MainWindow.ViewModel;
        try
        {
            var viewModel = new MainViewModel(_ => { }, string.Empty);
            var target = new Animation("base.cast");
            var selectedElsewhere = new Animation("other.cast");
            viewModel.Animations.Add(target);
            viewModel.Animations.Add(selectedElsewhere);
            MainWindow.ViewModel = viewModel;

            var outerAnimationList = new ListView { ItemsSource = viewModel.Animations };
            outerAnimationList.SelectedItem = selectedElsewhere;
            var hoveredLayerList = new ListBox { DataContext = target };
            AutomationProperties.SetAutomationId(hoveredLayerList, "LayerList");
            var data = new DataObject();
            data.SetData(DataFormats.FileDrop, new[] { "offset.cast", "gesture.cast" });

            var dragEventConstructor = typeof(DragEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                [typeof(IDataObject), typeof(DragDropKeyStates), typeof(DragDropEffects), typeof(DependencyObject), typeof(Point)],
                modifiers: null)
                ?? throw new InvalidOperationException("DragEventArgs constructor was not found.");
            var dragEvent = (DragEventArgs)dragEventConstructor.Invoke(
                [data, DragDropKeyStates.None, DragDropEffects.Copy, hoveredLayerList, new Point(4, 4)]);
            dragEvent.RoutedEvent = DragDrop.DropEvent;
            dragEvent.Source = hoveredLayerList;

            var window = (MainWindow)RuntimeHelpers.GetUninitializedObject(typeof(MainWindow));
            var dropHandler = typeof(MainWindow).GetMethod("ListViewDrop", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Animation drop handler was not found.");
            dropHandler.Invoke(window, [outerAnimationList, dragEvent]);

            Assert(viewModel.Animations.Count == 2, "Files dropped over the animation-layer area were incorrectly added as main animations.");
            Assert(target.Layers.Select(layer => layer.Name).SequenceEqual(["offset.cast", "gesture.cast"]), "Files dropped over the animation-layer area were not imported into the hovered animation.");
            Assert(selectedElsewhere.Layers.Count == 0, "The outer animation selection incorrectly overrode the hovered animation-layer target.");
            Assert(dragEvent.Handled, "The prioritized animation-layer drop was not marked handled.");
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            failure = ex.InnerException;
        }
        catch (Exception ex)
        {
            failure = ex;
        }
        finally
        {
            MainWindow.ViewModel = previousViewModel;
        }
    });
    thread.SetApartmentState(ApartmentState.STA);
    thread.Start();
    thread.Join();

    if (failure is not null)
        throw new InvalidOperationException("Animation-layer drop routing failed.", failure);
}

static void TestMergedSkeleton(Skeleton skeleton)
{
    var duplicateNames = skeleton.Bones
        .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
        .Where(x => x.Count() > 1)
        .Select(x => x.Key)
        .ToArray();
    Assert(duplicateNames.Length == 0, "Merged skeleton contains duplicate bone names: " + string.Join(", ", duplicateNames));
    Assert(skeleton.Bones.Count(x => string.Equals(x.Name, "j_gun", StringComparison.OrdinalIgnoreCase)) == 1, "Expected exactly one shared j_gun bone.");
    Assert(skeleton.Bones.Count > 100, "Merged skeleton is unexpectedly small.");
}

static void TestRightHandIkCycle(Skeleton skeleton)
{
    var target = skeleton.FindBone("tag_ik_loc_ri") ?? throw new InvalidDataException("Missing tag_ik_loc_ri.");
    var start = skeleton.FindBone("j_shoulder_ri") ?? throw new InvalidDataException("Missing j_shoulder_ri.");
    Assert(target.IsDescendantOf(start), "Fixture no longer contains the expected unsafe right-hand IK hierarchy.");

    var player = new RedFox.Graphics3D.AnimationPlayer("IK safety acceptance");
    var solver = AnimationConverter.CreateIKSolver(
        "right",
        new IKSettings("j_shoulder_ri", "j_elbow_ri", "j_wrist_ri", "tag_ik_loc_ri"),
        skeleton,
        player);
    Assert(solver is null && player.Solvers.Count == 0, "Unsafe IK solver was added to the animation player.");
}

static void TestExampleProject(MainViewModel viewModel, int expectedLayerCount)
{
    Assert(viewModel.Animations.Count == 1 && viewModel.Parts.Count == 2, "Example project has unexpected inputs.");
    var animation = viewModel.Animations.Single();
    Assert(animation.Layers.Count == expectedLayerCount, "Example project has an unexpected layer count.");
    Assert(animation.Layers.All(layer => ReferenceEquals(layer.Owner, animation)), "Layer owner was not restored.");
    Assert(viewModel.Parts.All(part => ReferenceEquals(part.Owner, viewModel)), "Part owners were not restored.");
    Assert(!animation.EnableRightHandIK, "Example project must not enable cyclic right-hand IK.");
}

static void TestBatchExample(MainViewModel viewModel, MainViewModel sprintProject, MainViewModel idleProject)
{
    Assert(viewModel.Animations.Count == 2 && viewModel.Parts.Count == 2, "Batch example should share two model parts across two animations.");
    Assert(viewModel.Animations.Select(animation => animation.OutputName).Distinct(StringComparer.OrdinalIgnoreCase).Count() == 2, "Batch outputs must have unique names.");
    var sprint = viewModel.Animations.Single(animation => animation.OutputName.Contains("sprint", StringComparison.OrdinalIgnoreCase));
    var idle = viewModel.Animations.Single(animation => animation.OutputName.Contains("idle", StringComparison.OrdinalIgnoreCase));
    Assert(sprint.Layers.Count == 2 && sprint.Layers.All(layer => layer.Type == AnimationLayerType.Additive), "Batch sprint should contain two Additive layers.");
    Assert(idle.Layers.Count == 0, "Batch idle should not contain animation layers.");
    Assert(ProjectSettingsSignature(viewModel) == ProjectSettingsSignature(sprintProject), "Batch and sprint project settings have drifted.");
    Assert(ProjectSettingsSignature(viewModel) == ProjectSettingsSignature(idleProject), "Batch and idle project settings have drifted.");
    Assert(PartsSignature(viewModel) == PartsSignature(sprintProject), "Batch and sprint model parts have drifted.");
    Assert(PartsSignature(viewModel) == PartsSignature(idleProject), "Batch and idle model parts have drifted.");
    Assert(AnimationSignature(sprint) == AnimationSignature(sprintProject.Animations.Single()), "Batch and standalone sprint animations have drifted.");
    Assert(AnimationSignature(idle) == AnimationSignature(idleProject.Animations.Single()), "Batch and standalone idle animations have drifted.");
    Assert(viewModel.Animations.SelectMany(animation => animation.Layers).All(layer => layer.Owner is not null), "Batch layer ownership was not restored.");
    Assert(viewModel.Parts.All(part => ReferenceEquals(part.Owner, viewModel)), "Batch part ownership was not restored.");
}

static string ProjectSettingsSignature(MainViewModel viewModel) => JsonSerializer.Serialize(new
{
    viewModel.EnableAnimationTrimming,
    viewModel.LeftIKStartBoneName,
    viewModel.LeftIKMidBoneName,
    viewModel.LeftIKEndBoneName,
    viewModel.LeftIKTargetBoneName,
    viewModel.RightIKStartBoneName,
    viewModel.RightIKMidBoneName,
    viewModel.RightIKEndBoneName,
    viewModel.RightIKTargetBoneName,
    viewModel.OutputPrefix,
    viewModel.OutputSuffix,
    viewModel.OutputFormat,
    viewModel.MatchOldCallOfDuty,
});

static string PartsSignature(MainViewModel viewModel) => JsonSerializer.Serialize(
    viewModel.Parts.Select(part => new { part.FilePath, part.ParentBoneTag, part.Type }));

static string AnimationSignature(Animation animation) => JsonSerializer.Serialize(new
{
    animation.OutputFramerate,
    animation.Name,
    animation.OutputName,
    animation.OutputFolder,
    animation.EnableLeftHandIK,
    animation.EnableRightHandIK,
    animation.UseExperimentalFeatures,
    animation.LeftHandPoseFile,
    animation.RightHandPoseFile,
    animation.LeftIKTargetBoneName,
    animation.RightIKTargetBoneName,
    Layers = animation.Layers.Select(layer => new { layer.Name, layer.Offset, layer.Color, layer.Type }),
});

static void TestStandardExample(MainViewModel viewModel, StandardExampleDefinition definition)
{
    Assert(viewModel.Animations.Count == definition.AnimationCount, $"Standard project {definition.Path} has an unexpected animation count.");
    Assert(viewModel.Parts.Count == definition.PartCount, $"Standard project {definition.Path} has an unexpected model part count.");
    Assert(viewModel.OutputFormat == definition.OutputFormat, $"Standard project {definition.Path} output format changed.");
    Assert(viewModel.Animations.Sum(animation => animation.Layers.Count) == definition.LayerCount, $"Standard project {definition.Path} animation layers changed.");
    Assert(viewModel.Animations.SelectMany(animation => animation.Layers).All(layer => layer.Owner is not null), "Standard MP5 layer ownership was not restored.");
    Assert(viewModel.Parts.All(part => ReferenceEquals(part.Owner, viewModel)), "Standard MP5 part ownership was not restored.");
}

static void TestStandardExampleHashes(string exampleDirectory, IEnumerable<StandardExampleDefinition> examples)
{
    foreach (var example in examples)
    {
        var path = ResolveExamplePath(exampleDirectory, example.Path);
        var actualHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
        Assert(actualHash == example.Sha256, $"Standard source example changed: {example.Path}.");
    }
}

static void TestHawkSprintPattern(MainViewModel hawkProject, MainViewModel mp5BaseProject)
{
    var sourceSprint = mp5BaseProject.Animations.Single(animation => animation.OutputName.EndsWith("sprint_loop", StringComparison.OrdinalIgnoreCase));
    var hawkSprint = hawkProject.Animations.Single();
    Assert(sourceSprint.Name.Contains("idle", StringComparison.OrdinalIgnoreCase), "Upstream sprint-loop pattern no longer uses an idle base.");
    Assert(sourceSprint.Layers.Count == 2 && sourceSprint.Layers.All(layer => layer.Type == AnimationLayerType.Additive), "Upstream sprint-loop pattern should contain two Additive layers.");
    Assert(hawkSprint.Name.Contains("idle", StringComparison.OrdinalIgnoreCase), "Hawk sprint should use the idle animation as its base.");
    Assert(hawkSprint.Layers.Count == 2 && hawkSprint.Layers.All(layer => layer.Type == AnimationLayerType.Additive), "Hawk sprint should contain two Additive layers.");
    Assert(Path.GetFileNameWithoutExtension(hawkSprint.Layers[0].Name).EndsWith("sprint_loop", StringComparison.OrdinalIgnoreCase), "The first Hawk sprint layer should be the sprint loop.");
    Assert(Path.GetFileNameWithoutExtension(hawkSprint.Layers[1].Name).EndsWith("sprint_offset_additive", StringComparison.OrdinalIgnoreCase), "The second Hawk sprint layer should be the sprint offset.");
    Assert(hawkSprint.Layers.All(layer => layer.Offset is null), "Hawk sprint layer offsets should remain unset like the upstream example.");
}

static void TestExampleManifest(string exampleDirectory, ExampleManifest manifest)
{
    Assert(manifest.SchemaVersion == 1, "Unsupported example manifest schema.");
    Assert(manifest.StandardExamples.Select(example => example.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(["mp5-base", "mp5-grip"]), "Manifest must declare both source projects as the standards.");
    Assert(manifest.ImprovedExamples.Select(example => example.Id).ToHashSet(StringComparer.OrdinalIgnoreCase)
        .SetEquals(["hawk-sprint", "hawk-idle", "hawk-batch"]), "Manifest must declare every improved Hawk project by role.");

    var declaredFiles = manifest.StandardExamples.Select(example => example.Path)
        .Concat(manifest.ImprovedExamples.Select(example => example.Path))
        .Concat(manifest.Documentation)
        .Append("manifest.json")
        .Select(NormalizeExamplePath)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    foreach (var relativePath in declaredFiles)
        Assert(File.Exists(ResolveExamplePath(exampleDirectory, relativePath)), $"Manifest example file is missing: {relativePath}.");

    var actualFiles = Directory.EnumerateFiles(exampleDirectory, "*", SearchOption.AllDirectories)
        .Select(path => NormalizeExamplePath(Path.GetRelativePath(exampleDirectory, path)))
        .Where(path => !IsIgnoredExampleOutput(path))
        .ToHashSet(StringComparer.OrdinalIgnoreCase);
    var unexpected = actualFiles.Except(declaredFiles, StringComparer.OrdinalIgnoreCase).ToArray();
    Assert(unexpected.Length == 0, "Example directory contains undeclared files: " + string.Join(", ", unexpected));
    var missing = declaredFiles.Except(actualFiles, StringComparer.OrdinalIgnoreCase).ToArray();
    Assert(missing.Length == 0, "Example manifest declares missing files: " + string.Join(", ", missing));
}

static string NormalizeExamplePath(string path) => path.Replace('\\', '/');

static bool IsIgnoredExampleOutput(string path) =>
    path.StartsWith("Output/", StringComparison.OrdinalIgnoreCase) ||
    path.StartsWith("Hawk/Output/", StringComparison.OrdinalIgnoreCase);

static ExampleManifest LoadExampleManifest(string exampleDirectory)
{
    var path = Path.Combine(exampleDirectory, "manifest.json");
    if (!File.Exists(path))
        throw new FileNotFoundException("Example manifest is missing.", path);

    return JsonSerializer.Deserialize<ExampleManifest>(File.ReadAllText(path))
        ?? throw new InvalidDataException("Example manifest is empty.");
}

static string ResolveExamplePath(string exampleDirectory, string relativePath)
{
    var root = Path.GetFullPath(exampleDirectory);
    var path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar)));
    Assert(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase), $"Example path escapes its root: {relativePath}.");
    return path;
}

static MainViewModel LoadExampleProject(string exampleDirectory, string relativePath)
{
    var viewModel = new MainViewModel(_ => { }, string.Empty);
    var projectFile = ResolveExamplePath(exampleDirectory, relativePath);
    if (!File.Exists(projectFile))
        throw new FileNotFoundException("Example project is missing.", projectFile);
    viewModel.LoadProjectFile(projectFile);
    return viewModel;
}

static string FindFile(params string[] relativeParts)
{
    foreach (var seed in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        for (var directory = new DirectoryInfo(seed); directory is not null; directory = directory.Parent)
        {
            var direct = Path.Combine(new[] { directory.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(direct))
                return direct;

            var nested = Path.Combine(
                new[] { directory.FullName, "fork", "AlchemyStars" }.Concat(relativeParts).ToArray());
            if (File.Exists(nested))
                return nested;
        }
    }

    throw new FileNotFoundException("Unable to locate test fixture: " + Path.Combine(relativeParts));
}

static void ValidateMayaPackage(string path, IEnumerable<Part> sourceParts)
{
    Assert(File.Exists(path), "Output CAST was not created.");
    var cast = CastReader.Load(path);
    var nodes = cast.RootNodes.SelectMany(DescendantsAndSelf).ToArray();
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Model) == 1, "CAST package must contain one physically merged model.");
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Animation) == 1, "CAST package must contain exactly one animation.");
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Curve) > 0, "CAST package contains no animation curves.");
    var model = nodes.OfType<ModelNode>().Single();
    var skeleton = model.Skeleton ?? throw new InvalidDataException("Merged CAST model has no skeleton.");
    Assert(skeleton.Bones.Count(bone => bone.ParentIndex < 0) == 1, "Merged CAST model must contain one skeleton root.");
    Assert(skeleton.Bones.GroupBy(bone => bone.Name, StringComparer.OrdinalIgnoreCase).All(group => group.Count() == 1), "Merged CAST skeleton contains duplicate bone names.");
    Assert(skeleton.Bones.Count(bone => string.Equals(bone.Name, "j_gun", StringComparison.OrdinalIgnoreCase)) == 1, "Merged CAST skeleton must contain one j_gun.");
    Assert(nodes.OfType<CurveNode>().Count(curve => string.Equals(curve.NodeName, "j_gun", StringComparison.OrdinalIgnoreCase)) == 4, "Merged CAST animation must contain all j_gun transform curves.");
    Assert(model.Meshes.All(mesh => mesh.EnumerateBoneWeights().All(influence => influence.Item1 >= 0 && influence.Item1 < skeleton.Bones.Length)), "Merged CAST mesh contains an out-of-range bone influence.");
    var sourceModels = sourceParts
        .OrderBy(part => part.Type switch
        {
            PartType.ViewHands => 0,
            PartType.Weapon => 1,
            PartType.Attachment => 2,
            _ => 3,
        })
        .SelectMany(part => CastReader.Load(part.FilePath).RootNodes)
        .SelectMany(DescendantsAndSelf)
        .OfType<ModelNode>()
        .ToArray();
    var sourceMeshes = sourceModels.SelectMany(sourceModel => sourceModel.Meshes.Select(mesh => (Model: sourceModel, Mesh: mesh))).ToArray();
    Assert(model.Meshes.Length == sourceMeshes.Length, "Merged CAST model did not retain every source mesh.");
    for (var meshIndex = 0; meshIndex < sourceMeshes.Length; meshIndex++)
    {
        var sourceModel = sourceMeshes[meshIndex].Model;
        var sourceSkeleton = sourceModel.Skeleton ?? throw new InvalidDataException("Source CAST model has no skeleton.");
        var sourceInfluences = sourceMeshes[meshIndex].Mesh.EnumerateBoneWeights().ToArray();
        var mergedInfluences = model.Meshes[meshIndex].EnumerateBoneWeights().ToArray();
        Assert(sourceInfluences.Length == mergedInfluences.Length, $"Merged CAST mesh {meshIndex} changed its influence count.");
        for (var influenceIndex = 0; influenceIndex < sourceInfluences.Length; influenceIndex++)
        {
            var sourceInfluence = sourceInfluences[influenceIndex];
            var mergedInfluence = mergedInfluences[influenceIndex];
            var sourceBoneName = sourceSkeleton.Bones[sourceInfluence.Item1].Name;
            var mergedBoneName = skeleton.Bones[mergedInfluence.Item1].Name;
            Assert(string.Equals(sourceBoneName, mergedBoneName, StringComparison.OrdinalIgnoreCase), $"Merged CAST mesh {meshIndex} remapped influence {influenceIndex} to the wrong bone.");
            Assert(Math.Abs(sourceInfluence.Item2 - mergedInfluence.Item2) < 0.000001f, $"Merged CAST mesh {meshIndex} changed influence {influenceIndex} weight.");
        }
    }
    Assert(nodes.Select(x => x.Hash).Distinct().Count() == nodes.Length, "CAST package contains duplicate hashes.");
}

static void ValidateSmdAnimation(string path, int expectedBoneCount, int expectedFrameCount)
{
    Assert(File.Exists(path), "SMD output was not created.");
    var lines = File.ReadAllLines(path);
    Assert(lines.FirstOrDefault() == "version 1", "SMD version header is missing.");
    var nodesStart = Array.IndexOf(lines, "nodes");
    var nodesEnd = Array.IndexOf(lines, "end", nodesStart + 1);
    var skeletonStart = Array.IndexOf(lines, "skeleton", nodesEnd + 1);
    Assert(nodesStart >= 0 && nodesEnd > nodesStart && skeletonStart > nodesEnd, "SMD sections are malformed.");
    var nodeLines = lines[(nodesStart + 1)..nodesEnd];
    Assert(nodeLines.Length == expectedBoneCount, "SMD did not retain every skeleton bone.");
    var gunNode = nodeLines.FirstOrDefault(line => line.Contains("\"j_gun\"", StringComparison.OrdinalIgnoreCase));
    Assert(gunNode is not null, "SMD skeleton does not contain j_gun.");
    var gunIndex = int.Parse(gunNode!.Split(' ', StringSplitOptions.RemoveEmptyEntries)[0], CultureInfo.InvariantCulture);
    var parents = nodeLines.ToDictionary(
        line => int.Parse(line.AsSpan(0, line.IndexOf(' ')), CultureInfo.InvariantCulture),
        line => int.Parse(line.AsSpan(line.LastIndexOf(' ') + 1), CultureInfo.InvariantCulture));

    var frameTimes = lines.Skip(skeletonStart + 1)
        .Where(line => line.StartsWith("time ", StringComparison.Ordinal))
        .Select(line => int.Parse(line.AsSpan(5), CultureInfo.InvariantCulture))
        .ToArray();
    Assert(frameTimes.SequenceEqual(Enumerable.Range(0, expectedFrameCount)), "SMD frames are missing or out of order.");
    var transformLines = lines.Skip(skeletonStart + 1)
        .Where(line => line.Length > 0 && char.IsDigit(line[0]))
        .ToArray();
    Assert(transformLines.Length == expectedBoneCount * expectedFrameCount,
        "SMD does not contain one local transform for every bone on every frame.");
    Assert(transformLines.All(line => line.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 7),
        "SMD transform records must contain bone, translation, and Euler rotation values.");
    var gunWorldPositions = new List<Vector3>();
    foreach (var frame in Enumerable.Range(0, expectedFrameCount))
    {
        var localTransforms = transformLines
            .Skip(frame * expectedBoneCount)
            .Take(expectedBoneCount)
            .Select(ParseSmdTransform)
            .ToDictionary(transform => transform.Index);
        var worldTransforms = new Dictionary<int, (Vector3 Position, Quaternion Rotation)>();
        (Vector3 Position, Quaternion Rotation) ResolveWorld(int index)
        {
            if (worldTransforms.TryGetValue(index, out var cached))
                return cached;
            var local = localTransforms[index];
            var parent = parents[index];
            var world = parent < 0
                ? (local.Position, local.Rotation)
                : Compose(ResolveWorld(parent), (local.Position, local.Rotation));
            worldTransforms[index] = world;
            return world;
        }
        gunWorldPositions.Add(ResolveWorld(gunIndex).Position);
    }
    Assert(gunWorldPositions.Skip(1).Any(position => Vector3.DistanceSquared(position, gunWorldPositions[0]) > 0.000001f),
        "SMD weapon joint has no world-space motion; the weapon animation was not retained.");
}

static (int Index, Vector3 Position, Quaternion Rotation) ParseSmdTransform(string line)
{
    var values = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
    var index = int.Parse(values[0], CultureInfo.InvariantCulture);
    var position = new Vector3(
        float.Parse(values[1], CultureInfo.InvariantCulture),
        float.Parse(values[2], CultureInfo.InvariantCulture),
        float.Parse(values[3], CultureInfo.InvariantCulture));
    var x = float.Parse(values[4], CultureInfo.InvariantCulture);
    var y = float.Parse(values[5], CultureInfo.InvariantCulture);
    var z = float.Parse(values[6], CultureInfo.InvariantCulture);
    var halfX = x / 2;
    var halfY = y / 2;
    var halfZ = z / 2;
    var sinX = MathF.Sin(halfX);
    var cosX = MathF.Cos(halfX);
    var sinY = MathF.Sin(halfY);
    var cosY = MathF.Cos(halfY);
    var sinZ = MathF.Sin(halfZ);
    var cosZ = MathF.Cos(halfZ);
    var rotation = Quaternion.Normalize(new Quaternion(
        sinX * cosY * cosZ - cosX * sinY * sinZ,
        cosX * sinY * cosZ + sinX * cosY * sinZ,
        cosX * cosY * sinZ - sinX * sinY * cosZ,
        cosX * cosY * cosZ + sinX * sinY * sinZ));
    return (index, position, rotation);
}

static (Vector3 Position, Quaternion Rotation) Compose(
    (Vector3 Position, Quaternion Rotation) parent,
    (Vector3 Position, Quaternion Rotation) local) =>
    (Vector3.Transform(local.Position, parent.Rotation) + parent.Position,
        Quaternion.Normalize(parent.Rotation * local.Rotation));

static IEnumerable<CastNode> DescendantsAndSelf(CastNode node)
{
    yield return node;
    foreach (var child in node.Children)
    {
        foreach (var descendant in DescendantsAndSelf(child))
        {
            yield return descendant;
        }
    }
}

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidDataException(message);
    }
}

sealed record ExampleManifest(
    int SchemaVersion,
    StandardExampleDefinition[] StandardExamples,
    ExampleDefinition[] ImprovedExamples,
    string[] Documentation);

sealed record StandardExampleDefinition(
    string Id,
    string Path,
    string Sha256,
    int AnimationCount,
    int LayerCount,
    int PartCount,
    string OutputFormat);

sealed record ExampleDefinition(string Id, string Path);
