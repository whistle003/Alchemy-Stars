namespace AlchemyStars.Avalonia;

public sealed partial class UiText
{
    public string DualAnimations => L("双持动画", "Dual wield");
    public string DualAdd => L("新建双持任务", "New dual task");
    public string DualPair => L("配对已有动画", "Pair source tasks");
    public string DualLeft => L("左侧动画任务", "Left animation task");
    public string DualRight => L("右侧动画任务", "Right animation task");
    public string DualEditLeft => L("编辑左侧动画", "Edit left source");
    public string DualEditRight => L("编辑右侧动画", "Edit right source");
    public string DualName => L("双持输出名称", "Dual output name");
    public string DualLeftMount => L("左武器挂点", "Left weapon mount");
    public string DualRightMount => L("右武器挂点", "Right weapon mount");
    public string DualSourceMount => L("源动作武器挂点", "Source weapon mount");
    public string DualHelp => L("挂点模式 · 当前工程选择一个手臂和一个武器模型。左右任务的姿势、叠加层和 IK 会先独立处理，再组装两把武器。", "Attached mode · Choose one hands model and one weapon model. Source poses, layers and IK are processed before assembling the two weapons.");
    public string DualTimingHelp => L("左右任务须使用相同帧率和处理后帧数。公共骨骼使用右侧任务，双持始终全骨骼烘焙。", "Source frame rates and processed durations must match. Shared bones use the right task; dual output always bakes all bones.");
    public string DualPairHelp => L("在动画栏目导入左右文件，再按共同前缀和 _l_ / _r_ 动作名配对。", "Import sources in Animations, then pair matching prefixes and _l_ / _r_ action names.");
    public string DualPairResult => L("新建 {0} 个双持任务；{1} 组缺少一侧动画。", "Created {0} dual tasks; {1} groups are missing one side.");
    public string DualAmbiguousPairs => L("存在重复的左右来源，请删除重复动画任务后重试。", "Duplicate source tasks make pairing ambiguous. Remove duplicates first.");
    public string DualDuplicateOutputs => L("双持任务的输出路径重复，请修改名称或目录。", "Dual tasks have duplicate output paths. Change their names or folders.");
    public string DualUnmapped => L("未绑定到所选模型的源曲线", "Source targets absent from the selected models");
    public string DualStale => L("配置已变化，请重新生成双持预览。", "Configuration changed. Rebuild the dual preview.");
    public string DualAllFolder => L("为全部双持任务选择目录", "Set folder for all dual tasks");
    public string DualExportAll => L("导出全部双持任务", "Export all dual tasks");
    public string DualExportSelected => L("导出当前双持", "Export selected dual");
    public string DualExportModels => L("导出武器模型", "Export weapon models");
    public string DualExportModelsHelp => L("额外输出 _model.cast，包含手臂、左右武器全部网格和统一骨架。不受动画格式或“仅动画 CAST”影响。关闭只停止生成配套模型文件。", "Also writes _model.cast with hands, both weapons and one skeleton, regardless of animation format or animation-only CAST. Disabling stops the companion file only.");
}
