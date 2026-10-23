# Lifecycle observation audit, 17.10.2026

Target: installed SHCDESE 2.14.2.0,
SHA-256 `577F9AAB5DE504BE0BFA2193C7DEB734B26076ECBBAF0A408FC9ABC4CC5E7175`.
The user explicitly authorized reassessment and installation against this target.
Historical correction acceptance remains tied to 2.14.1, hash
`E5D78FDF2EA9701336C398F7D29410F206D141FC01AA8099F30C824E6AB8A0B3`.
No historical runtime acceptance is relabeled as a 2.14.2 game test.

Installed binary decompilations in evidence.zip in this folder are the primary evidence. Local
source commit: `10628df85cd432184742a50a1dee3969a66cf89e`.
The initial whole-type byte/IL text equality attempt did not pass (different
compiler output, offsets and caller-file logging strings). It is archived as an
unsuccessful analysis experiment, not used as a passing compatibility test.
Acceptance uses a fresh review and actual transformation tests of the new binary.

## Managed path and observations

- Chainloader interception is unchanged; Finish remains listener/diagnostics only.
- Before AddComponent of the Extender, the probe prepares and publishes the
  permanently rooted MapModManager observer and existing compatibility observer.
- Plugin.Awake performs ConfirmModCompatibility before normal asset registration;
  PrepareAllAndCheckCompatibility builds its local index and retains prepared
  candidates. Its original requirement loop and return are preserved. 75 real
  dependency comparisons and transformed-copy tests pass on the installed DLL.
- Native-library-loaded initialization calls MapModManager.Setup. Setup clears
  stale staging and subscribes once. The Steam initialized Post event calls
  TryUpdateModsFromRemote. The probe never calls any of those methods itself.
- TryUpdateModsFromRemote has one original Workshop query. The inserted dup/call
  copies its returned list and leaves the original stack and list intact.
- The original map selection and TryLoad calls supply paths and already loaded
  archive metadata. The observer enumerates existing ZIP entry names only; no
  archive reopening, extraction, additional Steam query or download call.
- MapArchive Info/Archive are ordinary stored properties. TryLoad can reject a
  map, or succeed without Info. Such results cannot establish a BepInEx owner.
- After the original uninstall scan and staging loop, callbacks execute before
  ResetDeployAttempts (no-change branch) or ReadDeployAttempts (changed branch).
  Thus the snapshot precedes retry-limit handling, restart dialog and deployment.
- _SE filenames identify map names, not an authoritative GUID ownership ledger.
  Local manifests, staging hashes and delete_list entries are copied as evidence.
  Conflicting local metadata yields Unverifiable. A Unique result means one
  observed payload path claim, never verified byte ownership or removal authority.
- The installed MapModManager has no authoritative external-manager ownership
  query. SCDEModManagerLaunchId presence is reported as a hint only; its absence
  cannot establish that no external manager exists.
- No overlap with these methods was found in the local Fixes source. APIShared
  WorkshopCleanupRuntime targets TryStageModUpdate, a different method; its
  original call remains untouched. The probe imports no APIShared source/API.

## Checks and limits

Real DMD transforms preserve original instruction order, operands, labels, EH
boundaries and locals. Each inserted block has zero net stack change. Missing,
ambiguous, wrong-signature and changed-boundary cases fail before mutation.
Copies are compiled as delegates, never executed. Unity Mono JIT preparation
and actual callback execution remain pending a new game start.

All 96 actual package/removal cases passed under Windows PowerShell:
`../lifecycle-implementation/b8dbc27e58e34dcca7576166e4d63aa4/run-897c8dbe606d4a29b5fecf02d2e84dee`.
Removal remains restricted to workspace fixtures; preview imports Pair.ps1 only.

APIShared dev/Serpens66 checked read-only: own SE minimum is 2.14.0; maximum is
empty; dependency bootstrap does not gate SE on a whole-assembly hash. Generic
MaximumVersion handling validates consumer declarations and is not an APIShared
SE upper bound. No APIShared source/build/installation change is needed.

Installation backs up only own probe components/switches plus logs and verifies
APIShared and both loader identities before/after copying. Rolling back the probe
does not downgrade the Extender: the historical probe rejects 2.14.2 for correction.
