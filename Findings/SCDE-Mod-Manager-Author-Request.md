# Compatibility and durability requests for SCDE Mod Manager

## Reviewed baseline

This report is based on SCDE Mod Manager commit `918aa87`, official Script Extender 2.8.0, Fixes 1.18.1, and SerpsMods 1.0.14.

The bundled Script Extender 2.6.0 is correctly understood as a bootstrap/fallback package: the manager already discovers, prepares, and installs newer official releases automatically. The current updater also already has a staged candidate, directory-swap transaction, backup, recovery, rollback, and reapply support. The requests below are therefore limited to gaps that remain in the reviewed code.

The present Fixes and SerpsMods Workshop packages import successfully. The current SE 2.8.0 still provides the reflected updater and asset-registry members used by the manager compatibility component.

## High-priority corrections

### 1. Do not inherit unmanaged mod-loader files from the source installation

`prepareStage` recursively copies the selected game directory before applying managed packages. If that source already contains BepInEx, Doorstop, Script Extender, or other plugins, those files become active in the stage without being represented by `enabledMods`, `activeFiles`, the deployment receipt, or the multiplayer profile.

The same issue can recur during redeployment because removal of a previously active path restores the corresponding file from the source game when it exists there.

Recommended implementation:

- Use one shared managed-runtime path policy for both the initial copy and later restoration.
- Exclude the complete source `BepInEx` tree.
- Derive manager-owned root files from the required system-package payload inventories where possible, instead of maintaining only a fragile hard-coded list.
- Never restore a managed-runtime path from the source installation; such a path must come from an enabled manager package or be absent.
- Optionally warn about or reject a modified source before staging.
- Never modify the selected source installation itself.

Acceptance test: prepare and repeatedly redeploy from a source containing an old SE, Fixes, and an arbitrary BepInEx plugin. None of those unmanaged files may appear in the stage when their manager packages are disabled.

### 2. Preserve narrowly defined runtime-owned user data during a full rebuild

The existing `BepInEx/config` backup is useful, but persistent settings also exist beside plugin assemblies. Script Extender lobby settings use `LobbyModSettings/*.msgpack`, and Fixes stores the user-editable `data/hopsFarmWhitelist.json` below its plugin directory.

Recommended implementation:

- Preserve `LobbyModSettings/**/*.msgpack` as a built-in, narrowly scoped convention.
- Add a versioned `persistentPaths` manifest field for additional package-owned data. Normalize every path, reject traversal, and confine it to the declaring package's payload root.
- Provide a narrow migration rule for existing packages that cannot yet declare metadata, including the Fixes whitelist.
- Do not preserve whole plugin/data directories. In particular, `aobcache.json` is a regenerable cache and should not be migrated.
- Reject a collision between packaged content and restored persistent content.
- Extend the existing backup behavior: restore after package deployment and retain the backup with an actionable path if rebuilding fails.

Acceptance test: lobby settings and the Fixes whitelist survive a forced full rebuild and package update, while `aobcache.json` is allowed to regenerate. A packaged/persistent path collision must fail clearly.

### 3. Reserve system-package paths from ordinary `.scdemod` packages

The general `.scdemod` payload can currently address the stage namespace used by required system packages. Since required packages are ordered before ordinary mods, a later ordinary package can overwrite BepInEx, Script Extender, or the manager compatibility component.

Recommended implementation:

- Build the reserved set from required system-package inventories plus structural roots such as `BepInEx/core`.
- Reject ordinary packages that own a reserved path during import.
- Repeat the check immediately before deployment so manual corruption of an installed package cannot bypass it.
- Keep the current visible last-wins behavior for ordinary mod-to-mod conflicts. The existing order and conflict UI are useful and do not need to be replaced by a blanket conflict failure.

Acceptance test: ordinary packages targeting Doorstop/BepInEx runtime files, `BepInEx/plugins/000shcdese`, or the manager compatibility plugin must fail before writing the stage. Reordering mods must not bypass this rule.

## Longer-lived SE, game, and package compatibility

### Extend the existing SE candidate preflight

The Early Managed Guard currently validates the method it patches, while the multiplayer component later reflects additional SE APIs at runtime. Extend the existing read-only candidate preparation to validate those reflected types, properties, and methods, and resolve the candidate's required assemblies against the bundled BepInEx runtime before swapping it into the active package.

If a contract is unknown, leave the candidate unapplied and retain the current active package. The existing transaction, backup, and rollback design can remain unchanged.

### Validate Harmony targets structurally

Keep the known `Assembly-CSharp.dll` hash as provenance and a tested-build indicator, but do not use a whole-assembly hash as the only durable compatibility gate. Before installing each manager patch group, validate its target type and signature. Transpilers should also verify the expected match count and instruction shape. If a contract changed, fail only the affected feature with a clear diagnostic and prevent an unsafe multiplayer launch.

### Detect same-version Workshop content changes

The installed package hash is written during import, but update discovery currently compares only versions and normalized installed metadata drops `packageSha256`. Preserve and validate that hash, hash update candidates during the bounded scan, and expose equal-ID/equal-version/different-hash packages as content changes that can be atomically re-imported.

### Version and document the `.scdemod` contract

Please document the manifest schema, ID stability, path ownership, and dependency behavior. A future schema should add `minimumVersion` and `maximumVersion` dependencies while preserving existing behavior:

- `version` remains an exact dependency;
- no version field remains unbounded;
- version ranges use the manager's existing semantic comparison rules;
- exact and range constraints cannot be combined ambiguously.

## Manager/standalone multiplayer interoperability

SCDEMM3 already improves on a package-only profile: it reads loaded BepInEx plugins and registered Script Extender components by GUID/version and applies the SE client-only metadata. The remaining interoperability problem is that it merges those runtime entries with manager package-wrapper rows from `active-mods.lobby`, and refuses to build a profile when that manager file is absent.

For a new protocol version, please consider:

- Make the strict network fingerprint depend on the normalized loaded runtime, not on manager package wrappers. Keep wrapper IDs, source archives, order, and package hashes in the deployment receipt.
- Allow the compatibility plugin to run in a runtime-only mode when `active-mods.lobby` is absent. The manager-specific SE updater policy should remain conditional on a valid manager profile.
- When both peers support the protocol, compare runtime profiles strictly and report GUID/version differences.
- When a peer lacks the protocol, defer to the existing Script Extender checks and clearly mark the additional BepInEx portion as unverified instead of claiming full equality.

An exact runtime-file digest could be added by a later protocol revision, but it should not be mandatory until the canonical set of network-relevant assemblies, private dependencies, and assets—and the exclusions for user data, translations, and caches—is defined.

SerpsModsHost currently performs additional SE lobby-hash warnings. No concrete unavoidable conflict with the manager component was found, so a dedicated interop/readiness API is a useful future enhancement rather than a required correction.

## Suggested regression coverage

- Modified source installation: no inherited unmanaged loader/plugin survives initial preparation or redeployment.
- Full rebuild: lobby settings and the Fixes whitelist survive; caches do not need to survive.
- Reserved paths: ordinary package rejected at import and deployment defense-in-depth.
- Ordinary conflicts: current ordered override and visible warning remain functional.
- Unknown SE candidate: reflected-contract failure leaves the active version untouched.
- Game update: unchanged patch contracts pass structural probes; changed contracts fail the affected feature closed.
- Same version, changed bytes: detected and offered as a content refresh.
- Dependency schema: legacy exact/unbounded forms and new ranges all retain defined behavior.
- Manager versus standalone: identical loaded runtimes match under the new protocol; a GUID or version difference is rejected.
