# Compatibility and durability requests for SCDE Mod Manager

## Reviewed baseline

This request is based on SCDE Mod Manager commit `918aa87`, Script Extender 2.8.0, Fixes 1.18.1, and SerpsMods 1.0.14.

The bundled Script Extender 2.6.0 is only the bootstrap/fallback version; the manager already discovers and installs newer official releases automatically. Its updater already provides candidate staging, an atomic directory swap, backup, recovery, rollback, and reapply. The manager also already preserves `BepInEx/config`, reports ordinary file conflicts, and inventories loaded BepInEx and Script Extender components. The requests below address the remaining gaps without replacing those mechanisms.

## Important fixes

### 1. Isolate the stage from a modified source installation

`prepareStage` copies the complete selected game directory. Existing BepInEx, Doorstop, Script Extender, and plugin files therefore enter the stage without appearing in the manager's enabled-mod list, deployment receipt, or multiplayer profile. During later deployments, removal of a previously active path can also restore the corresponding unmanaged source file.

**Impact if unresolved:** Unmanaged, outdated, or duplicate plugins may load unnoticed, causing irreproducible failures while the manager reports a different setup.

Please apply one system-path policy to both the initial copy and later restoration: exclude the source `BepInEx` tree and system-package root files, and never restore those paths from the source. Deriving root exclusions from the required system-package inventories would avoid relying only on a hard-coded list. Alternatively, reject a modified source before copying it. The original installation must remain untouched.

Acceptance test: stage and redeploy from a source containing an old SE, Fixes, and another BepInEx plugin. Disabled or unmanaged copies must never appear in the stage.

### 2. Preserve explicitly declared user data across full rebuilds

The existing `BepInEx/config` backup does not cover settings stored beside plugins, notably SE `LobbyModSettings/*.msgpack` and Fixes `data/hopsFarmWhitelist.json`.

**Impact if unresolved:** Rebuilds may erase settings, presets, and user-maintained data or restore gameplay defaults.

Please add a normalized, versioned `persistentPaths` manifest contract, confined to the declaring package's payload root, plus narrow migration rules for existing SE and Fixes packages. Restore this data after deployment and retain the backup with a clear recovery path if rebuilding fails. Packaged content colliding with restored user data should fail explicitly. Do not preserve whole data directories: Fixes `data/aobcache.json`, for example, is a regenerable cache.

Acceptance test: lobby settings and the Fixes whitelist survive a forced rebuild and update, while `aobcache.json` may regenerate and collisions fail clearly.

### 3. Reserve manager system paths

An ordinary `.scdemod` can currently target paths owned by required system packages and, because ordinary mods are applied later, overwrite the runtime, Script Extender, or compatibility plugin.

**Impact if unresolved:** One malformed package can replace shared runtime components and break every installed Mod, not only itself.

Please derive reserved paths from required system-package inventories, supplemented by structural roots such as `BepInEx/core`, and reject ownership by ordinary packages both during import and immediately before deployment. Keep the current ordered, visible last-wins behavior for ordinary mod-to-mod conflicts; only system ownership needs to be exclusive.

Acceptance test: packages targeting Doorstop/BepInEx runtime files, `BepInEx/plugins/000shcdese`, or the manager compatibility plugin are rejected before stage writes, regardless of mod order.

### 4. Support manager/standalone multiplayer interoperability

This is an interoperability goal rather than a deployment defect. SCDEMM3 already records loaded BepInEx and SE components by GUID/version, but it also includes manager package-wrapper entries and cannot build a profile without `active-mods.lobby`. Consequently, peers with the same loaded runtime can be rejected solely because one installation is not manager-controlled.

**Impact if unresolved:** Runtime-identical players may be separated solely by installation method, splitting distribution, multiplayer, and support.

For a new protocol version, please make the network contract depend on normalized loaded components, while retaining package IDs, archives, ordering, and hashes as deployment provenance. Allow the compatibility plugin to build a runtime-only profile without `active-mods.lobby`; manager-specific updater policy can remain conditional on a valid manager profile. When both peers support the protocol, compare strictly. If one does not, retain the existing Script Extender checks and clearly report the additional BepInEx portion as unverified rather than fully compatible.

Acceptance test: manager and standalone installations with identical loaded GUIDs/versions match, while an actual GUID or version difference is rejected.

## Nice-to-have durability improvements

- Extend SE candidate preparation to validate the additional reflected APIs used by the compatibility plugin and resolve required assemblies against the bundled BepInEx runtime. An unknown candidate should remain unapplied; the existing update transaction need not be redesigned.

  **Impact if unresolved:** A future SE may pass basic validation yet expose incompatible APIs only after activation, blocking manager features or multiplayer.
- Validate Harmony targets by type, method signature, expected match count, and instruction shape before patching. Keep the `Assembly-CSharp.dll` hash as provenance and a known-build indicator, not as the sole compatibility gate.

  **Impact if unresolved:** A future game update may make patches fail at runtime or operate against changed, unverified targets.
- Preserve the installed `packageSha256` through manifest normalization and detect equal-ID/equal-version Workshop candidates whose content hash changed.

  **Impact if unresolved:** Users may retain different package bytes under one displayed version, missing fixes and potentially diverging in multiplayer.
- Version and document the `.scdemod` schema, stable ID rules, path ownership, and dependency semantics. Add backward-compatible `minimumVersion`/`maximumVersion` constraints while retaining exact `version` and unbounded legacy dependencies.

  **Impact if unresolved:** Authors must choose overly strict or unsafe dependencies, while undocumented rules make packages fragile across manager updates.

These changes would address the currently reproducible correctness risks first, while keeping broader compatibility hardening incremental and compatible with the manager's existing design.
