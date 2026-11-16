# AI emergency demolition and nervous Lord messages [Scope: Vanilla + mod integration]

Audit date: 2026-11-14. Installed `CrusaderDE.dll` SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The installed file was checked against `CURRENT.json` and `DATABASE_INFO.json`.
Function names in the older semantic export remain candidate labels. The control
flow, call sites, instruction boundaries and argument assignments below are
confirmed-static for this binary; they do not establish a live reproduction of
the original user's report.

## Strategy gate and message production [Vanilla facts]

Strategy update RVA `0x2F080` first computes strength and troop-production stance.
Its later comparison at `0x2F454` checks the caller's byte argument saved at
`[rsp+0x80]`. Zero branches to `0x2F653`, bypassing the remainder of the emergency
strategy path. This is not a gate limited to building demolition.

The gated path checks current pressure through `0x2B510` and `0x2B490`, maintains
two emergency countdowns and associated flags, selects weak/defender production,
and, in the eligible wealthy emergency branch, calls the resource-help helper
`0x30B90`. Its demolition actions are separate calls to `0x2D810`. A newly detected
emergency also calls the notification helper at `0x3F3B0` from `0x2F624` before
initializing the emergency countdown. Skipping the enclosing path therefore
suppresses more than demolition, including that notification opportunity.

`0x3F3B0` checks the player's eligibility through `0x187E60`, an existing message
cooldown and the relationship to the current viewpoint. When eligible it writes
the cooldown and calls `0x103AA0` with category 9 and the original Lord identity.
The current Script Extender C# enum calls category 9 `NervWeak`; the older reverse
engineering header calls it `NervousInsideWalls`. Category 8 is `NervPreSiege` in
the current C# enum and is a distinct message. Neither name is a promise that every
attack produces a video.

The ordinary downstream path resolves Lord text/audio/video at `0x103AA0`, queues
the message and presents it through `0xFEE50`. Text, speech and video become
managed output records; record 21 reaches `SFXManager.playBink`. Existing queue,
cooldown, relationship and media conditions remain relevant after restoring the
strategy path. The presentation contract is documented in
[NOTIFICATION_LAST_FRAME.md](NOTIFICATION_LAST_FRAME.md) and
[LORD_MEDIA_LOOKUP.md](LORD_MEDIA_LOOKUP.md).

## Demolition-only boundary [Vanilla facts]

RVA `0x2D810` is a 474-byte helper (end `0x2D9EA`), raw function SHA-256
`6FBDAFBC168BFF73765A2AFF122FA45EB2EFE2863D01EF20745E13F9DAFB451E`.
The native return is unused. Its audited Windows x64 ABI is
`void(void* manager, int playerId, int category, byte specialFlag)`:
RCX is unused by this body, EDX is the one-based player, R8D the category and
R9B the special filtering flag. Caller values and argument widths are preserved
at this boundary; no manager layout or player-record offset is needed to veto it.

All direct calls originate in `0x2F080`, at RVAs `0x2F516`, `0x2F527`, `0x2F5B6`,
`0x2F5CA`, `0x2F5DE` and `0x2F609`. Together these sites pass categories 1 through
7; the shared final call site selects category 1, 2, 6 or 7. The helper scans
building records belonging to that player, excluding unused/dead records, and
applies the category and special-flag building filters. Categories are internal
demolition filters, not notification categories or arbitrary building IDs.
Behavior for category values outside the caller-proven set is not generalized.

The only callees are `0xC4480` at `0x2D996`, which applies the selected building's
refund with argument 50, and `0xC4290` at `0x2D9A4`, which deletes it. The complete
helper has no separate strategy-timer, stance, message or resource-help writes.
Refund and deletion together are the action to suppress; skipping deletion alone
would leave the refund as an unintended side effect.

## Corrected consumer boundary [mod integration]

The former `AIEconomyProtectionHook.PreventEmergencyDemolition` cleared the saved
byte at the `0x2F454` comparison. With the default-enabled setting this forced the
whole emergency path to return, including the category-9 message producer. This
is a confirmed code-level suppression path, not proof of the reporter's exact
Lord, settings, save or game version.

The corrected design places the permanent owner at `0x2D810` instead. With
`EnableMod` and `PreventEmergencyDemolition` active it returns before the helper;
otherwise it invokes the original exactly once with unchanged arguments. The
strategy update, emergency flags/countdowns, stance, resource help and normal
message conditions are left to Vanilla. No notification is synthesized and no
cooldown is bypassed. Settings changes affect logical activation and never undo
or rewrite a published detour. Implementation owners are
`AIEmergencyDemolitionHook`, `AIEmergencyDemolitionNativeContract` and
`AIEmergencyDemolitionDispatch` in BugfixesAndQoL.

The unique executable-section entry pattern is:

    40 55 56 57 41 56 48 83 EC 28 41 8B F8 BE 01 00 00 00 44 8B 05 ?? ?? ?? ?? 41 0F B6 E9 44 8B F2 44 3B C6

The `.pdata` entry independently confirms the function start. NativeX64
`Indirect` displaces exactly six bytes: `push rbp` (two bytes), `push rsi`,
`push rdi`, `push r14` (two bytes). Continuation is `0x2D816`, beginning
`sub rsp,0x28`. The audited executable direct-edge inventory has no entry into
the interior of the first 18 bytes; the actual published contract remains the
six-byte span. FollowCalls and FollowJumps are disabled and chaining is refused.
No ABI parameter or return register is inferred from the prologue alone.

## Evidence, maintenance and verification limits

Reproducible baseline queries are `function 0x2F080`, `function 0x2D810`,
`callers 0x2D810`, `callees 0x2D810` and `function 0x3F3B0` through
`tools/semantic/query.ps1`. The installed instruction audit additionally validates
entry bytes, `.pdata`, all six calls, both callee calls and incoming direct edges.
See BugfixesAndQoL's `UpdateToNewDLL.md` for update/failure requirements.

The current function claim for `0x2F080` corrects the old `emergency-gate`
interpretation. Historical source hashes and earlier audit artifacts are retained;
they prove what was previously recorded, not that the gate was demolition-only.
Current curated labels/index/manifest must be regenerated from the corrected
knowledge. Historical native raw exports and analysis generations are unchanged.

At publication of this source audit, corrected-hook fixture execution, a new build
and in-game acceptance are not yet established by this document. Intended checks
cover all seven categories, active/inactive dispatch, original argument forwarding,
refund/deletion parity when inactive, absence of both when active, the real
Indirect backend span and preservation of surrounding strategy/message behavior.
Live media decoding and a real host/client run remain separate acceptance evidence.
