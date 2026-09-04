# 炼金之星标准示例与使用说明

本目录直接采用 `D:\_tiqu\Alchemist.UI.0.0.0.9\Example` 中的两个原版 Alchemist 项目作为炼金之星的标准示例。根目录的 `MP5Base.aprj`、`MP5Grip.aprj` 均为未经改写的逐字节副本；软件构建、发布和验收都会检查它们，防止标准示例被意外修改。

## 目录结构

| 路径 | 定位 | 内容 |
| --- | --- | --- |
| `MP5Base.aprj` | 标准示例 | 原版 MP5 基础项目，26 个动画、5 个模型部件，输出 `.cast` |
| `MP5Grip.aprj` | 标准示例 | 原版 MP5 垂直握把项目，26 个动画、6 个模型部件，输出 `.seanim` |
| `manifest.json` | 示例清单 | 供验收和发布共同读取的路径、结构与标准哈希唯一数据源 |
| `Hawk\HawkSprint.aprj` | 改进示例 | 按 `MP5Base` 冲刺规则配置的 Hawk 单动画 CAST |
| `Hawk\HawkIdle.aprj` | 改进示例 | Hawk Idle 单动画 CAST |
| `Hawk\HawkBatch.aprj` | 改进示例 | 共用模型部件，分别导出 Hawk 冲刺与 Idle |

标准文件校验值记录在 `manifest.json`，当前为：

- `MP5Base.aprj`：`7352B7F9D50CA0B238E3246556B5CF9187EF4A400DA9FD41C1B75BC5A7728995`
- `MP5Grip.aprj`：`D127DB4B83F2DDB393064CECC746F248102AC7F3F3A35E4D92151D3ED08D5018`

原版 `Example` 目录只提供项目文件，不包含 MP5 模型和动画素材；项目中保存的是 `C:\AlchemistExample\IW8-MP5`、`D:\Tools\CordyCap\...` 等原作者机器上的绝对路径。首次使用时必须用文件浏览器重新选择对应素材。

## 原版标准示例的配置规则

两个 MP5 项目都采用“一个项目共享模型部件，每个动画条目独立配置和输出”的批处理结构，并包含 26 个动画条目、13 个动画层。主要差异如下：

| 项目 | 基础动画/姿势格式 | 输出格式 | 模型部件 | 左手姿势 |
| --- | --- | --- | --- | --- |
| `MP5Base` | `.seanim` | `.cast` | 手臂 + 4 个武器附件 | 标准 MP5 姿势 |
| `MP5Grip` | `.cast` | `.seanim` | 手臂 + 4 个武器附件 + 垂直握把 | 垂直握把姿势 |

两者都启用左右手 IK 和实验性功能。手臂模型使用 `Type=0`（ViewHands）；接收机使用 `Type=2`（Attachment）并挂到 `tag_weapon`，其余组件同为 Attachment。动画层类型为：`0` Normal、`1` Additive、`2` Gesture、`3` GesturePose。

原版冲刺由三行组成：

| 输出行 | 基础动画 | 动画层 |
| --- | --- | --- |
| `sprint_in` | Idle | `walk_to_sprint`，Additive |
| `sprint_loop` | Idle | 先 `sprint_loop`，再 `sprint_offset_additive`，两层均为 Additive |
| `sprint_out` | Idle | `sprint_to_walk`，Additive |

所有层的偏移均留空。这里最重要的标准是：`sprint_loop` 不是基础动画，而是叠加在 Idle 基础上的第一层；冲刺偏移是第二层。层顺序不能交换。

## 使用标准示例

1. 启动 `Alchemy Stars.exe`，点击工具栏最左侧的“载入项目”，选择 `MP5Base.aprj` 或 `MP5Grip.aprj`。也可以拖入项目文件，或把项目路径作为程序启动参数。
2. 在“动画”页逐项检查基础动画、左右手姿势和动画层路径，在“模型部件”页检查手臂、接收机和附件路径。
3. 使用路径右侧的文件夹按钮重新定位素材。动画主列表、动画层区域和模型部件列表的条目或空白处都支持右键导入；也可聚焦后按 `Shift+F10`。从资源管理器把动画文件拖到动画层区域时，文件会优先加入鼠标所在动画的层列表，不受外层当前选择影响。
4. 选择输出目录。需要保留原项目输出格式时，确认 `MP5Base` 为 `.cast`、`MP5Grip` 为 `.seanim`。
5. 使用“项目另存为”保存适配本机路径的副本，不要覆盖根目录中的标准示例。
6. 点击“保存动画”。炼金之星会让每个动画条目产生一个独立文件；CAST 输出把全部模型部件物理合并成一个 Model，并且仅包含当前条目的一个烘焙动画。导出时会自动按 ViewHands、Weapon、Attachment 规范化顺序并重映射蒙皮权重，因此界面中的手动排列不会造成 Maya 双骨架，正常导入新场景也无需启用 `Import Merge`。

