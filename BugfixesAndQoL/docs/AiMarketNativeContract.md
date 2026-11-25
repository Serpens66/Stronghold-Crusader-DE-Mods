# AI market pending-demand native contract

The functional owner is `src/AI/Market` in namespace
`BugfixesAndQoL.AI.Market`. It corrects only requests established by an audited
native producer. It does not infer procurement intent from a pending quantity
or retrofit requests in saves without its ledger. The editor is excluded.

Reference native SHA-256:
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
No pattern fallback is authorized: an unknown image needs a fresh producer,
consumer, layout and control-flow audit, not a hash-only update.

## Owners, data and publication

`APIShared.Economy.AiMarketEvents` owns the native purchase-decision boundary
at `0x3EC90`; the consumer must not install a competing entry hook. The shared
Windows-x64 ABI is `void(pointer root, int player)`. Native purchases inside
this boundary are excluded from external-delivery accounting and the native
original executes once. Market buy/sell helpers and recruitment remain owned
by their existing shared broker.

The consumer owns 22 exact producer MOVs in `MarketDemandSites.cs`:
food `2A464/2A488/2A4AC/2A4D0/2A4F4`, recruitment
`403A7/40597/40D3C/40D62`, materials `CC555/CC5C1/CC657/CC692/CC6E4`,
scarcity `2ADB5/2ADD7/2ADF9/2AE1B`, pitch `2EB9C`, emergency `2F56E`,
AIV `51ADB`, stone ammunition `D5791`. Full bytes are maintained with these
production records; each span is one complete 6–12-byte MOV, without an
RSP/RIP operand or an incoming interior branch.

The pending cell must equal both the public Extender resource pointer and
`module+379D840+player*583C+good*4`. Stock starts at `module+379B2A0`;
the arrays contain 25 signed Int32 cells and player IDs are 1–8. Resource stride
is `583C`, AIC root `404C950`, record size `5E4`. Validate every public Interop
offset and each register operand again after a native/Extender update.

The Indirect RedBird relay saves GPRs, flags and FXSAVE state, observes the
old cell, executes the original MOV exactly once, observes its actual result,
then jumps directly to its continuation. A 14-byte inline detour is unsafe at
several sites. Fresh private relay pages are prepared before publication;
published patches, pages and delegates remain rooted until process exit.
Live span/integrity checks reject foreign patches. Runtime enable/disable
changes data gates only; no startup execution probes on copied game entries.

## Quantity and provenance

Fixed batches retain confirmed delivery credit even after consumption:
remaining = original batch minus credited supply. A same-policy refresh of
the still-owned pending cell retains credit; an observed clear, changed amount,
source family, AIC identity or relevant policy starts a new request. Unrelated
AIC values are not part of the policy. The original stock-plus-batch sum is
evidence, not an inventory target.

Material MOV operands establish current construction cost:
cost = source stock + amount minus padding (wood padding2, otherwise0).
An unfinished material goal can grow after stock loss. Reaching the cost ends
it; later consumption cannot revive the completed request. Concurrent observed
owned material costs merge by maximum; abandoned intentions are not inferred.

Before merging or applying a request, its session must match the current owner.
A correction also requires the expected positive pending cell, current AIC and
relevant policy. Unknown/unowned requests remain unchanged. Numerically equal
foreign writes without a distinguishable observed producer are outside the
promised compatibility contract.

## Stock and optional Fixes producers

Completed stock publications are observed at `C1F4A`
(`42 89 AC 36 CC F0 12 00`, RSI=player*583C) and `B9A30`
(`48 8B 4C 24 28 48 33 CC`) after the all-player rebuild. The latter reads the
original stack; the relay must restore RSP before entering its trampoline.
All audited native non-gold receipts reach these boundaries. `B8122`
(`45 89 94 8E A0 B2 79 03`) records a known zero-stock loss before Vanilla
clamps a transient negative total. Own purchase recounts are excluded by the
per-player purchase-decision depth.

