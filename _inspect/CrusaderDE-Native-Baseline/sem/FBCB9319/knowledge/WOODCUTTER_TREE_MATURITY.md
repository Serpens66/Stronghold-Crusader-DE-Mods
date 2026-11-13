# Woodcutter tree maturity [Scope: Vanilla + mod integration]

Checked on 2026-11-07 against the canonical native image with SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

## Vanilla

- The woodland types are chestnut, oak, pine and birch, in native type order
  1 through 4. All four have wood amounts `1,2,4,9,9,0,0` and starting
  health `2,3,4,5,5,0,0` across growth stages 0 through 6. Stage 3 is the
  first full yield. Natural growth checks current health against maximum
  health. A damaged standing tree cannot resume growing at reduced health.
- Humans and AI use the same woodcutter state machine. The selector at
  `0x107E00` is called from state 0 and the no-target HUD query for state 10.
  Its candidate loop checks stage < 4 and alive state 2 before resource and
  reachability checks. State 10 retries through the usual worker schedule.
- The worker's standing-target check at `0x1086A0` validates the stored
  vegetation serial and the felled field. A false result can still reach
  the resource-only check and allow the worker to collect a felled tree.
  The damage function at `0x107C90` runs after these checks.
- The vegetation manager uses a 0x9C stride and one-based slots 1 through
  3999. Offsets relative to manager plus slot times stride are 0x68 alive,
  0x6A type, 0x70 serial, 0x96 health, 0x98 maximum health, 0x9A felled
  and 0xA4 growth stage. Health and maximum health are signed 16-bit fields.
  The Extender's typed record starts 0x18 bytes into this native view.
- The ordinary saved-game/editor-save path reaches the first internal native
  update in scene 40, where worker dispatch is suppressed. See the raw
  restore analysis in [Worker production](WORKER_PRODUCTION_BONUS.md).

## ExtraFeatures integration [mod integration]

| Path | Displaced RVA and length | Fallthrough | New rejection target |
| --- | --- | --- | --- |
| Candidate search | `0x107EC5`, 20 | `0x107ED9` | `0x107FEA`, skip candidate |
| Arrival | `0x12BFCF`, 18 | `0x12BFE1` | `0x12C03D`, reset worker to state 0 |
| Strike | `0x12C1D8`, 17 | `0x12C1E9` | `0x12C2B3`, reset worker to state 0 |

Search RSI addresses the alive word. Arrival and strike receive the standing
result in EAX, manager in R14 and slot in EBP. The worker resets require
RAX loaded from signed Int32 `0x9302C4`, and strike also requires EDX=EBP.
ESI already holds zero at both resets. The unmodified false branches at
`0x12BFFA` and `0x12C291` retain their resource-only fallbacks. The skip
and reset paths overwrite flags before using them.

The three spans have unique signatures and no direct edges into their interiors.
ExtraFeatures claims them together and publishes a single native transaction.
The installed Extender's woodcutter pickup/dropoff spans `0x12C8D6` and
`0x12CA3B`, APIShared's wood production site `0x12C984`, and the inspected
local Fixes registrations are disjoint. The per-session host choice is captured
after APIShared completes mission start. The native fixture runs both the
copied Vanilla instructions and the production emitter against the same inputs.
Hook availability is local to each process. A one-sided installation failure
can therefore diverge in multiplayer even when the host setting matches.