## 改进后的 Hawk 验证示例

`Hawk\HawkSprint.aprj` 把上述原版 `MP5Base` 冲刺循环规则映射到用户提供的 Hawk CAST 素材：

| 顺序 | 角色 | 文件 | 类型/设置 |
| --- | --- | --- | --- |
| 基础 | Idle | `sat_vm_ar_hawk_idle.cast` | 基础动画 |
| 第 1 层 | Sprint Loop | `sat_vm_ar_hawk_sprint_loop.cast` | Additive，偏移留空 |
| 第 2 层 | Sprint Offset | `sat_vm_ar_hawk_sprint_offset_additive.cast` | Additive，偏移留空 |

该示例输出 30 FPS 的 `sat_vm_ar_hawk_sprint_alchemy_stars.cast`。手臂为 ViewHands；Hawk 是已经合并好的武器模型，使用 Weapon 并挂到 `j_gun`。左手 IK 启用。右手 IK 明确关闭，因为现有 Hawk 骨架中的右手目标位于右臂链的后代层级，求解会形成循环依赖；这是针对素材安全性的改进，不改变原版冲刺分层方式。

`Hawk\HawkIdle.aprj` 不含动画层并关闭左右手 IK。`Hawk\HawkBatch.aprj` 复用完全相同的项目级设置和模型部件，同时包含 Sprint 与 Idle 两行，分别输出两个名称唯一、各自只有一个动画的 CAST。

Hawk 示例保存了当前机器上的绝对素材路径。素材位置改变时，用文件浏览器替换路径并另存项目。后续 Hawk 冲刺验收只读取 `Hawk\HawkSprint.aprj` 中的基础动画、层顺序、IK、部件、格式与输出名称，不在测试脚本里另写一套配置。

## Maya 2025

发布目录的 `MayaPlugin` 文件夹包含 `cast.py` 与 `castplugin.py`：

1. 将两个文件放到 Maya 可访问的脚本或插件目录。
2. 在 Maya Plug-in Manager 中载入 `castplugin.py`。
3. 使用 File → Import 导入炼金之星生成的 `.cast`。
4. 将时间单位设为 30 FPS（NTSC），按导入动画设置播放范围。

当前 Hawk 冲刺基准已在本机 Maya 2025 无界面模式、关闭 `Import Merge` 的条件下验证：214 个关节、21 个可见网格、1284 条动画曲线、30 FPS、0–66 帧，并且场景中只有一个骨架根和一个 `j_gun`。两个 Additive 层已经烘焙进唯一动画，武器骨骼曲线与蒙皮均保留，不会作为多余动画节点留下。

## 发布与自动验证

运行仓库根目录下的：

```powershell
.\scripts\run-tests.ps1
.\scripts\build-release.ps1
```

验收会从 `manifest.json` 读取并检查标准 MP5 文件的 SHA-256、26 个动画、13 个动画层、模型部件数和输出格式；检查 Hawk 冲刺是否严格采用 Idle + 两个有序 Additive 层；实际导出 Sprint、Idle 和 Batch；再把本轮 Sprint 产物交给 Maya 2025。发布脚本读取同一清单，逐项确认文件已进入精简版发布包，并再次校验源文件和发布副本的标准哈希。

## 常见问题

- 提示“找不到文件”：项目保留绝对路径，请通过文件浏览器逐项替换。
- 输出失败：确认动画、模型路径有效，输出目录可写，并查看 `Alchemy-Stars-Log.log`。
- Maya 未显示材质：无界面验证会跳过材质交互界面；正常 Maya 会话由 CAST 插件导入材质，网格、蒙皮和动画不受影响。
- 想修改标准示例：先“项目另存为”。根目录的两个 MP5 文件用于兼容和回归校验，应保持与源目录一致。
