using AlchemyStars.Core.Baking;
using AlchemyStars.Core.Cast;
using System.Numerics;

var tests = new (string Name, Action Run)[]
{
    ("CAST 所有属性类型可无损往返", TestPropertyRoundTrip),
    ("模型与 Additive 动画烘焙后只有一个动画", TestBakeCreatesOneAnimation),
    ("通用动画保留缩放并解析层级曲线模式覆盖", TestScaleAndCurveModeOverride),
    ("父级缩放参与 IK 世界坐标计算", TestScaledParentIk),
    ("用户提供的五个 CAST 文件可被分析", TestProvidedFilesWhenAvailable),
};

var failures = new List<string>();
foreach (var (name, run) in tests)
{
    try
    {
        run();
        Console.WriteLine($"PASS  {name}");
    }
    catch (Exception exception)
    {
        failures.Add($"{name}: {exception}");
        Console.WriteLine($"FAIL  {name}: {exception.Message}");
    }
}

static void TestScaledParentIk()
{
    var arms = CreateModel("root", includeTarget: false);
    var armsSkeleton = arms.NodesOfType(CastConstants.Skeleton).Single();
    var rootBone = armsSkeleton.ChildrenOfType(CastConstants.Bone).Single();
    rootBone.SetProperty(new CastProperty("s", "3v", new[] { 2f, 2f, 2f }));
    armsSkeleton.Children.Add(CreateBone("shoulder", 0, 20, new[] { 0f, 0f, 0f }));
    armsSkeleton.Children.Add(CreateBone("elbow", 1, 21, new[] { 1f, 0f, 0f }));
    armsSkeleton.Children.Add(CreateBone("wrist", 2, 22, new[] { 1f, 0f, 0f }));

    var weapon = CreateModel("root", includeTarget: false);
    var weaponSkeleton = weapon.NodesOfType(CastConstants.Skeleton).Single();
    weaponSkeleton.Children.Add(CreateBone("target", 0, 23, new[] { 1f, 1f, 0f }));

    var rig = SkeletonRig.FromModels(arms, weapon);
    var pose = new PoseFrame(rig);
    var chain = new IkChainNames("shoulder", "elbow", "wrist", "target");
    Equal(true, TwoBoneIkBaker.TryApply(pose, rig, chain), "缩放骨架 IK 求解");
    rig.TryGetIndex("wrist", out var wristIndex);
    rig.TryGetIndex("target", out var targetIndex);
    var error = Vector3.Distance(pose.WorldPositions[wristIndex], pose.WorldPositions[targetIndex]);
    if (error > 0.001f)
    {
        throw new InvalidOperationException($"缩放骨架 IK 误差过大：{error}");
    }
}

if (failures.Count == 0)
{
    Console.WriteLine($"\n全部 {tests.Length} 项测试通过。");
    return 0;
}

Console.Error.WriteLine($"\n{failures.Count} 项测试失败：\n{string.Join("\n\n", failures)}");
return 1;

static void TestPropertyRoundTrip()
{
    var document = new CastDocument();
    var root = new CastNode(CastConstants.Root, CastConstants.HashBase);
    root.Properties.AddRange(
    [
        new CastProperty("b", "b", new byte[] { 1, 2, 3 }),
        new CastProperty("h", "h", new ushort[] { 4, 500 }),
        new CastProperty("i", "i", new uint[] { 6, 70_000 }),
        new CastProperty("l", "l", new ulong[] { 8, 9 }),
        new CastProperty("f", "f", new float[] { 1.25f }),
        new CastProperty("d", "d", new double[] { 2.5 }),
        new CastProperty("s", "s", "星炼金术"),
        new CastProperty("2v", "2v", new float[] { 1, 2, 3, 4 }),
        new CastProperty("3v", "3v", new float[] { 1, 2, 3 }),
        new CastProperty("4v", "4v", new float[] { 0, 0, 0, 1 }),
    ]);
    document.Roots.Add(root);

    var path = TemporaryCastPath();
    try
    {
        CastIo.Save(document, path);
        var loaded = CastIo.Load(path);
        Equal(1, loaded.Roots.Count, "根节点数量");
        Equal("星炼金术", loaded.Roots[0].StringProperty("s"), "UTF-8 字符串");
        SequenceEqual(new float[] { 1, 2, 3, 4 }, loaded.Roots[0].Property("2v")!.GetFloats(), "2v");
        SequenceEqual(new ulong[] { 8, 9 }, loaded.Roots[0].Property("l")!.GetUInt64s(), "l");
    }
    finally
    {
        File.Delete(path);
    }
}

