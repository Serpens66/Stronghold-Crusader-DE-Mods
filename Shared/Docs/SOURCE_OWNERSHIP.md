# Shared source ownership

`Shared` belongs to this mod workspace. APIShared owns its implementation inside
`APIShared/src`. Neither side compiles the other's internal implementation.
Mods consume the public APIShared assembly; their private adapters live here.

## Layout

| Directory | Responsibility |
| --- | --- |
| `Runtime/Diagnostics` | Mod logging and crash breadcrumbs |
| `Runtime/Threading` | Mod-owned dispatch and deferred refresh |
| `Runtime/Persistence` | Dependency-free JSON and atomic file replacement |
| `Runtime/Gameplay` | Shared player, unit, command and recruitment helpers |
| `Runtime/Native` | Shared mod-side native lookup and read-only inspection |
| `Runtime/UI` | Shared mod-side tooltip, input and action-button presentation |
| `Runtime/Localization` | Our mod text and translations |
| `Runtime/Workshop` | Mod-side Workshop paths and upload staging |
| `Adapters/APIShared` | Our adapters for public APIShared contracts |
| `Tools` | Validation, releases, Steam packaging, Extender updates and documentation tools |
| `Tests` | Tests of workspace-owned shared implementations |
| `Docs`, `Examples` | Workspace documentation and data examples |

## Independence

An MSBuild `Compile Include` compiles the referenced file into each consuming
assembly. `Link` changes only the IDE's displayed path; it is not a filesystem
hardlink, a runtime reference or a separate copy. Static state belongs to each
compiled assembly unless the implementation explicitly uses a process-wide publisher.

Changing a workspace helper must not change APIShared's source or release.
Changing an APIShared internal helper must not change a mod's compiled helpers.
Do not add source links in either direction to avoid this boundary.
Tests may intentionally compile both independent implementations.

The API's helpers were initialized from commit `a7888900e` and then isolated.
Each API-owned file records its original source path. These are independent
implementations, not fallback variants, generated copies or synchronization inputs.
For a bug fix in an originating basic algorithm, review whether the independent
implementation is affected, and apply and test a separate fix if necessary.
Mod-only features belong here; API-only features belong in APIShared.

The workspace JSON parser is `Runtime/Persistence/DependencyFreeJson.cs` in
namespace `Shared`. APIShared's independently maintained parser lives under its
own settings internals, in namespace `APIShared.Internal`. Both remain dependency
free. Runtime mods must not add serializer assemblies or duplicate the workspace
parser per mod. Parsing and serialization behavior is tested on both implementations.

Folder placement does not imply a namespace change. Existing workspace C#
namespaces and XAML identities stay stable when their files move.

Run `Tools/Validation/Test-SharedBoundaries.ps1` before a runtime build.
The migration inventory is recorded under `_inspect/SharedSeparation`.
