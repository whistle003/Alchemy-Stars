using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using AlchemyStars.Core.Baking;

namespace AlchemyStars.App;

public partial class MainWindow : Window
{
    private const string DefaultArms = @"D:\_tiqu\Files\viewhands_mp_base_iw8_LOD0.cast";
    private const string DefaultWeapon = @"D:\_tiqu\Saluki\exported_files\Merged Models\sat_vm_ar_hawk_rec_LOD0.cast";
    private const string DefaultSprint = @"D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_loop.cast";
    private const string DefaultOffset = @"D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_offset_additive.cast";
    private const string DefaultIdle = @"D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_idle.cast";
    private readonly AlchemyStarsBaker _baker = new();
    private bool _busy;

    public MainWindow()
    {
        InitializeComponent();
        ArmsPathTextBox.Text = DefaultArms;
        WeaponPathTextBox.Text = DefaultWeapon;
        SprintPathTextBox.Text = DefaultSprint;
        OffsetPathTextBox.Text = DefaultOffset;
        IdlePathTextBox.Text = DefaultIdle;
        OutputPathTextBox.Text = DefaultOutputPath("sat_vm_ar_hawk_sprint_loop_alchemy_stars.cast");
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        MayaStatusText.Text = MayaPluginInstaller.DescribeMaya2025();
        if (new[] { DefaultArms, DefaultWeapon, DefaultSprint, DefaultOffset }.All(File.Exists))
        {
            await AnalyzeAsync();
        }
    }

