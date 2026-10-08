# Arrangement hover correction

Opening hover now attaches directly to the named Noesis button and writes the existing short Vanilla rollover, following the working Knight event/visibility path. It clears stale cost fields, hides the long rollover, and keeps foreign hover text intact on leave. Replacement buttons and viewmodels detach obsolete UI handlers. Feature disable, HUD hide and scene changes clean up logically; no native hooks are added or disposed.

The menu remains statically rooted by FormationFeature; the existing rooted FormationRuntime camera presentation callback invokes RefreshHostState. APIShared contracts, configuration, protocol, icons, row selection, checkbox font and texts remain unchanged. The three changed files were compared with current Git and their starting snapshots; diffs are retained here.

Executable fixtures compile the production menu and exercise direct event dispatch, localized short rollover visibility, cleared costs, repeated refresh, foreign short/long hover, missing runtime, scene/selection changes, HUD and viewmodel replacement. Existing formation, packet, wheel, remembered-row and queue regressions are run separately. Runtime JSON/lifecycle, installed interop, real assembly, permanent-hook, XAML and CRLF checks pass.

Visible hover placement and actual game mouse dispatch still require playtest; fixture success is not a visible in-game acceptance result. Versions and README are unchanged.
