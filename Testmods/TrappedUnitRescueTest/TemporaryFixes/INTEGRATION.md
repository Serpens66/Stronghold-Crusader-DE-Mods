# Temporary Fixes scanner-capture correction

This folder belongs only to the disposable TrappedUnitRescueTest setup. It is not
part of BugfixesAndQoL, APIShared or a distributed Fixes package.

Build `TrappedUnitRescueTest.Fixes130.csproj`; run
`bin/TrappedUnitRescueTest.Fixes130.exe --self-test <original-fixes.dll>` to validate
the installed source without changing it. After copying the test plugin layout
into the workspace `.work` fixture, run the same executable with
`--patch-copy <copied-layout/plugins/fixes/fixes.dll>` before the official runner
installs that layout. The CLI refuses other locations and reparse points. It
validates the serialized candidate before atomically replacing the fixture entry.

The supported upstream installer captures a mutable DataScanner and reads its
CurrentAddress only when the transaction later generates the native hook. The
correction captures that UInt64 immediately after Scan. The generated native
recovery emitter, its settings and wall/moat rescue suppression remain unchanged.
Only relevant installer/callback IL is pinned; assembly versions and unrelated
methods are ignored. Unknown relevant changes fail before modification. A matching
already corrected scalar capture is skipped, including omission of an inert NOP
and the field name relocateAddress.

The explicit temporary test package enables this independent upstream correction
for both rescue-off and rescue-on runs. EnableExperimentalRescue continues to
control only the experimental rescue and its existing narrow suppression scope.
No new modsetting is introduced. The official runner must restore original Fixes
bytes on every exit. The original installed DLL and canonical external source are
never edited by this tool; no native code is repatched in a running process.

Removal: remove this folder and the dedicated build/self-test and copied-layout
patch invocations from the test build/runner. No preloader, plugin or persistent
configuration file is installed by the CLI.
