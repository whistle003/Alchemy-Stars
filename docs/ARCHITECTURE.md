# Alchemy Stars 架构

## 数据流

```text
手臂 Model CAST ─┐
                 ├─ 骨架并集（j_gun 去重） ───────────────┐
武器 Model CAST ─┘                                        │
                                                          ▼
主 Animation CAST ── 曲线采样 ──┐                    每帧局部姿态
                                 ├─ Base + Additive ───────┤
偏移 Animation CAST ─ 强制 Additive ┘                    │
                                                          ▼
                                               循环安全检查 + 两骨 IK
                                                          │
                                                          ▼
                                             rq/tx/ty/tz 绝对曲线
                                                          │
模型根 × 2 ───────────────────────────────────────────────┤
                                                          ▼
                                             单一 Maya CAST 包
                                                          │
                                      内存校验 → 原子写盘 → 回读校验
```

## 模块

- `AlchemyStars.Core.Cast`：无第三方依赖的 CAST v1 二进制读取、保留和写入。
- `AlchemyStars.Core.Baking`：骨架并集、曲线插值、Additive 合成、IK、绝对曲线生成与不变量校验。
- `AlchemyStars.Cli`：适合批处理的 `analyze` / `bake` 命令。
- `AlchemyStars.App`：WPF 桌面界面与 Maya 用户级插件安装器。
- `maya/verify_cast_in_maya.py`：使用 Maya 2025 实际导入器检查最终产物。

## 关键决策

### 保留两个模型根

模型根保持独立可以完整保留各自的 mesh、material、file、skin weight 与骨骼索引。随包插件在 Maya 中通过共享 `j_gun` 合并骨架，避免离线改写 mesh 权重和 bind pose 带来的风险。

### 输出一个动画根

主动画、Additive 层和 IK 结果不作为多个 Maya 动画层保存。它们在程序内逐帧求值，最后仅写出一套全绝对曲线，保证导入结果不受场景旧关键帧或层顺序影响。

### 拒绝循环 IK

如果目标位于 Start 骨骼的后代，旋转求解链会同时移动目标，迭代含义不再成立。Alchemy Stars 会保留该侧原动画并给出警告；当前武器的右手目标就是这种结构。