static void TestBakeCreatesOneAnimation()
{
    var armsPath = TemporaryCastPath();
    var weaponPath = TemporaryCastPath();
    var basePath = TemporaryCastPath();
    var additivePath = TemporaryCastPath();
    var outputPath = TemporaryCastPath();
    try
    {
        CastIo.Save(CreateModel("root", includeTarget: false), armsPath);
        CastIo.Save(CreateModel("root", includeTarget: true), weaponPath);
        CastIo.Save(CreateAnimation(
            frames: new[] { 0, 1 },
            translation: new[] { 1f, 1f },
            rotation: new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f }), basePath);
        CastIo.Save(CreateAnimation(
            frames: new[] { 0 },
            translation: new[] { 2f },
            rotation: new[] { 0f, 0f, 0f, 1f }), additivePath);

        var report = new AlchemyStarsBaker().Bake(new BakeRequest
        {
            ArmsModelPath = armsPath,
            WeaponModelPath = weaponPath,
            BaseAnimationPath = basePath,
            AdditiveAnimationPath = additivePath,
            OutputPath = outputPath,
            AnimationName = "test_sprint",
            EnableLeftHandIk = false,
            EnableRightHandIk = false,
        });

        Equal(1, report.AnimationCount, "报告动画数量");
        var output = CastIo.Load(outputPath);
        Equal(2, output.NodesOfType(CastConstants.Model).Count(), "模型数量");
        var animation = output.NodesOfType(CastConstants.Animation).Single();
        Equal("test_sprint", animation.StringProperty("n"), "动画名称");
        var tx = animation.ChildrenOfType(CastConstants.Curve)
            .Single(x => x.StringProperty("nn") == "root" && x.StringProperty("kp") == "tx");
        SequenceEqual(new[] { 3f, 3f }, tx.Property("kv")!.GetFloats(), "Additive 位移");
        Equal(report.BoneCount * 7, animation.ChildrenOfType(CastConstants.Curve).Count(), "每根骨骼唯一七条变换曲线");
    }
    finally
    {
        foreach (var path in new[] { armsPath, weaponPath, basePath, additivePath, outputPath })
        {
            File.Delete(path);
        }
    }
}

static void TestScaleAndCurveModeOverride()
{
    var armsPath = TemporaryCastPath();
    var weaponPath = TemporaryCastPath();
    var animationPath = TemporaryCastPath();
    var outputPath = TemporaryCastPath();
    try
    {
        CastIo.Save(CreateModel("root", includeTarget: false), armsPath);
        var weapon = CreateModel("root", includeTarget: true);
        var targetBone = weapon.NodesOfType(CastConstants.Bone)
            .Single(x => x.StringProperty("n") == "target");
        targetBone.SetProperty(new CastProperty("lp", "3v", new[] { 10f, 0f, 0f }));
        targetBone.SetProperty(new CastProperty("s", "3v", new[] { 3f, 3f, 3f }));
        CastIo.Save(weapon, weaponPath);

        var animation = CreateAnimation(
            frames: new[] { 0, 1 },
            translation: new[] { 0f, 0f },
            rotation: new[] { 0f, 0f, 0f, 1f, 0f, 0f, 0f, 1f });
        var animationNode = animation.NodesOfType(CastConstants.Animation).Single();
        animationNode.Children.Add(CreateCurve("target", CastConstants.CurveTranslateX, new[] { 0, 1 }, "f", new[] { 2f, 4f }));
        animationNode.Children.Add(CreateCurve("target", CastConstants.CurveScaleX, new[] { 0, 1 }, "f", new[] { 2f, 2f }));
        var modeOverride = new CastNode(CastConstants.CurveModeOverride, 99);
        modeOverride.Properties.Add(new CastProperty("nn", "s", "root"));
        modeOverride.Properties.Add(new CastProperty("m", "s", CastConstants.ModeRelative));
        modeOverride.Properties.Add(new CastProperty("ot", "b", new byte[] { 1 }));
        modeOverride.Properties.Add(new CastProperty("os", "b", new byte[] { 1 }));
        animationNode.Children.Add(modeOverride);
        CastIo.Save(animation, animationPath);

        new AlchemyStarsBaker().Bake(new BakeRequest
        {
            ArmsModelPath = armsPath,
            WeaponModelPath = weaponPath,
            BaseAnimationPath = animationPath,
            OutputPath = outputPath,
            AnimationName = "generic_with_override",
            EnableLeftHandIk = false,
            EnableRightHandIk = false,
        });

        var outputAnimation = CastIo.Load(outputPath).NodesOfType(CastConstants.Animation).Single();
        var curves = outputAnimation.ChildrenOfType(CastConstants.Curve).ToArray();
        SequenceEqual(
            new[] { 12f, 14f },
            FindCurveValues(curves, "target", CastConstants.CurveTranslateX),
            "CMOV 相对位移");
        SequenceEqual(
            new[] { 6f, 6f },
            FindCurveValues(curves, "target", CastConstants.CurveScaleX),
            "CMOV 相对缩放");
        SequenceEqual(
            new[] { 3f, 3f },
            FindCurveValues(curves, "target", CastConstants.CurveScaleY),
            "未动画缩放轴使用绑定值");
        Equal(14, curves.Length, "两根骨骼的完整绝对变换曲线数");
        Equal(true, curves.All(x => x.StringProperty("m") == CastConstants.ModeAbsolute), "全部输出曲线为绝对模式");
    }
    finally
    {
        foreach (var path in new[] { armsPath, weaponPath, animationPath, outputPath })
        {
            File.Delete(path);
        }
    }
}

