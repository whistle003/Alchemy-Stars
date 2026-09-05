# Screenshot-led editor — 1.3.0-preview.7

The supplied Beutl screenshot overrides the generic landing-page suggestions in MASTER.md. Use neutral, flat editor surfaces and real workflow controls; do not copy Beutl branding.

- Shell: 48 DIP command bar, 48 DIP icon rail, 24 DIP status strip. Keep Windows native window controls.
- Geometry: adjustable 240 DIP library, flexible center, 320 DIP inspector; 4 DIP keyboard-operable dividers and Restore layout. Lower layer area spans library and center; inspector remains full height.
- Inspector: flat, independently expandable sections; explicit input labels, scrollable content, 40 DIP fields and 44 DIP commands.
- CAST viewport: real export-engine result, read-only independent scene, CPU skinning and depth-tested clay rendering off the UI thread. Actual playback/frame controls, orbit/zoom, skeleton overlay and fit-all alternative. No textures or automatic rebuild implied.
- Curve-only CAST has no embedded skeleton: require matching current-project parts and disclose this dependency in the viewport; bone names alone do not prove bind-pose compatibility.
- Layer rows: purple base, green layers, with names and offsets. Colors supplement labels and indicate composition, not duration. The earlier frame-ruler work is canceled.
- Project toolbar: New=document plus, Open=folder, Save=disk, Save as=disk pencil. All use a fixed 24×24 canvas, explicit stroke-safe insets and 44×44 hit targets. Do not stretch path bounds to the edge.
- Accessibility: bilingual UIA names/tooltips, visible focus, native keyboard controls, non-drag alternatives and centered owner dialogs. At minimum size (900×600), labels may ellipsize with tooltips but actionable glyphs must not clip.
- Validation: render normal/minimum-size Chinese/English views, assert toolbar ink bounds, verify native UIA, preview frame changes, reverse scrub determinism and input-file integrity.
