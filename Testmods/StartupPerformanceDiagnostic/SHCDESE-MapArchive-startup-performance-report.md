# Avoid reconstructing map archives during read-only startup inspection

SHCDE-SE 2.5.0 currently reconstructs the same appended ZIP archive three times during one startup. For `SerpsMods.map`, the observed calls take about 2.9 s, 3.0 s, and 3.3 s (roughly 9.3 s total). Two calls originate from `MapModManager`; the third originates from the `MapFileManager_UpdateWorkshopMap_Hook` path.

`MapArchive.TryLoad` materializes every entry into a new `ZipFile` and commits it, even when the caller only needs the central directory, `info.json`, and safe entry names. This decompresses and recompresses all assets, including about 36 MB of PNG data.

Suggested change:

- Add a read-only inspection path that reads the central directory, `info.json`, and normalized safe entry names without materializing or recompressing archive contents.
- Preserve the current manifest validation, path-safety decisions, exceptions, and fail-closed behavior.
- Use full reconstruction only for actual install, update, or mutation operations.
- Cache successful and failed inspection results per process by normalized full path, file length, and `LastWriteTimeUtc`; invalidate when either file property changes.
- Switch the read-only startup paths in `MapModManager` and `MapFileManager_UpdateWorkshopMap_Hook` to this inspector.

Regression tests should compare the old and new paths for ordinary maps, BepInEx maps, asset maps, empty archives, corrupt archives, and unsafe entry names. They should require identical manifest data, relevant entry sets, safety decisions, and error outcomes, plus cache invalidation on length or timestamp changes.
