# Alchemy Stars v1 规格

## 原始需求

基于以下既有资产，设计并实现一款类似
[Scobalula/Alchemist](https://github.com/Scobalula/Alchemist) 的工具，项目名称和工具名称均为 **Alchemy Stars**：

- 手臂 CAST：`D:\_tiqu\Files\viewhands_mp_base_iw8_LOD0.cast`
- 武器 CAST：`D:\_tiqu\Saluki\exported_files\Merged Models\sat_vm_ar_hawk_rec_LOD0.cast`
- 冲刺动画：`D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_loop.cast`
- 冲刺 Additive 偏移：`D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_sprint_offset_additive.cast`
- Idle 动画：`D:\_tiqu\Saluki\exported_files\bo7\animations\sat_vm_ar_hawk_idle.cast`

合并后必须只存在一个目标冲刺动画，或用户选择的其他受支持动画；产物导入本机 Maya 2025 后必须可以正常播放和使用。

项目的全部文件必须存放在 `E:\Alchemy Stars\`。

## v1 验收条件

1. Windows 桌面工具和项目显示名称均为 Alchemy Stars。
2. 默认填入上述五条输入路径，支持用户另选 CAST 文件。
3. 能合并手臂与武器模型，并将 Additive 层逐帧烘焙到主动画。
4. 提供参考项目同类的左右手两骨 IK 烘焙能力。
5. 检测并拒绝会因目标位于求解链后代而产生的循环 IK。
6. 输出一个 CAST 文件，其中包含模型且恰好包含一个 Animation 节点。
7. 输出动画使用唯一的绝对变换曲线，不依赖 Maya 的已有动画状态或动画层顺序。
8. 支持冲刺预设、Idle 预设及通用主动画 CAST。
9. 提供 Maya 2025 CAST 插件安装入口与可重复运行的 Maya 验收脚本。
10. 本机 Maya 2025 验收至少确认：单一合并骨架、全部可见 mesh、动画曲线、每帧关键帧、30 FPS、0–66 帧和左手 IK 目标误差。
11. 提供可运行发布包、命令行入口、测试与中文使用文档。

