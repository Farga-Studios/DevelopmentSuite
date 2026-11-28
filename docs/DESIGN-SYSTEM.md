# DevelopmentSuite Design System 1.0 — draft for review

## Scope
SpriteLab, MarkdownLab and future DevelopmentSuite tools only. ReleaseLab, NetworkingLab, SEALSWORN and other independent Farga Studios projects are explicitly outside scope. This is a proposed visual contract, not a shipped UI library.

## Principles
Professional, flat, calm desktop interfaces; content first; no decorative gradients, exaggerated shadows or excessive rounded cards. Each product has its own identity and accent, while navigation, spacing and interaction conventions remain shared. Support ES/EN, keyboard operation, high DPI, light and dark themes. Never communicate state by color alone.

## Foundations
Use existing dark palette as baseline: window #171B22, surface #202630, recessed #11151C, border #353E4C. Proposed light palette: window #F4F6F8, surface #FFFFFF, recessed #E9EDF2, border #CED5DE. Proposed text dark #E9EDF3 / #AAB5C5; light #202630 / #536174. Close hover #C42B36, pressed #A91E2C. Accent per product, validated for contrast; do not assign final product colors until approved logo assets are inspected. Semantic success/warning/error colors are separate from product accent. Target WCAG AA contrast, verify in both themes.
Typography: system UI font (Segoe UI on Windows); 12–14 DIP body, 16–20 section headings, 24–28 major headings. Spacing scale 4/8/12/16/24/32 DIP; corner radius 4–6 DIP for controls. Minimum interactive target 32 DIP, preferably 36; visible focus, tooltips for icon-only controls. Respect system scaling; no clipped text at 125/150/200%.

## Shell
Integrated title bar (product icon, name, window controls), traditional menus, contextual toolbar, central workspace, optional dockable side panels, status bar. Preserve OS keyboard shortcuts and window behavior. Close control uses consistent red hover, not permanent red fill. Show version in About; avoid redundant labels. Preserve existing project workflows.

## Components
Buttons: primary action, secondary action, destructive action clearly named; safe default and Escape dismissal. Tabs: active/inactive distinction, close affordance and dirty marker. Inputs: labels, validation near field, no placeholder-only label. Toolbars: consistent 16–20 DIP icon artwork with accessible names, tooltips and keyboard alternatives. Dialogs: concise title, explanatory text, action footer, scrolling where necessary; destructive action never default. Native file dialogs retained. Loading/progress: determinate when known, indeterminate otherwise, cancel only where safe.

## Updates
States: checking, up to date, available, downloading, verified and ready, installing, confirmation, cancelled, restore, error. Show current/new version and release notes when relevant. Explain what happens next; avoid internal phase names or cryptographic jargon in ordinary UI. Keep technical detail available separately. Preserve all existing trust and recovery conditions. Confirm previous version health and installer exit before resolving cancellation; require explicit attestation that no files were copied. Restore requires backups and compatibility confirmation. Never imply a failed verification means up to date.

## Identity and assets
Use approved product-specific logos, flat recognizable silhouette, shared visual construction, legibility at 16/24/32 px. Source vector where available; export ICO with multiple sizes, PNG at appropriate DPI, app/window/installer/About/README consistency. Do not replace approved artwork with an approximation. Locate the agreed SpriteLab S logo and MarkdownLab artwork before integration; verify licensing and asset provenance.

## Implementation
Keep framework-neutral design tokens and documentation in DevelopmentSuite. WPF and Avalonia adapters only after representative screens prove the contract; avoid forcing a single UI framework or introducing empty packages. First audit SpriteLab, then implement shell, assets, components and update dialogs; align MarkdownLab afterwards. Preserve file formats, preferences, shortcuts and updater trust contracts. Separate design branches from updater stabilization; pin package versions.

## Acceptance
Review real screenshots in both themes, 100/125/150/200% DPI, narrow windows, ES/EN, keyboard focus, contrast, modal cancellation, icon legibility and existing functionality. Verify installer and executable branding. No production releases without approval. Visual specification remains draft until owner approves representative screens.
