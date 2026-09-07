# 界面主题

在“设置 → 外观”选择界面风格和明暗模式，即时生效，自动保存。当前提供 Apple（平面、胶囊主按钮）和拟物化（柔和阴影、凹陷输入框）两种风格，每种支持浅色、深色、跟随系统。

首次启动及旧配置默认保持 Apple 浅色。偏好保存在现有用户 settings.json 的 `ThemeStyle`（apple/neumorphic）与 `ThemeMode`（light/dark/system）字段中，不写进 .aprj 项目文件；未知值回退至默认值。与原偏好机制一致，配置目录不可写时仍可在当前进程切换，但无法跨重启保存。

实现位于 `src/AlchemyStars.Avalonia/AppearanceTheme.cs`（调色板和形状资源）、`Themes/Appearance.axaml`（动态样式），以及 `MainWindowViewModel.Appearance.cs`（设置绑定与文案）。颜色和形状通过动态资源更新，明暗模式同步 Avalonia FluentTheme；系统模式监听 ActualThemeVariantChanged，不重建窗口或工作区。

保留现有功能布局、44px 常规控件及 32/40px 预览控件。沿用 Apple Blue 图标包，深色主题对文字、焦点、轨道、面板和状态颜色分别处理；主按钮保持深蓝底白字。

验证：已有 `--self-test` 覆盖默认值、偏好保存互不覆盖和未知值回退。`--render-smoke <path> --appearance-smoke --page settings --window-size 900x600` 通过真实下拉框反复切换，检查即时效果、44px 高度、磁盘读取、语言刷新和系统模式事件。运行此检查须设置 `ALCHEMY_STARS_SETTINGS_PATH` 指向临时文件，避免改动日常偏好。

本次通过四种风格/明暗组合的五页渲染，以及两种深色风格的中文双持模型预览。系统通知路径通过应用实际主题变化事件验证，未修改本机 Windows 个性化设置。