static void TestProvidedFilesWhenAvailable()
{
    const string arms = @"D:\_tiqu\Files\viewhands_mp_base_iw8_LOD0.cast";
    const string weapon = @"D:\_tiqu\Saluki\exported_files\Merged Models\sat_vm_ar_hawk_rec_LOD0.cast";
    const string sprint = @"D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_loop.cast";
    const string offset = @"D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_offset_additive.cast";
    if (!new[] { arms, weapon, sprint, offset }.All(File.Exists))
    {
        Console.WriteLine("SKIP  用户输入文件当前不可用");
        return;
    }

    var analysis = new AlchemyStarsBaker().Analyze(arms, weapon, sprint, offset);
    Equal(141, analysis.ArmsBoneCount, "手臂骨骼数");
    Equal(74, analysis.WeaponBoneCount, "武器骨骼数");
    Equal(1, analysis.SharedBoneCount, "共享骨骼数");
    Equal(214, analysis.CombinedBoneCount, "合并骨骼数");
    Equal(0, analysis.MissingAnimationTargetCount, "缺失动画目标数");
    Equal(66, analysis.FrameEnd, "冲刺结束帧");
    Equal(true, analysis.HasLeftHandIkChain, "左手 IK 可安全求解");
    Equal(false, analysis.HasRightHandIkChain, "右手循环 IK 被拒绝");
}

static CastDocument CreateModel(string rootName, bool includeTarget)
{
    var document = new CastDocument();
    var root = new CastNode(CastConstants.Root, 1);
    var model = new CastNode(CastConstants.Model, 2);
    var skeleton = new CastNode(CastConstants.Skeleton, 3);
    skeleton.Children.Add(CreateBone(rootName, -1, 4));
    if (includeTarget)
    {
        skeleton.Children.Add(CreateBone("target", 0, 5));
    }
    model.Children.Add(skeleton);
    root.Children.Add(model);
    document.Roots.Add(root);
    return document;
}

static CastNode CreateBone(string name, int parentIndex, ulong hash, float[]? position = null)
{
    var bone = new CastNode(CastConstants.Bone, hash);
    bone.Properties.Add(new CastProperty("n", "s", name));
    bone.Properties.Add(new CastProperty("p", "i", new[] { unchecked((uint)parentIndex) }));
    bone.Properties.Add(new CastProperty("lp", "3v", position ?? new[] { 0f, 0f, 0f }));
    bone.Properties.Add(new CastProperty("lr", "4v", new[] { 0f, 0f, 0f, 1f }));
    return bone;
}

static CastDocument CreateAnimation(int[] frames, float[] translation, float[] rotation)
{
    var document = new CastDocument();
    var root = new CastNode(CastConstants.Root, 10);
    var animation = new CastNode(CastConstants.Animation, 11);
    animation.Properties.Add(new CastProperty("fr", "f", new[] { 30f }));
    animation.Properties.Add(new CastProperty("lo", "b", new byte[] { 1 }));
    animation.Children.Add(CreateCurve("root", "tx", frames, "f", translation));
    animation.Children.Add(CreateCurve("root", "rq", frames, "4v", rotation));
    root.Children.Add(animation);
    document.Roots.Add(root);
    return document;
}

static CastNode CreateCurve(string name, string property, int[] frames, string valueType, float[] values)
{
    var curve = new CastNode(CastConstants.Curve, (ulong)Random.Shared.NextInt64(100, long.MaxValue));
    curve.Properties.Add(new CastProperty("nn", "s", name));
    curve.Properties.Add(new CastProperty("kp", "s", property));
    curve.Properties.Add(new CastProperty("kb", "b", frames.Select(x => checked((byte)x)).ToArray()));
    curve.Properties.Add(new CastProperty("kv", valueType, values));
    curve.Properties.Add(new CastProperty("m", "s", "absolute"));
    return curve;
}

static float[] FindCurveValues(IEnumerable<CastNode> curves, string nodeName, string propertyName) =>
    curves.Single(x => x.StringProperty("nn") == nodeName && x.StringProperty("kp") == propertyName)
        .Property("kv")!
        .GetFloats();

static string TemporaryCastPath() => Path.Combine(Path.GetTempPath(), $"alchemy-stars-test-{Guid.NewGuid():N}.cast");

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"{label}：期望 {expected}，实际 {actual}");
    }
}

static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException($"{label}：序列不一致");
    }
}
