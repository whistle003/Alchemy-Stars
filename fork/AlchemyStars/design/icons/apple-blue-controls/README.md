# Apple Blue 操作图标补充包

通过内置 imagegen 生成，与现有蓝白图标配套的 16 种操作造型：保存、另存为、关于、添加、删除、上移、下移、恢复布局、适应视图、放大、缩小、上一帧、播放、暂停、下一帧、通知。

提供 png-64、png-128、png-512 三种透明 PNG；512px 由生成素材放大，不代表原生 512px 细节。颜色归一到 #0066cc 与 #ffffff。prompt.txt 保存实际生成提示词，style-spec.json 保存构造规则，raw 保留原始素材。

已接入 Avalonia 的 Assets/AppleBlue 资源，并替换 MainWindow 和 CastPreviewView 中剩余的自定义 Path 图标。AnimationTrackRow 的基础动画和图层标记也换为上一套对应素材。保留命令绑定、播放/暂停显隐条件、44px 常规控件与 32/40px 预览控件尺寸。小尺寸显示使用 HighQuality 缩放。

原始切割脚本之后，对固定网格进行分离部件保护与颜色清理；适应视图四角、逐帧竖线和暂停双竖线均保留。qa-magenta.png 与 qa-dark.png 用于边缘检查，preview.png 为浅色总览。build-pack.py 需要 Pillow、numpy、scipy。

应用 Logo、窗口/EXE 品牌图标及 Avalonia 自带下拉箭头、复选框等系统控件符号不属于本操作图标包。
