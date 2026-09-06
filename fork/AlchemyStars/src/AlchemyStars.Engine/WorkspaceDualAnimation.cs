namespace AlchemyStars.Engine;

/// <summary>References editable source tasks, never cached/exported source files.</summary>
public sealed class WorkspaceDualAnimation : ObservableModel
{
    private string name = "dual", left = "", right = "", folder = "";
    private string leftMount = "tag_weapon_left", rightMount = "tag_weapon_right", sourceMount = "tag_weapon";
    private bool exportWeaponModels = true;
    public bool ExportWeaponModels { get => exportWeaponModels; set => SetProperty(ref exportWeaponModels, value); }
    public string Name { get => name; set => SetProperty(ref name, value ?? ""); }
    public string LeftAnimationId { get => left; set => SetProperty(ref left, value ?? ""); }
    public string RightAnimationId { get => right; set => SetProperty(ref right, value ?? ""); }
    public string OutputFolder { get => folder; set => SetProperty(ref folder, PathInput.Normalize(value)); }
    public string LeftMount { get => leftMount; set => SetProperty(ref leftMount, value ?? ""); }
    public string RightMount { get => rightMount; set => SetProperty(ref rightMount, value ?? ""); }
    public string SourceMount { get => sourceMount; set => SetProperty(ref sourceMount, value ?? ""); }
}