Fixes writes wood/iron/ale at `2A4D8`. Compatibility requires its complete
recognized stub, arrays and enclosing food-purchase body. The observer at
`2A42A` (`4C 69 C0 3C 58 00 00`) captures guaranteed subsequent assignments
without stock changes or an earlier nonzero-AIC return. `2A4EB` misses writes
when the preceding hops JLE is taken. Unknown Fixes semantics disable correction
for these three goods. Recheck source/body identity at session boundaries.

## Automatic ammunition

`D5710` consumes ten stone atomically for twenty shots; `D5791` is not a
general ten-unit shopping batch. Only return sites `153057`, `153156`,
`1542C6`, `1543C7`, `18FD8A` prove automatic empty-ammunition origin; their
CALL targets must resolve to `D5710`. Its prolog
`48 89 5C 24 08 57 48 83 EC 30` proves the return at original RSP+38h;
`D572B` (`48 63 DA`) proves the unit ID preserved in RBX. The snapshot is
128 bytes below that stack. Manual `18AE0` tail calls remain unowned.

Public UnitAccess identity includes owner, global ID and creation tick.
GameUnit size490 and fields Alive88, Owner92, GlobalId94, Creation98,
death marker29C, ammunition35E are hexadecimal layout contracts. Ammunition
is a signed Int16 across both SDK bytes. An ended origin or positive ammunition
ends the request; an empty live origin needs max(0,10-current stock).

## Lifecycle, saves and repair

APIShared lifecycle/admission arms the normal functional owner independently
of diagnostic hosts, tick observers, output buffers and log progress. Editor
worlds stay inactive. Save schema1 uses the existing stable owner/record IDs;
restore validates rows against the native world before its first fold and binds
them to the new session. Capture omits invalid rows without mutating live state.

Common resync Start and Complete callbacks clear this consumer's private ledger
on both peers. They never change native pending cells or import old client
credits. Repair suspends source/receipt gates; successful completion rearms
only admitted, ready gameplay. Missing provenance after reset waits for a new
native source. No Data-provider transfer for this ledger is introduced.

## Maintained checks and historical evidence

`tests/AiMarket.Tests/Logic/AiMarket.Logic.Tests.csproj` links production target,
codec and mission state. `tests/AiMarket.Tests/Native/AiMarket.Native.Tests.csproj`
executes the actual relay/Indirect backend with sentinel registers, flags, SIMD,
all producer sites, ammunition layout and shared trade entry ABIs against private
bounded continuations. Neither harness proves full game or two-peer acceptance.

The byte-preserved audits and dated acceptance reports moved to
[`Findings/AiMarket`](../Findings/AiMarket). Their old paths and diagnostic
descriptions are historical evidence, not current build instructions. In
particular `DiagnosticNativeContract-20261125.md` preserves the former testmod
native document. The observer-volume result is retained separately; its removed
overview/formatter harness is no longer part of functional verification.
## Main-mod integration

BugfixesAndQoL owns the feature and the versioned `AIMarketPendingDemand` record.
`EnableAiMarketOverbuyFix` is host synchronized, enabled by default, and combined
with the host `EnableMod` switch. The client activation switch is irrelevant.
The permanent native relays are installed once; settings change only their data
gates. Disabled, unavailable or editor worlds do not collect provenance or alter
native requests. They save an empty ledger and ignore saved ledger contents.

APIShared provides market decision/recruitment/trade events, mission initialization,
versioned save records, early native restore, resync boundaries, native-site
ownership and optional multiplayer capability agreement. No feature-specific API
or transport is added to APIShared. If one peer cannot run the feature, agreement
leaves it inactive on all peers. An active runtime fault remains fail-closed.
The accepted repair boundary still drops private credit on both peers without
writing native pending cells; no speculative post-repair cap is introduced.

Old standalone-testmod archives are not migrated. Without a current owner record,
only newly observed genuine native producers establish correction authority.
