# Alchemy Stars（炼金之星）

Alchemy Stars 是 [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist) 的可用化改进版，面向 Windows、CAST 第一人称武器资产与 Autodesk Maya 2025。项目保留原版 Alchemist 的 WPF 批处理界面和 RedFox 动画管线，并补齐了原仓库尚未完成的模型/动画一体化导出。

主源码位于 `fork/AlchemyStars`，固定使用与原项目同期的 RedFox 提交，避免上游变动破坏构建。先前的独立重写已保存在 Git 分支 `independent-rewrite-v1`，不再是当前实现。

## 已完成的改进

- 手臂和武器按同名骨骼合并，避免共享 `j_gun` 被改名或生成两套骨架。
- 每个输出 CAST 保留全部模型、材质和蒙皮，但只包含当前选中的一个烘焙动画。
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

开发仓库仍保留用于自动化验收的冲刺和 Idle 项目夹具：

`E:\Alchemy Stars\fork\AlchemyStars\presets\sat_vm_ar_hawk_idle.aprj`

它们不会随程序发布，也不会自动加载。用户自己保存的 `.aprj` 可从文件浏览器打开、拖进窗口或作为命令行参数载入。批处理中可以加入更多原项目支持的动画；程序会为每个条目分别输出一个 CAST，所以每个文件始终只有一个动画。

## Maya 2025

发布目录的 `MayaPlugin` 文件夹包含官方 CAST 导入插件。将其中的 `cast.py` 和 `castplugin.py` 放入 Maya 脚本/插件路径，在 Plug-in Manager 中载入 `castplugin.py`，然后用 File → Import 导入输出 CAST。

当前冲刺产物已在本机 Maya 2025 中完成无界面实测：

- 214 个关节，只有一个骨架根和一个 `j_gun`；
- 21 个网格全部导入且可见；
- 1284 条平移/旋转曲线，每个关节每帧都有关键帧；
- 30 FPS，播放范围 0–66；
- 左手 IK 逐帧最大位置误差约 0.012；
- 右手动画正常保留，循环依赖的右手 IK 被安全跳过。

验证报告：`fork/AlchemyStars/output/sat_vm_ar_hawk_sprint_alchemy_stars.maya2025.json`。

## 构建与验证

开发构建需要 .NET 9 SDK；运行发布版需要本机安装 [.NET 9 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/9.0)：

```powershell
.\scripts\run-tests.ps1
.\scripts\build-release.ps1
```

`run-tests.ps1` 会编译改进后的原项目、用真实资产重新生成冲刺与 Idle CAST，并在检测到 `D:\Maya2025\bin\mayapy.exe` 时执行 Maya 2025 验收。`build-release.ps1` 会生成不内置 .NET 运行环境的精简 Windows x64 单文件发布包和 ZIP。

## 源码与许可

- 改进后的 Alchemist：`fork/AlchemyStars`，GPL-3.0，详见 `fork/AlchemyStars/LICENSE`。
- 固定版本 RedFox：`fork/RedFox` Git 子模块。
- Maya CAST 插件：`third_party/cast`，MIT，详见 `THIRD_PARTY_NOTICES.md`。

上游基线：Alchemist `d86da66536ed3bf304a5cb7142d360fb934f73fb`；RedFox `7031da79614d1d979b1f17cae9d4bda2c699fd53`。
