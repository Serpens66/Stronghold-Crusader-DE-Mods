# APIShared compatibility

The testmod depends on the public mission lifecycle, preset settings and NativeSiteRegistry contracts. It does not hash APIShared at runtime or require an exact APIShared release. The declared minimum in info.json and the plugin is retained while these contracts require no newer service. The registry was introduced in commit 5ed595c with declared plugin version 0.9.1; the preview hook uses its existing public registration, rollback and image-bound contracts.

The selected SDK is authoritative for compilation and tests. Set SHCDE_API_SHARED_DIR for a different already validated package; otherwise the local APIShared package is used. Its build provenance is checked with the shared package validator. APIShared is referenced with Private=false, never rebuilt or copied by this testmod's working-source driver.

Use build.bat /source:working /nopause to build and install the current testmod sources. Add /verifyonly for the checks without runtime build/installation, or /noinstall for an uninstalled runtime artifact. The existing branch-selection entry remains available without /source:working.

Compatibility tests compile all production sources against the selected public assembly, including inherited XAML properties. Synthetic metadata copies verify that changing the assembly version and adding an unrelated public service remain accepted, while making the required TryGetMissionLifecycle member inaccessible fails. These copies are test fixtures under .work/t, never installed. Public semantic changes still require relevant behavior/in-game review; a successful signature check cannot prove every possible future behavior.

The machine-level Zinnen hook has independent native/RedBird safety contracts; accepting an APIShared update does not weaken them. Per-build dependency hashes verify that one build leaves its SDK and installed APIShared untouched; they never pin the next build to the same files. Earlier dated API snapshots are historical evidence only.
