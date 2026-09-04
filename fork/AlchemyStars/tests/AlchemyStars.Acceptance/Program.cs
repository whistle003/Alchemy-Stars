using Alchemist.InverseKinematics;
using Alchemist.UI;
using Cast.NET;
using RedFox.Graphics3D.Skeletal;
using System.Text.Json;

var outputDirectory = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "output"));

var checks = new List<object>();
var failures = new List<string>();
var sprintProject = LoadBundledProject("sat_vm_ar_hawk_sprint.aprj");
var idleProject = LoadBundledProject("sat_vm_ar_hawk_idle.aprj");
var sprintTemplate = sprintProject.Animations.Single();
var idleTemplate = idleProject.Animations.Single();

Run("Animation.Clone preserves all per-animation settings", () => TestAnimationClone(sprintTemplate));
Run("Context animation import adds every selected animation", TestContextAnimationImport);
Run("Context layer import targets only the requested animation", TestContextLayerImport);
Run("Context model import adds every selected model part", TestContextPartImport);
Run("Bundled sprint project restores layer and part ownership", () => TestBundledProject(sprintProject, expectedLayerCount: 1));
Run("Bundled idle project restores part ownership", () => TestBundledProject(idleProject, expectedLayerCount: 0));

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

    var sprintAnimation = sprintTemplate.Clone();
    sprintAnimation.OutputFolder = outputDirectory;
    var sprintOutput = Path.Combine(outputDirectory, sprintAnimation.OutputName + ".cast");
    Run("Sprint and additive offset bake into one Maya CAST", () =>
    {
        Bake(parts, skeleton, sprintAnimation);
        ValidateMayaPackage(sprintOutput);
    });

    var idleAnimation = idleTemplate.Clone();
    idleAnimation.OutputFolder = outputDirectory;
    var idleOutput = Path.Combine(outputDirectory, idleAnimation.OutputName + ".cast");
    Run("Idle bakes into a separate one-animation Maya CAST", () =>
    {
        Bake(parts, skeleton, idleAnimation);
        ValidateMayaPackage(idleOutput);
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
    passed = failures.Count == 0,
    checks,
    failures,
};
Console.WriteLine(JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
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

static void Bake(IEnumerable<Part> parts, Skeleton skeleton, Animation animation)
{
    var output = AnimationConverter.Convert(
        skeleton,
        animation,
        new IKSettings("j_shoulder_le", "j_elbow_le", "j_wrist_le", "tag_ik_loc_le"),
        new IKSettings("j_shoulder_ri", "j_elbow_ri", "j_wrist_ri", "tag_ik_loc_ri"),
        string.Empty,
        string.Empty,
        ".cast",
        matchOldCallOfDuty: false,
        parts);
    Assert(
        Path.GetFullPath(output) == Path.GetFullPath(Path.Combine(animation.OutputFolder, animation.OutputName + ".cast")),
        "Unexpected output path.");
}

static void TestAnimationClone(Animation template)
{
    var source = template.Clone();
    source.EnableLeftHandIK = false;
    source.EnableRightHandIK = true;
    source.LeftIKTargetBoneName = "left_override";
    source.RightIKTargetBoneName = "right_override";
    source.Layers.Single().Offset = 7;

    var clone = source.Clone();
    Assert(!clone.EnableLeftHandIK && clone.EnableRightHandIK, "IK flags were not cloned independently.");
    Assert(clone.LeftIKTargetBoneName == "left_override" && clone.RightIKTargetBoneName == "right_override", "IK overrides were lost.");
    Assert(clone.Layers.Single().Offset == 7 && ReferenceEquals(clone.Layers.Single().Owner, clone), "Layer offset or ownership was lost.");
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

static void TestBundledProject(MainViewModel viewModel, int expectedLayerCount)
{
    Assert(viewModel.Animations.Count == 1 && viewModel.Parts.Count == 2, "Bundled sprint preset has unexpected inputs.");
    var animation = viewModel.Animations.Single();
    Assert(animation.Layers.Count == expectedLayerCount, "Bundled preset has an unexpected layer count.");
    Assert(animation.Layers.All(layer => ReferenceEquals(layer.Owner, animation)), "Layer owner was not restored.");
    Assert(viewModel.Parts.All(part => ReferenceEquals(part.Owner, viewModel)), "Part owners were not restored.");
    Assert(!animation.EnableRightHandIK, "Bundled preset must not enable cyclic right-hand IK.");
}

static MainViewModel LoadBundledProject(string fileName)
{
    var viewModel = new MainViewModel(_ => { }, string.Empty);
    viewModel.LoadProjectFile(FindFile("presets", fileName));
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

static void ValidateMayaPackage(string path)
{
    Assert(File.Exists(path), "Output CAST was not created.");
    var cast = CastReader.Load(path);
    var nodes = cast.RootNodes.SelectMany(DescendantsAndSelf).ToArray();
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Model) >= 2, "CAST package does not contain both source models.");
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Animation) == 1, "CAST package must contain exactly one animation.");
    Assert(nodes.Count(x => x.Identifier == CastNodeIdentifier.Curve) > 0, "CAST package contains no animation curves.");
    Assert(nodes.Select(x => x.Hash).Distinct().Count() == nodes.Length, "CAST package contains duplicate hashes.");
}

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
