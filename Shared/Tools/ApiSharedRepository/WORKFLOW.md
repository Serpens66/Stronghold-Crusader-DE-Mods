# Independent APIShared repository and workspace subtree

The canonical public source repository is
<https://github.com/SHCDE-APIShared/APIShared>. Its independent local checkout is
`D:\CDesktopLink\Unterlagen\Mods\Stronghold Crusader DE\SHCDE-APIShared`.
The mod repository retains its regular `APIShared/` files as a Git subtree;
there are no hardlinks, junctions, submodules or automatic source updates.

The `apishared` Git remote points at the public repository. The initial join
preserves the entire existing mod history and records the matching independent
source commit using Git subtree trailers. It changes no source tree. Generated
APIShared packages remain available locally but are no longer tracked in the
API source subtree. Other mods keep using the local built public assembly.

Review a community commit or release tag, then explicitly import that revision:

```powershell
& '.\Shared\Tools\ApiSharedRepository\Import-ApiShared.ps1' -Revision '<reviewed commit or tag>'
```

The working tree must be clean. An import preserves a selected upstream commit
through a squash merge and runs the workspace ownership/runtime checks. Review
the diff, run the relevant regression tests, and build APIShared and affected
consumers through their own elevated `build.bat /nopause` drivers. Do not treat
an imported Git commit as gameplay acceptance.

For changes first developed locally in the workspace, commit the reviewed changes
and export only the API subtree to a contribution branch:

```powershell
& '.\Shared\Tools\ApiSharedRepository\Export-ApiShared.ps1' -Branch 'codex/my-api-fix'
```

Create a pull request in the independent repository, review it there, and explicitly
import the accepted commit. The tool does not force-push, auto-merge, stash, reset,
copy workspace helpers, or publish releases. An isolated round-trip test covers
import, export, source-tree equality and preservation of unrelated workspace files.

APIShared releases are prepared from the independent checkout by its own
`release.bat`; drafts are the default. Historical APIShared releases stay in the
original mod repository. New API-only releases belong in the canonical repository.
Repository administrators will be selected separately by the owner.
