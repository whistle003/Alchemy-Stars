# Alchemy Stars 1.1.9 — UI audit

## Changes / 改进

This audit follows the UI/UX skill's priorities for unclipped controls, accessible names, contrast, keyboard editing, and responsive layout. The existing dark theme and vector icon family are preserved; export algorithms and the 1.1.8 skeleton fix are unchanged.

| Area / 区域 | Finding / 问题 | Change / 改进 |
| --- | --- | --- |
| About button | Default flat-button padding consumed the icon's space | Explicit 22-DIP icon, 44-DIP button, 10-DIP padding |
| Toolbar | Width was not constrained before the language/About area | Normal WPF overflow; protected right-side controls; readable enabled icons |
| Animation layers | A path could measure only 14 DIPs | Separate full-width path row and mode/offset/actions row; neutral layer background |
| Main columns | Widths did not follow window resizing; model column bindings were missing | Recalculate animation and model widths; horizontal scrolling on narrow animation tables preserves usable inputs |
| Settings | Long formats were cramped; standalone text style lost dark-theme styling; legacy primary brush was missing | Two-column format grid, themed text inputs, valid primary/focus brush, scrollable content and fixed close action |
| About window | Fixed height could exceed a small owner | Owner-constrained size, scrollable body, fixed footer, explicit logo resource URI |
| Message windows | Long content needed a bounded scrolling region | Grid-constrained read-only text; fixed close action; owner-constrained size; readable info icon |
| Import surfaces | Empty lists did not explain how to import | Chinese/English right-click and drop hints that do not intercept pointer events |
| Accessibility | Inline icons lacked accessible names; editing shortcuts could reach batch actions | Names from localized tooltips, full-path tooltips, editor shortcut isolation while keeping Ctrl+S |
| Export tooltip | Said “selected” although export processes all project animations | Corrected both translations; no change to export scope |

## Validation / 验证

Run the actual WPF layout suite without changing saved user preferences:

```powershell
dotnet run --project fork/AlchemyStars/tests/AlchemyStars.Acceptance -c Release -- --ui-layout-only fork/AlchemyStars/output/ui-layout
```

- Chinese and English; main-window widths 900, 1100, 1366, and 1920 DIPs. Minimum test size: 900 × 520 DIPs.
- Actual compiled controls/templates: main animation table, model parts, layer-mode popup, settings output/IK tabs, About, and message windows.
- Assertions cover inline icon names/padding, toolbar separation, usable layer/model path width, About logo loading, format text bounds, dropdown item sizing, empty-layer hint hit testing, and scrollable long messages with an unobstructed close button.
- Render files are generated in the specified output directory for visual inspection. These are WPF renders, not captures of the user's desktop.
- The existing 29-check acceptance suite covers import/drop behavior, output defaults, example integrity, skeleton merging, and CAST/SMD/FBX export.

## Limits / 边界

The Computer Use runtime could not initialize on this host (kernel-assets path error), so this iteration uses in-process WPF rendering and application tests. It does not claim end-to-end mouse/keyboard automation or physical multi-monitor/OS-DPI verification. The older PowerShell UI smoke script remains available and has the expected version updated, but was not run in this session.

窄窗口中的动画表格会保留横向滚动，避免路径框、动画层和操作按钮被压扁。这不是移动端重排；本工具仍面向 Windows 桌面。系统级 DPI、多显示器和屏幕阅读器的实际体验需在对应环境继续验证。
