# Arrangement tooltip review

Starting snapshots match HEAD. APIShared, versions, protocol and README remain
unchanged. New public game accesses are MainViewModel's existing public enter/
leave command getters and public TroopsPanelRollover getter/setter, validated
against the installed true Assembly-CSharp.dll. The installed Noesis converter
contract was checked; the converter returns float for FontSize.

The original complete managed ToggleControlGroups hover path clears resource
labels/icons, shows TroopsPanelRollover and hides TroopsPanelRollover2. The new
button invokes that public command and replaces only its localized text. Leave
checks owner identity and text before invoking the original public leave command.
Menu close, unavailable runtime, troop deselection and scene/host changes clear
ownership. Existing camera/presentation callbacks and bindings remain rooted.

Executable tests compile production menu and converter sources, and verify
localized original-command dispatch, owned close, foreign takeover, failed
runtime, scene changes, deselection and float 16/20/36 -> 32/40/72. Existing
formation initialization, remembered rows and native command regressions pass.
Runtime JSON, lifecycle, real assembly, permanent hooks, XAML and CRLF checks pass.
No Script Extender or Fixes edits, additional hooks or plugin callbacks.

Visible tooltip placement, readability and screen-edge behavior await playtest.
