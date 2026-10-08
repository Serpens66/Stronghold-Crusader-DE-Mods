# Native compatibility

The runtime's current reference native SHA-256 is
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
The authoritative supported hash, function hashes, bytes, addresses and layouts are
the runtime catalogs. This document explains their purpose; it does not override them.

## Ownership and validation

| Service | Native contract / owner |
|---|---|
| Building repair | Validated native callable functions; no competing detour |
| Gatehouse distance/timing/automation | APIShared owns the coordinated intervals in the shared gatehouse functions |
| AIV build-step observation | APIShared owns the detour at reference RVA `0x51790` |
| HUD and recruitment | Shared managed/native presentation hooks and registration brokers |
| Internal commands and formations | Permanent APIShared command runtime for existing integrations |
| Assassin selection | Script Extender owns its function detour; APIShared uses its audited integration points |

Capability failures remain independent. Hash-bound catalogs, live-byte validation
and the existing unique-pattern resolution policy determine support. A pattern
match is not permission to bypass function, layout, overlap or backend validation.
Game or Extender version numbers alone are not evidence of native compatibility.

## Hook backend contracts

Published hooks remain installed for the process lifetime. Activation changes are
logical gates; inactive callbacks preserve the original behavior. Installation
rollback is limited to a candidate that has not been published.

RedBird inline-hook size is a minimum: actual displacement is instruction-aligned
and can exceed the requested size. Function detours have a separate scheme-dependent
contract. Validate the real backend, complete displaced range, incoming branches,
continuation and ABI before publishing. Context register masks must preserve live
registers; SIMD state and flags need separate verification. Do not assume a context
wrapper preserves the original flags. Keep assembler labels valid and decode emitted
stubs, not only their intended pseudo-code.

The gatehouse centered-distance replacement loads both unit coordinates before
using `cdq`, because `cdq` overwrites EDX and would otherwise destroy the live record
offset. Timing and automation share coordinated ranges; changes must preserve their
non-overlap and the original inactive paths. Existing backend tests execute productive
stubs on isolated buffers, never on another running game's executable pages.

## Updating support

1. Identify the actual installed native image and Extender/backend versions.
2. Audit the complete affected feature flow, including validation, incoming edges,
   displaced instructions, register/flag use and fallback behavior.
3. Compare real managed/interop visibility, field types, offsets and native strides.
   Publicized assemblies are not proof of runtime access.
4. Check overlapping hooks, particularly Script Extender and
   [Fixes](https://gitlab.com/rawra-stronghold-crusader/shcde-fixes).
5. Update catalogs and behavior/byte/backend tests together; retain fail-closed
   behavior for unsupported contracts.
6. Run local integration tests and game acceptance for the affected service.

`tools/Validation/Verify-Interop.ps1` verifies consumed installed layouts;
`tests/APISharedTests` contains native transactions, byte generation and backend
fixtures. UI/session doubles and synthetic memory are not gameplay acceptance.
Relevant gameplay checks include startup cleanup, map transitions, save/load,
host/client synchronization, gatehouse orientations and formations/MoatMove.
