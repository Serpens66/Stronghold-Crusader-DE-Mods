# Independent probe package lifecycle

This is executable offline tooling, not an installed Unity runtime extension.
Pair.ps1 validates real files and the selected/loaded identity supplied by its caller.
Removal.ps1 creates ownership receipts and revalidates removal. The fresh-process
Invoke-PostExitRemoval.ps1 driver defaults to preview; -Apply deletes only the fixed
probe bootstrap. VerifiedFileDeletion.cs hashes and marks the same locked Windows
file handle for deletion, rejecting reparse points, directories and multiple links.

## Fixed identities and boundaries

- Owner: DependencyBootstrapProbe_Serp.
- Runtime: BepInEx/plugins/DependencyBootstrapProbe_Serp/DependencyBootstrapProbe.dll.
- Bootstrap: BepInEx/patchers/DependencyBootstrapProbe.Patcher.dll.
- Pair: BepInEx/plugins/DependencyBootstrapProbe_Serp/dependency-bootstrap-pair.json.
- Schema 1: Schema, Guid, Version, RuntimeSHA256, BootstrapSHA256 only.

The pair describes consistency, not publisher authentication. Unknown fields,
invalid versions/hashes, partial copies, mismatched winners or loaded identities
reject activation. Original installed hooks and their settings are not changed.
The runtime activation adapter is not implemented by this offline PowerShell code.

Deletion is restricted in code to descendants of the workspace evidence directory
_inspect/DependencyBootstrapProbe/lifecycle-implementation. That restriction must
not be removed as a shortcut to a live trial. There is no updater launch or Steam
call and no write to the SE deletion manifest. No APIShared files are targets.

## Removal protocol

A receipt can be captured only with a matching complete source package, complete
installed pair and exactly one known owner. It binds the root, owner, pair digest,
bootstrap digest and optionally origin process ID plus start time. The retained
source package remains mandatory after the installed runtime disappears.

Execution rereads the fixture ownership state and refuses unknown/external or
competing ownership, remaining subscribers, pending staging, renamed duplicate
bootstraps, a running origin/target game, changed evidence, changed bytes and links.
It repeats checks after helper preparation, then validates the final file hash and
identity through the same handle used for deletion. Already-absent targets are a
successful no-op only after validation. Errors return Rejected with a reason;
preview returns Preview, successful deletion Removed, repeat execution Absent.
The CLI exits 2 on rejection, 0 otherwise.

probe-lifecycle-state.json is an explicit fixture adapter, not Steam evidence.
Its Schema=1, OwnershipKnown, Owners, CompetingClaim and ExternallyManaged fields
must be present and typed. The worker does not infer ownership from absence alone.
Receipts and fixture states are trusted local test inputs, not signed authorization.
No subscription capture, waiting service, automatic cleanup or self-repair is active.

## Verification and remaining work

Run tests/Test-PackageLifecycle.ps1 from a fresh Windows PowerShell process. The
driver keeps fixtures, exact source copies, source hashes and results under the
workspace evidence root. It uses archived probe bytes; synthetic revision B changes
bytes at the same version without executing either package. File/hash correctness
does not establish that those synthetic assemblies can load.

The existing runtime transformation/inventory tests remain a separate prerequisite.
The next live step must observe actual selected winners, subscriptions, competing
claims and staging, and coordinate any deletion worker with the actual updater.
The locked file handle closes the check/delete file-identity race; it does not make
subscription or staging snapshots transactional with a concurrently running updater.
Linux/Proton and the final APIShared assemblies need their own acceptance.

## Difference from the APIShared development transfer

This implementation has no imports/source links or build dependency on APIShared.
It uses probe identities and actual independent PowerShell components plus a small
Windows file helper. APIShared's transferred C# PairContract, package ZIP/map tools
and manual remover remain untouched. The probe's future runtime adapter and real
ownership capture still require implementation; these tests do not certify them.
Old 80-case model sources are archived as history, not used as a fallback or proof
for this implementation. Existing public inventory schemas are not extended here.
