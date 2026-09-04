# Alchemy Stars（炼金之星）

Alchemy Stars 是 [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist) 的可用化改进版，面向 Windows、CAST 第一人称武器资产与 Autodesk Maya 2025。项目保留原版 Alchemist 的 WPF 批处理界面和 RedFox 动画管线，并补齐了原仓库尚未完成的模型/动画一体化导出。

主源码位于 `fork/AlchemyStars`，固定使用与原项目同期的 RedFox 提交，避免上游变动破坏构建。先前的独立重写已保存在 Git 分支 `independent-rewrite-v1`，不再是当前实现。

## 已完成的改进

- 手臂和武器按同名骨骼合并，避免共享 `j_gun` 被改名或生成两套骨架。
- 导出时按 ViewHands → Weapon → Attachment 规范化模型顺序，并把全部部件物理合并成一个 Model；即使工程把武器放在手臂之前、Maya 未启用 Import Merge，也只会生成一套骨架。
- 每个输出 CAST 保留全部模型网格、材质和重映射后的蒙皮权重，但只包含当前选中的一个烘焙动画。
- Additive、Gesture、GesturePose、普通层以及正负帧偏移继续走原版 RedFox 采样流程，最终转为绝对动画曲线。
- 修复原版双骨 IK 算法；循环目标会被拒绝，防止右手腕通过 `j_gun` 反向依赖自身。
- 修复动画复制时右手 IK、目标覆盖与层偏移丢失的问题。
- 项目载入后恢复层和部件的 UI 所有权，拖动、删除与排序命令可继续使用。
- CAST 写入采用临时文件替换，并在写入前后验证模型数、唯一动画和节点哈希。
- 工具与产品名改为 Alchemy Stars；移除未使用的 Supabase 依赖，并将 `log4net` 更新至 3.4.0。
- 所有动画、姿势层、模型和输出目录均通过系统文件浏览器选择；程序不再自动载入带固定路径的内置预设。
- 主界面、对话框和 About 窗口支持中文/英文即时切换，并会记忆语言选择。
- 使用“炼金术瓶 + 星芒”主题的新应用图标。

## 直接使用

运行：

`E:\Alchemy Stars\release\Alchemy Stars\Alchemy Stars.exe`

程序以空白批处理启动。点击工具栏的动画与模型按钮，或使用每个路径字段右侧的文件夹按钮，通过系统文件浏览器选择文件；可选姿势文件旁的清除按钮可恢复为空。

在“动画”页的主列表区域（包括空白处）右键，选择“导入动画…”可一次加入一个或多个 `.cast`。动画行内的“动画层”子区域（包括空白处）有独立的“导入动画层…”右键菜单，不会混淆导入目标。

在“模型部件”页的列表区域（包括空白处）右键，选择“导入模型部件…”可一次加入一个或多个 `.cast`。上述列表均可聚焦后按 `Shift+F10` 打开对应菜单。

选择手臂、武器、基础动画和需要的动画层后，点击工具栏中的“保存动画”按钮即可生成：

`E:\Alchemy Stars\fork\AlchemyStars\output\sat_vm_ar_hawk_sprint_alchemy_stars.cast`

发布目录和 ZIP 均包含完整的 `Example` 文件夹。根目录的 `MP5Base.aprj`、`MP5Grip.aprj` 是从原版 Alchemist `Example` 目录直接迁移、保持逐字节一致的标准示例；统一的 `manifest.json` 为验收与发布提供路径、结构和校验值。按标准示例改进的 Hawk 冲刺、Idle 与批处理项目集中在 `Example\Hawk`。详细步骤见 [示例使用说明](fork/AlchemyStars/Example/README.zh-CN.md)，英文简版见 [Example README](fork/AlchemyStars/Example/README.en-US.md)。

示例不会自动加载。`.aprj` 可从文件浏览器打开、拖进窗口或作为命令行参数载入。批处理中可以加入更多原项目支持的动画；程序会为每个条目分别输出一个 CAST，所以每个文件始终只有一个动画。项目文件保存绝对路径，换机器后应通过文件浏览器重新选择素材与输出目录，再使用“项目另存为”。

## Maya 2025

发布目录的 `MayaPlugin` 文件夹包含官方 CAST 导入插件。将其中的 `cast.py` 和 `castplugin.py` 放入 Maya 脚本/插件路径，在 Plug-in Manager 中载入 `castplugin.py`，然后用 File → Import 导入输出 CAST。

当前冲刺产物已在本机 Maya 2025 中完成无界面实测：

- 214 个关节，只有一个骨架根和一个 `j_gun`；
- 21 个网格全部导入且可见；
- 1284 条平移/旋转曲线，每个关节每帧都有关键帧；
- 30 FPS，播放范围 0–66；
- 左手 IK 逐帧最大位置误差约 0.050；
- 右手动画正常保留，循环依赖的右手 IK 被安全跳过。

验证报告：`fork/AlchemyStars/output/sat_vm_ar_hawk_sprint_alchemy_stars.maya2025.json`。

## 构建与验证

开发构建需要 .NET 9 SDK；运行发布版需要本机安装 [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)：

```powershell
.\scripts\run-tests.ps1
.\scripts\build-release.ps1
```

`run-tests.ps1` 会编译改进后的原项目，先验证两份标准 MP5 示例没有被改写，再以 `fork\AlchemyStars\Example\Hawk\HawkSprint.aprj` 作为 Hawk 冲刺验证的唯一配置来源，用项目自身的 `ExportAnimations()` 读取其中的 Idle 基础、两个有序 Additive 层、IK、模型部件、格式和命名设置，并把本轮实际输出路径交给 Maya 2025 验收。它还会验证 Idle、批处理 CAST，以及“武器排在手臂之前”的顺序回归。`build-release.ps1` 会生成不内置 .NET 运行环境的精简 Windows x64 单文件发布包和 ZIP。

## 源码与许可

- 改进后的 Alchemist：`fork/AlchemyStars`，GPL-3.0，详见 `fork/AlchemyStars/LICENSE`。
- 固定版本 RedFox：`fork/RedFox` Git 子模块。
- Maya CAST 插件：`third_party/cast`，MIT，详见 `THIRD_PARTY_NOTICES.md`。

上游基线：Alchemist `d86da66536ed3bf304a5cb7142d360fb934f73fb`；RedFox `7031da79614d1d979b1f17cae9d4bda2c699fd53`。
