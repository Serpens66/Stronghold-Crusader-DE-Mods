# Integrated AI market fix acceptance

Date: 25 November 2026 (Europe/Berlin). BugfixesAndQoL 1.0.241.
APIShared 0.73.3, installed Script Extender 2.14.4, canonical native hash
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.

## Implementation

The functional core now belongs to BugfixesAndQoL/src/AI/Market. The host-only
EnableAiMarketOverbuyFix option is enabled by default under AI / Fixes, participates
in ordinary presets, save settings, host synchronization and search, and combines
with the host EnableMod switch. Client activation does not control this feature.
The editor remains excluded. Existing APIShared market/lifecycle/save/restore/resync
services are reused; no APIShared source or package was changed.

Published native hooks remain rooted and are controlled only by data gates. Disabled
worlds do not capture or correct orders, discard private state, save an empty ledger
and ignore ledger restores. Optional MP agreement disables the fix on all peers if
one cannot run it. Resync still clears private state on both peers without touching
native pending cells. The accepted lost-credit boundary is unchanged.

AIMarketOverbuyDiagnostic was removed from the workspace and its old host installation
was removed under the shared test mutex with the game closed. Historical audits and
acceptance evidence were moved without rewriting them. The separate Acceptance mod
and MakeDesync reader now target the main-mod owner. No old standalone save migration,
release, README edit, or new two-peer multiplayer run was performed.

## Verification

- Existing production demand-target, bounded save-codec and mission-state tests pass.
- The installed native backend passes all 22 writer relays, changed/foreign byte
  rejection, GPR/flags/SSE preservation, ammunition policy and shared entry ABI cases.
- Scoped runtime/JSON/lifecycle/native-hook/dependency/CRLF prechecks pass.
- Host/client preset suite passes, including 20 AutoSend checks.
- Full built-production settings suite passes 3,088 checks. The scoped market option
  check additionally passes 37 checks including the actual activation matrix.
- Two pre-existing AutoSend test defects were corrected in coordination with its
  owning chat: the UI guard now verifies the deliberately stronger permission;
  the preset fixture uses its intended explicit identity instead of a doubled prefix.
  No AutoSend production code, XAML permissions or SDK behavior was loosened.

## Singleplayer evidence

Run 15cc6b98321c4b22a3f26dc88ea518b0, 00:59:35–01:00:10, normal main-mod package
with Fixes, Script Extender and the external Acceptance mod on a hidden desktop.
A funded custom AI generated a real native five-item equipment request (player2,
good20). Physical supply1 followed by consumption1 left pending4 with credited1.
Two ordinary reloads preserved that raw state before the first derived fold.
Final supply4 closed the request; consumption4 did not reopen it and the next actual
native market decision attempted no further purchase for that good. Gold stayed
unchanged throughout these external supply/consumption checks.

Further loads in the same process confirmed EnableMod=false/option=true and
EnableMod=true/option=false: both native data gates stayed0, no private active row
remained, and physical supply/consumption did not adjust native pending4. All settings
changes occurred in the front end between retired missions. Re-enabling both switches
restored the saved partial credit and passed the terminal no-extra-buy check again.

Runner PASS; no Error/Fatal/MOD_FAILURE in the game log. Original plugin inventory,
settings and saves were restored and verified. Game windows were confirmed on the
nonactivated test desktop; input desktop was Default before/after. Temporary layout
was removed. No global input was used. This is SP evidence, not a new MP proof.

Raw evidence: .work/a/market-main/001/15cc6b98321c/{LogOutput.log,desktop.json}.
Relevant build and test logs: .work/a/market-main/001.

Final normal quick build and host installation passed at 01:05:54. Installed resource
hashes and DLL identity were verified; SDK runtime/content/bootstrap remained unchanged.
Installation evidence: .work/a/market-main/001/installation.json and final-build.log.

At the user's explicit request, README and1.0.241 changelog now describe both the
market fix and the integrated AutoSend feature in player terms. AutoSend's still
open real host/client gameplay acceptance is stated explicitly. Metadata-only
normal rebuild/host update passed at01:09:48; runtime source was unchanged after
the green quick build and the SP acceptance. Final package identity was reverified.
