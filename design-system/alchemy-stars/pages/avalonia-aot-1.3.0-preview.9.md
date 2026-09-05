# Duration-proportional animation tracks — 1.3.0-preview.9

This page inherits the screenshot-led editor rules from `avalonia-aot-1.3.0-preview.8.md`.

- Every bar shares one frame range. Width encodes its source CAST frame count; horizontal position encodes the configured layer offset.
- Keep clips shorter than the visible minimum discoverable with a 64 DIP floor, but preserve the true frame count as persistent text, a localized tooltip and a UI Automation name.
- Base animation and composition layers retain distinct purple/green roles; duration never depends on color alone.
- Read metadata off the UI thread. Missing, inaccessible or malformed CAST files remain editable and show an explicit unknown-duration state.
- The standard Hawk composition is the visual reference: 1-frame base, 67-frame sprint loop and 1-frame additive offset over frames 0–66.
