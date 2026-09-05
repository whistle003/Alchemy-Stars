using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AlchemyStars.Avalonia;

public sealed class MainWindowViewModel : INotifyPropertyChanged
{
    private readonly IAnimationExportEngine engine;
    private bool chinese;
    private string verificationStatus = string.Empty;

    public MainWindowViewModel(IAnimationExportEngine engine)
    {
        this.engine = engine;
        chinese = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName.Equals("zh", StringComparison.OrdinalIgnoreCase);
        ToggleLanguageCommand = new DelegateCommand(ToggleLanguage);
        RunContractCheckCommand = new DelegateCommand(RunContractCheck);
        NoOpCommand = new DelegateCommand(static () => { });
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ICommand ToggleLanguageCommand { get; }
    public ICommand RunContractCheckCommand { get; }
    public ICommand NoOpCommand { get; }

    public string WindowTitle => Chinese("炼金之星 | Avalonia AOT 迁移预览", "Alchemy Stars | Avalonia AOT migration preview");
    public string ProductName => Chinese("炼金之星", "Alchemy Stars");
    public string BranchSubtitle => Chinese("Avalonia + Native AOT 试验线", "Avalonia + Native AOT experimental line");
    public string PreviewBadge => "1.3.0-preview.1";
    public string LanguageButtonLabel => chinese ? "EN" : "中文";
    public string LanguageButtonAccessibleName => Chinese("切换到英文界面", "Switch to Chinese interface");
    public string WorkspaceLabel => Chinese("迁移工作台", "MIGRATION WORKSPACE");
    public string OverviewLabel => Chinese("迁移概览", "Migration overview");
    public string ModelPartsLabel => Chinese("模型部件", "Model parts");
    public string AnimationsLabel => Chinese("动画", "Animations");
    public string SettingsLabel => Chinese("设置", "Settings");
    public string ProductionNoticeTitle => Chinese("生产版仍可用", "Production app remains available");
    public string ProductionNoticeBody => Chinese("当前 WPF 程序保持不变；此分支只用于验证迁移与 AOT。", "The current WPF app is unchanged. This branch is only for migration and AOT validation.");
    public string MilestoneEyebrow => Chinese("里程碑 01 · 内核分离与窗口壳", "MILESTONE 01 · ENGINE SEAM AND WINDOW SHELL");
    public string PageTitle => Chinese("更快启动，更清晰的迁移路径", "Faster startup, with a migration path you can inspect");
    public string PageDescription => Chinese("已把动画转换能力放到不依赖 WPF 的内核接口后面，并建立可由 Native AOT 发布的 Avalonia 12 桌面入口。", "Animation conversion now sits behind a WPF-free engine interface, with an Avalonia 12 desktop entry point designed for Native AOT publishing.");
    public string ArchitectureTitle => Chinese("同一份已验证算法，两种界面适配", "One proven algorithm, two UI adapters");
    public string ArchitectureBody => Chinese("WPF 继续作为稳定基线；Avalonia 通过强类型请求调用相同的合并、IK、动画层、选择性烘焙和输出实现。", "WPF remains the stable baseline while Avalonia calls the same merge, IK, animation-layer, selective-bake and export implementation through typed requests.");
    public string EngineCardTitle => Chinese("转换内核已分离", "Conversion engine extracted");
    public string EngineCardBody => Chinese("不引用 WPF 或 MaterialDesign；支持 CAST、FBX、SMD 与 SEAnim。", "No WPF or MaterialDesign reference; CAST, FBX, SMD and SEAnim remain available.");
    public string AvaloniaCardTitle => "Avalonia 12.1.2";
    public string AvaloniaCardBody => Chinese("使用编译绑定、系统语言检测与键盘可见焦点。", "Compiled bindings, system-language detection and keyboard-visible focus are enabled.");
    public string AotCardTitle => "Native AOT";
    public string AotCardBody => Chinese("项目已开启裁剪和 AOT 兼容分析，发布产物不依赖托管运行时。", "Trimming and AOT analysis are enabled so published builds do not require a managed runtime.");
    public string VerificationTitle => Chinese("内核契约检查", "Engine contract check");
    public string VerificationBody => Chinese("确认输出格式、纯动画 CAST 与相关骨骼烘焙能力均从新接口公开。", "Confirms that output formats, animation-only CAST and relevant-bone baking are exposed by the new interface.");
    public string VerifyButtonLabel => Chinese("运行检查", "Run check");
    public string VerifyButtonAccessibleName => Chinese("运行转换内核契约检查", "Run conversion engine contract check");
    public string VerificationStatus => verificationStatus;
    public string FooterStatus => Chinese("转换内核就绪 · Avalonia 界面迁移进行中", "Engine ready · Avalonia UI migration in progress");
    public string VersionLabel => $"Engine {engine.Capabilities.Version}";

    internal bool IsChinese => chinese;

    private void ToggleLanguage()
    {
        chinese = !chinese;
        verificationStatus = string.Empty;
        RaiseAll();
    }

    private void RunContractCheck()
    {
        var capabilities = engine.Capabilities;
        var allFormats = Enum.GetValues<ExportFormat>().All(capabilities.OutputFormats.Contains);
        var passed = allFormats
            && capabilities.SupportsAnimationOnlyCast
            && capabilities.SupportsSelectiveBoneBake
            && capabilities.SupportsNativeAot;
        verificationStatus = passed
            ? Chinese("检查通过：转换接口能力完整。", "Check passed: the conversion interface is complete.")
            : Chinese("检查失败：转换接口能力不完整。", "Check failed: the conversion interface is incomplete.");
        OnPropertyChanged(nameof(VerificationStatus));
    }

    private string Chinese(string zh, string en) => chinese ? zh : en;

    private void RaiseAll()
    {
        OnPropertyChanged(string.Empty);
    }

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

internal sealed class DelegateCommand(Action action) : ICommand
{
    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    public bool CanExecute(object? parameter) => true;

    public void Execute(object? parameter) => action();
}
