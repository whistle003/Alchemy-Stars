# Alchemy Stars · Apple Blue 图标包

根据当前 Avalonia 页面和 DESIGN-apple.md 制作的 16 枚功能图标。使用内置 imagegen 生成整套图，再用 oil-icon 切割脚本导出、清理边缘并统一配色。背景透明；蓝色线条为 #0066cc，内部白色为 #ffffff；无投影或渐变。

## 文件

- `png-512/`：512 × 512 透明 PNG，适合较大功能展示；原始整图为 1254 × 1254，512 版经过放大，并非原生 512 细节。
- `png-128/`、`png-64/`：对应尺寸的透明 PNG，适合卡片、功能说明与空状态。
- `preview.png`：浅灰背景总览。
- `qa-magenta.png`、`qa-dark.png`：高对比及深色背景边缘检查。
- `style-spec.json`、`prompt.txt`：样式构造规范及实际生成提示词。
- `raw/`：保留生成原图和供切割使用的灰底整图。
- `prepare-pack.py`：锁定最终色值、修复分离部件并生成多尺寸文件和检查图；依赖 Pillow、numpy、scipy。

## 功能对应

| 文件名 | 对应功能 |
|---|---|
| animation-library | 动画库 |
| model-parts | 模型部件 |
| dual-wield | 双持合成 |
| animation-layers | 动画图层 |
| hand-pose | 手部姿势 |
| inverse-kinematics | IK 骨骼链 |
| cast-preview | CAST 模型预览 |
| import-assets | 导入素材 |
| export-animation | 导出动画 |
| batch-processing | 批量处理 |
| project-workspace | 项目工作区 |
| output-settings | 输出设置 |
| camera-view | 相机视角 |
| timeline-playback | 时间轴播放 |
| output-naming | 输出命名 |
| language | 语言切换 |

## 使用与验证

已按用户要求接入 Avalonia 程序：16 枚图标用于对应的导航、标题、预览、导入导出和设置功能。128px 素材作为 AvaloniaResource 嵌入 `Assets/AppleBlue/`，显示时启用高质量缩放。保存、删除、缩放、播放等基本操作现由相邻 `apple-blue-controls` 补充包覆盖。应用品牌标识保持原样。

原有控件高度、命令事件、绑定及网格布局保留。生成位图的线宽与圆角近似统一，不具有矢量图标的精确网格保证。

已逐项检查透明边缘、邻格碎片与功能完整性，并修复自动切割误删的第 4、6、9 格分离部件。浅色、深色及品红背景检查图均随包保留。