    private void AnimationModeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded || AnimationModeCombo.SelectedItem is not ComboBoxItem item)
        {
            return;
        }

        var sprintMode = string.Equals(item.Tag?.ToString(), "Sprint", StringComparison.Ordinal);
        OffsetPathTextBox.IsEnabled = sprintMode;
        LeftIkCheckBox.IsChecked = sprintMode;
        RightIkCheckBox.IsChecked = false;
        OutputPathTextBox.Text = sprintMode
            ? DefaultOutputPath("sat_vm_ar_hawk_sprint_loop_alchemy_stars.cast")
            : DefaultOutputPath("sat_vm_ar_hawk_idle_alchemy_stars.cast");
        ResetAnalysis("预设已切换，请重新检查输入。");
    }

    private void BrowseCast_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: string target })
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Alchemy Stars | 选择 CAST 文件",
            Filter = "CAST 文件 (*.cast)|*.cast|所有文件 (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false,
        };
        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        var textBox = target switch
        {
            "Arms" => ArmsPathTextBox,
            "Weapon" => WeaponPathTextBox,
            "Sprint" => SprintPathTextBox,
            "Offset" => OffsetPathTextBox,
            "Idle" => IdlePathTextBox,
            _ => null,
        };
        if (textBox is not null)
        {
            textBox.Text = dialog.FileName;
            ResetAnalysis("文件已更改，请重新检查输入。");
        }
    }

    private void BrowseOutput_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog
        {
            Title = "Alchemy Stars | 保存 Maya CAST 包",
            Filter = "CAST 文件 (*.cast)|*.cast",
            DefaultExt = ".cast",
            AddExtension = true,
            FileName = Path.GetFileName(OutputPathTextBox.Text),
            InitialDirectory = Path.GetDirectoryName(OutputPathTextBox.Text),
        };
        if (dialog.ShowDialog(this) == true)
        {
            OutputPathTextBox.Text = dialog.FileName;
            OpenOutputButton.IsEnabled = false;
        }
    }

    private async void AnalyzeButton_Click(object sender, RoutedEventArgs e) => await AnalyzeAsync();

    private async Task AnalyzeAsync()
    {
        if (_busy)
        {
            return;
        }

        try
        {
            SetBusy(true, "正在解析模型骨架与动画曲线…");
            var (baseAnimation, additive) = SelectedAnimations();
            var analysis = await Task.Run(() => _baker.Analyze(
                ArmsPathTextBox.Text.Trim(),
                WeaponPathTextBox.Text.Trim(),
                baseAnimation,
                additive));
            RenderAnalysis(analysis);
            HeaderStatusText.Text = analysis.MissingAnimationTargetCount == 0 ? "输入已就绪" : "输入有警告";
            TaskStatusText.Text = analysis.MissingAnimationTargetCount == 0
                ? "全部动画目标都能映射到合并骨架，可以开始烘焙。"
                : $"有 {analysis.MissingAnimationTargetCount} 个动画目标无法映射；可烘焙，但这些曲线会被跳过。";
        }
        catch (Exception exception)
        {
            ResetAnalysis(exception.Message);
            HeaderStatusText.Text = "检查失败";
            MessageBox.Show(this, exception.Message, "Alchemy Stars | 输入检查失败", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async void BakeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_busy)
        {
            return;
        }

        try
        {
            SetBusy(true, "正在准备烘焙…");
            OpenOutputButton.IsEnabled = false;
            var (baseAnimation, additive) = SelectedAnimations();
            var outputPath = OutputPathTextBox.Text.Trim();
            var request = new BakeRequest
            {
                ArmsModelPath = ArmsPathTextBox.Text.Trim(),
                WeaponModelPath = WeaponPathTextBox.Text.Trim(),
                BaseAnimationPath = baseAnimation,
                AdditiveAnimationPath = additive,
                OutputPath = outputPath,
                AnimationName = Path.GetFileNameWithoutExtension(outputPath),
                EnableLeftHandIk = LeftIkCheckBox.IsChecked == true,
                EnableRightHandIk = RightIkCheckBox.IsChecked == true,
            };
            var progress = new Progress<int>(value =>
            {
                BakeProgressBar.Value = value;
                TaskStatusText.Text = value switch
                {
                    < 10 => "正在读取 CAST 文件…",
                    < 75 => $"正在逐帧叠加曲线并求解 IK… {value}%",
                    < 92 => "正在构建唯一绝对动画…",
                    < 100 => "正在重新读取产物并做完整性校验…",
                    _ => "烘焙完成。",
                };
            });

            var report = await Task.Run(() => _baker.Bake(request, progress));
            HeaderStatusText.Text = "烘焙成功";
            TaskStatusText.Text =
                $"已生成 {report.FrameCount} 帧 / {report.CurveCount} 条绝对曲线；" +
                $"输出含 {report.ModelCount} 个模型、{report.AnimationCount} 个动画。";
            OpenOutputButton.IsEnabled = true;
            MessageBox.Show(
                this,
                $"唯一动画 CAST 已生成并通过回读校验。\n\n{report.OutputPath}\n\n" +
                $"帧：{report.FrameStart}–{report.FrameEnd} @ {report.Framerate:0.##} FPS\n" +
                $"骨骼：{report.BoneCount}，曲线：{report.CurveCount}\n" +
                $"左手 IK：{(report.LeftHandIkApplied ? "已烘焙" : "未应用")}，右手 IK：{(report.RightHandIkApplied ? "已烘焙" : "未应用")}",
                "Alchemy Stars | 完成",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            HeaderStatusText.Text = "烘焙失败";
            TaskStatusText.Text = exception.Message;
            MessageBox.Show(this, exception.Message, "Alchemy Stars | 烘焙失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void InstallMayaPlugin_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var result = MayaPluginInstaller.InstallFromBundle();
            MayaStatusText.Text = result;
            MessageBox.Show(this, result, "Alchemy Stars | Maya 插件", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception exception)
        {
            MessageBox.Show(this, exception.Message, "Alchemy Stars | Maya 插件安装失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        var path = OutputPathTextBox.Text.Trim();
        if (!File.Exists(path))
        {
            MessageBox.Show(this, $"找不到输出文件：{path}", "Alchemy Stars", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });
    }

    private (string BaseAnimation, string? Additive) SelectedAnimations()
    {
        var sprintMode = AnimationModeCombo.SelectedItem is ComboBoxItem item
            && string.Equals(item.Tag?.ToString(), "Sprint", StringComparison.Ordinal);
        return sprintMode
            ? (SprintPathTextBox.Text.Trim(), NullIfEmpty(OffsetPathTextBox.Text))
            : (IdlePathTextBox.Text.Trim(), null);
    }

    private void RenderAnalysis(InputAnalysis analysis)
    {
        CombinedBonesText.Text = analysis.CombinedBoneCount.ToString();
        TargetBonesText.Text = analysis.AnimationTargetCount.ToString();
        FrameRangeText.Text = $"{analysis.FrameStart}–{analysis.FrameEnd}";
        FpsText.Text = analysis.Framerate.ToString("0.##");
        MissingTargetsText.Text = analysis.MissingAnimationTargetCount.ToString();
        MissingTargetsText.Foreground = analysis.MissingAnimationTargetCount == 0
            ? FindBrush("AccentBrush")
            : FindBrush("DangerBrush");
        IkChainsText.Text = $"{(analysis.HasLeftHandIkChain ? 1 : 0) + (analysis.HasRightHandIkChain ? 1 : 0)}/2";
        AnalysisSummaryText.Text =
            $"手臂 {analysis.ArmsBoneCount} + 武器 {analysis.WeaponBoneCount}，共享 {analysis.SharedBoneCount} 根；" +
            $"Additive {analysis.AdditiveFrameCount} 帧。";
    }

    private System.Windows.Media.Brush FindBrush(string key) => (System.Windows.Media.Brush)FindResource(key);

    private void ResetAnalysis(string message)
    {
        CombinedBonesText.Text = "—";
        TargetBonesText.Text = "—";
        FrameRangeText.Text = "—";
        FpsText.Text = "—";
        MissingTargetsText.Text = "—";
        MissingTargetsText.Foreground = FindBrush("ForegroundBrush");
        IkChainsText.Text = "—";
        AnalysisSummaryText.Text = message;
        TaskStatusText.Text = message;
        BakeProgressBar.Value = 0;
    }

    private void SetBusy(bool busy, string? message = null)
    {
        _busy = busy;
        AnalyzeButton.IsEnabled = !busy;
        BakeButton.IsEnabled = !busy;
        if (!string.IsNullOrWhiteSpace(message))
        {
            TaskStatusText.Text = message;
        }
    }

    private static string DefaultOutputPath(string fileName)
    {
        var projectOutput = @"E:\Alchemy Stars\output";
        Directory.CreateDirectory(projectOutput);
        return Path.Combine(projectOutput, fileName);
    }

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
