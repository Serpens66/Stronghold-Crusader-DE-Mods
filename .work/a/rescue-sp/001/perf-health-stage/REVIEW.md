# First performance army HP mismatch: bounded diagnostic stage

Prepared2026-11-26 for actual143/237aa811c0594b3b80f2da270b0cb052. One staged source plus health.patch/files.json; active code, runner, validator and package unchanged. No build/game/install/save load performed.

## What143 actually establishes

The run saved the initialized4000 population, loaded the same checkpoint OFF, passed loaded and256-tick warmup boundaries, completed and qualified its first3600-tick OFF idle window, and passed the pre-walking boundary. OFF walking's3600 actual native calls and close marker then completed at08:22:27.600 (mean6.659ms/max15.625ms/no>50ms), but the subsequent whole-population boundary threw the generic HP equality message. No walking qualification line, no ON windows and no complete ABAB comparison follow. The first OFF-idle window alone had max13.705ms and no>50ms. Restoration was reported by the parent. This stage does not relabel143 as a pass or attribute its failure to rescue/damage/any mod.

The log does not identify the offending actor, expected/actual HP or HP direction. Damage and healing cannot be distinguished. Resolve already checks current actual ID/global/type/owner, and exact live tribe plus native93C/global checks precede the HP equality. Ordinary reuse of the numeric ID with a different global/type/owner therefore would have failed an earlier guard, not this one. This refutes that simple aliasing explanation; it does not prove that arbitrary external memory corruption is impossible.

Spawn records ID/global/type/tribe/source coordinates but intentionally does not freeze immediate constructor HP. After32 real native ticks, remember:true requires genuinely initialized idle flags0/SS8/Current==Next before capturing each actual current HP. Its identity hash and real common Save are reused. The passed loaded/warmup/idle/pre-walk boundaries make an already-wrong remembered HP or immediate save-loss explanation inconsistent with those observations. A later healing/damage/initialization writer remains unobserved: no cause is selected from the generic message.

Deterministic start verifies human1/AI2 Rat, pinned original Rat AIC/AIV, peace60/prebuild0 and active initial peace. Existing walking lanes are flat, ordinary, initially free and connected. Higher-tier idle actors additionally avoid known Keep rectangles+16 policy and declared active AIV tiles. These are setup facts, not a proof that no later health writer can run: there is no specific wildlife absence/across-window damage-source invariant, and the Keep/AIV exclusions explicitly make no future-construction guarantee. The current map's animals, any actual attack or a healing source are not identified by143. No native claim about peace protection against arbitrary actors is inferred.

## Proposed source change

The existing paused PerformanceBoundary loop gets a single conditional after successful Resolve and BEFORE existing lifetime/HP guards. On a mismatching saved HP and only once per entire four-load proof, log:

- Actual run/session/loaded session/enabled state and current native tick availability/value.
- ID/captured and actual global/type/owner/tribe, saved and actual HP plus signed delta, actual life.
- Current/accepted-next XY and packed tile, flags/substep/AI, requested goal XY and immutable saved-source XY.
- Walking-cohort membership, current and next raw terrain flags/ordinary/building/vegetation/height.

All HP and existing lifetime/tribe/ground/occupancy/identity checks remain. An HP0 mismatch can now be logged before the existing earlier lifetime rejection as well. Resolve still refuses a changed identity first; the diagnostic never labels a recycled unit as the original actor. The optional terrain read catches diagnostic failures and records unavailable instead of hiding all scalar evidence. At most one witness is emitted; it is not a new sampler, permanent unit loop, native hook or gameplay mutation. No observations execute inside native measured windows: only the already present paused boundary gains this conditional. Normal successful measurement retains the explicitly passive no-extra-UnitMove-demand profile. Existing validator and historical guarantees are untouched.

## Existing event option inspected, not enabled

Actual installed non-publicized SHCDESE.dll metadata confirms public static UnitR3EventHooks.OnUnitTakeMeleeDamage and OnUnitTakeProjectileDamageEx, and public actor/target/damage fields. The local matching detour sources show melee Pre, original unless vetoed, then Post; projectile Ex is a Pre-only context site whose mutable Damage is written back. The older projectile-touch event explicitly does not prove actual damage. Melee Post's event arguments are built from original call arguments, so an observer cannot blindly assume they identify the effective modified invocation or actual HP loss. Neither event is a universal HP-change/healing stream. APIShared.Health offers initialization reasons/modifier/legacy-basis contracts, not a general every-HP-write event.

If the boundary witness remains insufficient, the narrow next option is a separately named diagnostic run with disposable existing SE subscriptions, exact current actor ID/global/type/owner filtering and bounded records of both victim and attacker identities/HP around appropriate phases. Never modify args/SkipOriginal/ReturnValue, never install another hook, dispose on load/end/finally. Subscriptions alter diagnostic observation work and cannot silently be mixed into the passive baseline. No such subscription is added in this stage. Native causal semantics/coverage must be audited before treating an event as proof of every health writer; source/metadata contract review alone is not that proof.

## Verification and acceptance

Roslyn syntax accepted proposed source with and without SERP_DEVDIAG; CRLF and exact unchanged existing HP guard checked. Original/stage SHA256 use the shared .NET helper. No semantic compilation or actual diagnostic witness exists yet. Parent should recheck original hash, inspect/apply during its source slot and compile against selected SDK; then use an explicitly owned controlled game if needed. The archived143 common save has not been loaded by this task. Only subsequent actual primitives can justify a targeted environmental/setup correction; no guards were lowered to make the run pass.
