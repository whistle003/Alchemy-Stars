# Alchemy Stars

Alchemy Stars 是一个面向 Windows 与 Autodesk Maya 2025 的 CAST 模型/动画合成工具。它将手臂模型、武器模型、主动画和可选 Additive 偏移层逐帧烘焙为一个可直接导入 Maya 的 CAST 包。

项目源码、测试、设计系统、Maya 脚本、发布包和示例产物全部位于 `E:\Alchemy Stars\`。

## 已完成的默认任务

程序首次启动时已填入以下路径：

| 输入 | 路径 |
| --- | --- |
| 手臂 | `D:\_tiqu\Files\viewhands_mp_base_iw8_LOD0.cast` |
| 武器 | `D:\_tiqu\Saluki\exported_files\Merged Models\sat_vm_ar_hawk_rec_LOD0.cast` |
| 冲刺 | `D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_loop.cast` |
| 冲刺偏移 | `D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_offset_additive.cast` |
| Idle | `D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_idle.cast` |

已经生成并通过 Maya 2025 验收的文件：

- `output/sat_vm_ar_hawk_sprint_loop_alchemy_stars.cast`
- `output/sat_vm_ar_hawk_sprint_loop_alchemy_stars_maya2025.ma`
- `output/sat_vm_ar_hawk_sprint_loop_alchemy_stars_maya2025.json`

## 快速使用

1. 运行 `release/Alchemy Stars/Alchemy Stars.exe`。
2. 保持“冲刺循环 + 单帧偏移层”预设，点击“检查输入”。
3. 点击“开始烘焙唯一动画”。
4. 首次使用时点击“安装 / 更新 CAST 插件”，然后重启 Maya 2025。
5. 在 Maya 的 Plug-in Manager 中加载 `castplugin.py`，使用 File → Import 导入产出的 `.cast`。
6. 时间轴播放速度选择 Real-time；产物会自动设为 30 FPS、0–66 帧和循环播放。

随程序分发的 Maya 插件默认开启 `Import Merge`。一次导入会先通过共享的 `j_gun` 将手臂与武器合并为一套骨架，再应用唯一动画。

## 输出保证

每次烘焙都在替换目标文件前完成内存校验，并在写盘后重新读取做第二次校验：

- 模型与动画保存在同一个 CAST v1 文件；
- 恰好一个 Animation 节点；
- 每个合并骨骼只有一组完整的 `rq/tx/ty/tz/sx/sy/sz` 曲线；
- 所有曲线为 `absolute`，不依赖 Maya 中已有动画或导入顺序；
- 所有节点哈希唯一，帧范围一致，四元数有限且归一化；
- Additive 层在每帧采样后烘焙，不作为第二个动画保留；
- IK 目标若位于被求解手臂链的后代，会被自动拒绝以避免循环依赖。

这组资产中，`j_gun` 由右手腕驱动，右侧 `tag_ik_loc_ri` 又是 `j_gun` 的后代。因此右手保留原动画，左手使用独立目标完成 IK。Maya 2025 实测左手逐帧最大位置误差为 `0.00269`。

## 其他动画

“Idle 单动画”预设会使用 Idle 文件作为主动画并关闭偏移层。也可以把任何含一个 Animation 节点的 CAST 放入“冲刺循环”输入框，将它作为通用主动画处理；偏移层可留空。
通用动画的平移、旋转、缩放以及层级 `CMOV` 模式覆盖都会在程序内解析，最终统一烘焙为绝对曲线。

命令行版本支持自动化：

```powershell
dotnet run --project .\src\AlchemyStars.Cli -- bake `
  --arms "D:\path\viewhands.cast" `
  --weapon "D:\path\weapon.cast" `
  --animation "D:\path\animation.cast" `
  --additive "D:\path\offset.cast" `
  --output "E:\Alchemy Stars\output\result.cast" `
  --name "result"
```

使用 `--no-left-ik` 或 `--no-right-ik` 可关闭对应求解器。运行 `analyze` 命令可只检查输入而不写文件。

## 构建与验证

```powershell
.\scripts\run-tests.ps1
.\scripts\build-release.ps1
```

在本机 Maya 2025 中运行完整验收：

```powershell
& "D:\Maya2025\bin\mayapy.exe" `
  ".\maya\verify_cast_in_maya.py" `
  ".\output\sat_vm_ar_hawk_sprint_loop_alchemy_stars.cast" `
  ".\output\sat_vm_ar_hawk_sprint_loop_alchemy_stars_maya2025.ma" `
  ".\output\sat_vm_ar_hawk_sprint_loop_alchemy_stars_maya2025.json"
```

无界面 `mayapy` 不提供 ShaderFX UI 命令，因此验收脚本仅在该模式下把材质统一指向临时 Lambert；桌面 Maya 使用正常材质导入路径。骨架、蒙皮、网格、曲线、关键帧和时间轴仍由官方 CAST Maya 导入实现完成验证。

## 设计与来源

处理流程借鉴了 [Scobalula/Alchemist](https://github.com/Scobalula/Alchemist) 的逐帧 Additive/IK 烘焙思路；CAST 格式依据 [dtzxporter/cast](https://github.com/dtzxporter/cast) 的公开规范独立实现。项目没有链接 Alchemist 或 RedFox 代码。随包分发的官方 CAST Python/Maya 插件采用 MIT 许可，详见 `THIRD_PARTY_NOTICES.md` 与 `third_party/cast/LICENSE`。
