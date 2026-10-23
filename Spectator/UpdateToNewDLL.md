# Native contracts of the test tools

Reference DLL SHA-256: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Managed reference SHA-256: `BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
Script Extender source: commit `5908e1f12437deb7ef5f5b1dc2d64e301178fba3`, tree `c238441ea12db35ee36c5c5668d3c8fce0897295` (2.14.1).

## AI orders

The mod owns no new AI native hook. `TestToolsNativeOrders` resolves the complete
587-byte function at RVA `0xD7580`. Its C ABI is
`void(int attackTargetPlayerId, int orderedAiPlayerId, int requesterPlayerId, int order)`.
All player IDs are one-based. Attack passes the actual attack target and order 1;
defend passes zero as attack target and `AIRequestOrderType.DefendMe`; cancel uses
`ClearOrders`. The attack order integer is the native protocol branch, not a player index.

The reference hash and executable PE section are checked, every reference instruction
is decoded, and the entire live function must equal the reference bytes. A unique
full-function signature scan is restricted to that executable section if the primary
comparison fails. A relocated match is refused because RIP-relative data and calls
have not been revalidated at another origin. Unknown native hashes are refused:
there is deliberately no cross-version structure/layout fallback.

Vanilla flows through `DLL_GameAction` (0x81870), producer 0xD7520 and Chore 113
(0x1A6F0). The test view instead invokes 0xD7580 while holding
`EngineInterface.threadLock`, after checking the session, offline mode and players.
It never changes the native local-player ID or teams. Attack requesters use the
controlled live player, or the ordered AI when spectating. A defense requester's ID
is the explicitly selected protected player.

Acceptance checks at 0x2A500 (defend) and 0x41500 (attack) consider AI existence,
help state and available army strength. The installed Extender detours both checks
and publishes their Pre events. A thread-local, finally-restored context only overrides
the exact own request's readiness check, with `ReturnValue=true` and
`SkipOriginalFunction=true`; no refusal-side speech is emitted first. The untouched
outer function applies the order and, for accepted attacks, the 0x2A580 preparation
chain (siege-building/engineer/ladder regrouping and siege-state reset).

Postconditions read Extender fields `r_AIAllyOrderMode`,
`r_AIRequestedAttackTargetPlayerId` and `r_AIAllyOrderRequesterPlayerId`.
Native target selection 0x2F670 retains an explicit attack target while requester
and target Lords remain valid; 0x3C2E0 dispatches protection through 0x3D020.
Cancellation clears the requested mode. State acceptance is not proof of movement.
Forced acceptance does not bypass life, team, path, troop or objective validity.

## Existing placement and presentation contracts

The original placement behavior remains hash-bound. The native availability hook
owns 0xCC420 (10 displaced bytes, NativeX64 Indirect). Visual hooks own 0x65830,
0x41D60 and 0x6E620; their prologues, exact displacement and installed backend are
validated in `SpectatorPlugin.Native.cs`. Published hooks remain
installed until process exit. RedBird's audited assembly version is 1.5.0.0.

The visual spectator flag is at RVA 0x3666080. Placement also reads the Chore kind
and phase at 0x85F8FF4/0x85F8FEC and its argument buffer at
0x86C132C/0x86C1330/0x86C1334/0x86C1338. Those reads identify the exact pending
building/pitch/wall request; they are not general-purpose addresses. Existing native
request matching and temporary data changes must be audited with the full placement
path before supporting another DLL. These data locations have no independent
semantic fallback and remain disabled behind the native image check.

## Update and acceptance checklist

Re-audit native ABI, all callers/callees, acceptance events, player/AI layouts,
incoming-goods consumers and the placement/visual spans against the installed DLL.
Recheck original and hooked entries against Extender and Fixes. Do not infer
compatibility from a successful build. Static and pure-policy tests are separate
from in-game acceptance: movement, delivery, save/editor behavior and presentation
must be exercised with the real game. Spectator uses the verified development
SDK with additive PlayerPerspectiveAPI notifications (minimum 0.12.2).
# Extremkraefte und direkte Gebaeudebefehle (2026-10-16)

## Vanilla toolbar and amount focus follow-up

The installed managed SHA-256 remains
`BC8B6A395F01D48557DB413600C8DD8D1FDFD3ABDF97BFBBB68A3C56B04FD789`.
`EditorDirector.Start` disables Noesis keyboard input; Vanilla text fields restore
it through `SetNoesisKeyboardState` on focus changes. The amount adapter now owns
that handoff only while focused and releases it on hiding/detach, preserving a
subsequent text field. Input validation remains event-driven.

Vanilla `GameData.setGameState` enables the eight extreme buttons from the local
`extremeCount`. `MainViewModel.ButtonExtremeClick` sends GameAction ExtremePower;
`DLL_GameAction` (0x81870, branch 0x42C) checks the match flag and calls selection
0x105510. That function reads local-player mana/pause state, dispatches Gold via
Chore 119, or arms target mode 5. The target branch in 0x8C5F0 also uses the local
player for range, enemy and pause checks and writes that ID into Chore 119.
Its handler 0x1ABF0 passes the explicit player to dispatcher 0xCD630. The latter
has no spectator veto and uses its player argument for all eight effects.

The test mod intercepts the managed ExtremePower action through APIShared GameActionEvents
GameActionEvents and schedules selection outside the publisher. Vanilla's target
mode, cursor, previews, configured SH1 RTS controls and cancellation stay native.
There is no mod-owned power mouse/keyboard handler or duplicate target algorithm.

## Vanilla power context and gate HUD repair (2026-10-16)

The canonical native hash above remains mandatory. NativePowerAssembly/NativePowerContext
own four permanent, non-overlapping inline spans. RedBird X64InlineHook rounds to
at least 14 bytes; the installed backend is probed before publication. The complete
parent-function baseline xrefs (including jump-table references) have no interior
entry into these spans. The callback wrapper preserves volatile GPR/XMM state;
RAX and flags are separately saved. Callback decisions test fresh flags after the
wrapper. Inactive paths execute all displaced instructions exactly once.

| RVA / span | Function / contract |
|---|---|
| `0x105593` / 14 | Selection: replace the local actor load into RDX only inside our selection call; retain `RCX=RDX*0x583C`. |
| `0x8CAF8` / 14 | Native power-target branch only: retain R10 tile manager; replace R15 actor for native keep/Lord/team/pause checks. |
| `0x8CE20` / 18 | Native accepted target: capture already written tile `0x86C1330`, enqueue the captured AI action once, suppress duplicate Chore, preserve `R8D=2` continuation. |
| `0x105627` / 14 | Native immediate Gold confirmation: own selection captures one action; other selections preserve native Chore 119. |

A process-lived gateway has the exact Vanilla selection prologue (`push rbx`,
32-byte shadow frame, EBX=power), receives actor as its second Int32 argument,
sets RAX=actor and enters `0x105548`. It bypasses only the local-human availability
prefix and first local-identity load; the original cost, pause, selection, audio,
switch table and epilogue remain authoritative. Its initial write is before
publication; no subsequent executable mutation or release occurs.
Selection function `0x105510`, 326 bytes, SHA-256
`003599E68B7384B75515EAE73EA17EBA7D8B58BD34A748EADDE447FE28E5D88D`.
Cancellation is original `0x897F0`, 119 bytes, SHA-256
`5A244DF882A8C45FE8E7E8C15560986570BF2EADD2C898A23B49D54470B75ADA`.
Target mode `0x60AD56C` (4/5), selected power `0x60AD574`, and the native
submission argument at `0x86C1330` are hash-bound Int32 data contracts. Global
player identity `0x88E3D70` is never written. Cancellation calls Vanilla only
while the same session still owns the matching pending power.

All live selection-body bytes `105541..105656`, power-target bytes `8CAD2..8CF15`
and cancellation bytes are checked against the canonical executable PE sections
before installation. Hook-site bytes have independent exact signatures. These
blocks depend on surrounding branch/register/data contracts, so finding identical
bytes at another address is not a valid relocation fallback. Unknown bytes or
image hash disable only the AI-power adapter. ExtremePowers may own the original
selection/dispatcher entry: the gateway avoids its entry hook, metadata supplies
replacement target kind and costs, and the existing effect dispatcher/API remains
authoritative. No competing dispatcher hook or backend initialization is added.
Inactive ExtremePowers readiness retains Vanilla metadata and behavior.

Gate selection records input intent under the engine monitor and waits for a
completed managed PlayState revision. Main-thread rendering acquires one lease
bound to the session, owner, building/global IDs and concrete HUD objects. Only
the lease's own `InBuildingGameAction` scope overrides panel parameters. Full
Vanilla HUD transitions release without restoring obsolete flags; a direct panel
setup invalidates pending intentions before rebuilding. Only the four gate/bridge
button sprites and optional button bindings are restored; never snapshot the
whole visual tree or restore old HUD flags. Without a newer transition, release
routes through the current PlayState's Vanilla HUD method. Placement explicitly
opens the main/editor HUD before selecting a supported build page.
The Extender-registered automation host DataContext is never overwritten: its
visibility binding has an explicit test-model Source, and only its child button
receives the test DataContext. A HUD replacement never restores flags into
its new instance. The test toolbar follows global UI visibility, not a particular
bottom HUD tab. Native building selection and global player identity stay intact.

ExtraFeatures 1.0.114 and 1.0.115 supply the optional existing gate/bridge button host and
viewmodel. The encapsulated internal adapter verifies version/member contracts,
uses `IsManualOnlyBuilding` and `ApplyManualState(player,global,automatic,source)`,
and preserves the provider's native-ready, map-ready, loading, enabled and mode
gates. Only the test viewmodel command changes; the provider retains its sole
resolver and persistence. Unknown versions hide the integration, not direct
gate commands. Re-audit this internal adapter when ExtraFeatures changes.
The activation predicate is internal static bool IsEnabled(bool), resolved with
NonPublic/Static flags and cached once along with the readiness field contracts.
A provider refresh failure latches the optional adapter off and hides its button;
later frames must not throw again or format another stack trace. Regression tests
match the installed access modifier and exercise 1000 frames after a failure.


Validation: production-generator tests execute real installed RedBird hooks on
private native buffers, both inactive/active branches, AI IDs 2/8, all eight
original selection paths, register/flag preservation and duplicate-Chore
suppression. Pure lifecycle and production optional-adapter tests cover delayed
HUD readiness, repeated transitions, rejection and provider state. Real-game
target effects, visual HUD recovery and combined-mod acceptance remain separate.

## Existing native contracts

Referenz bleibt ausschliesslich die installierte DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die folgenden Ausfuehrungsadapter sind Call-only; die vier oben dokumentierten
Kontexthooks ergaenzen die native Kraftbedienung. Globale Spieleridentitaet und
Diplomatie bleiben unveraendert.

| Vertrag | RVA / Laenge | Herleitung und Fehlerverhalten |
|---|---|---|
| Kraftausfuehrung | `0xCD630` / 512 Byte | Vollstaendige Funktion, acht Switchzweige, explizite Argumente `(playerManager, playerId, powerId, packedTile)`. Aufrufer `0x1ABF0` erhaelt Opcode 119 (2/4/2 Byte). Manaformel `(powerId+1)*636`; Aufrufe `0xDD6C0`, `0xE1E70`, `0x1264D0`, `0x7530`. |
| Torhausbefehl | `0xD5810` / 96 Byte | `(playerId, buildingId, state, globalId)`; Global-ID-Veto, Befehl 10/11, Timer und Kopplung `0xC5300`. Der erste Parameter wird in Vanilla nicht gelesen; trotzdem expliziter KI-Besitzer. |
| Brueckenbefehl | `0xD57B0` / 64 Byte | Gleiche Argumente, Global-ID-Veto, Befehlsbyte und Kopplung `0xC5300`. |
| Zielregeln | `0x8C5F0` | Vanilla-Kartenabschluss: Spawnradius im Quadrat `2500`, Ingenieure `12544`; Gegner-Bergfried `<625`; Lordnaehe abhaengig vom unten genannten Flag. Spawnmaske `0x40501481`, Ritter zusaetzlich Bit `0x100`, PCL ungleich null; pausierter Spieler gesperrt. |
| Lordbezogene Spawnregel | `0x37EF96C` / Int32 | Nativer aktueller Partie-Schalter, gelesen im Zielabschluss. Hashgebundenes Datenziel ohne semantisch ausreichenden Pattern-Fallback. |

Die dateibasierten Vollfunktionen werden unter dem Referenzhash aus einer
ausfuehrbaren PE-Sektion gelesen und vollstaendig mit Iced dekodiert. Vor jedem
Aufruf werden 20 Live-Einstiegsbytes verglichen. Das ist eine Einstiegskontrolle,
kein Anspruch auf eine unveraenderte gesamte Live-Funktion: bestehende interne
APIShared-Torautomationshooks (unter anderem `0xD5835` und `0xC5300`) bleiben
ausdruecklich im Ausfuehrungsweg. Animation/Topologie verbleiben in Vanilla
(`0xA5320`, `0xA57F0`, `0xA5C80`, `0xA5E20`, `0x64460`, `0x645C0`).
Ein veraenderter Dispatcher-Einstieg ist nur mit dem nachgewiesenen bestehenden
ExtremePowers-Detour zulaessig: initialisierte API, verwurzelter Runtime-Detour,
IsApplied, identische Method-/Callback-Adressen und passende Sprungverfolgung.
Es wird weder ein Trampolin umgangen noch ein zweiter Dispatcher installiert.
Unbekannte Einstiegseigentuemer sperren den jeweiligen Aufruf und werden begrenzt
protokolliert. Kein Pattern-Fallback auf fremde DLLs: eine passende Funktion
wuerde die Datenziele und Zielregeln nicht beweisen.

Mana wird ueber `GamePlayerResources.r_ExtremePowersMana` als UInt32 gesichert,
nur im eigenen synchronen Aufruf unter dem Vanilla-Monitor angehoben und in
finally bitgenau wiederhergestellt. ExtremePowers-Ersatzcallbacks laufen ueber
deren bestehende TryExecuteReplacement-API, geaenderte Vanilla-Wirkungen ueber
den laufenden nativen Dispatcher. Zielart, Registrierung und Kosten werden vor
Ausfuehrung erneut verglichen. Die optionale API wird nie initialisiert oder
umkonfiguriert. Unit-Zieltypen bleiben entsprechend dem vorhandenen Backend
gesperrt. Der globale Schalter nutzt den oeffentlichen Extender-Setter, damit
CheatMods bestehender Setter-Hook seine menschliche Aufladung beobachtet.

Gebaeude-IDs bleiben 1-basiert. Bruecken verwenden die bestaetigten
Interop-Felder `N00008642_2` (Befehl, 0x293) und Low-Byte von `N0000182B`
(physisch, 0x294), Tore `r_GateState2` und `r_GateState` (0x2A3/0x2A2).
Das Panel verwendet Vanillas UI-Modi 36/37, nicht gleichnamige Struktur-IDs.
Vanillas lokale Befehls- und optimistische PlayState-Aktualisierung wird nur
fuer die eigene gepruefte Gebaeudeauswahl ersetzt. Andere Reparaturaktionen
werden dort nicht angeboten. Folgezustaende werden zeitlich begrenzt geloggt;
ein UI-Klick oder ein void-Ruecksprung beweist keinen abgeschlossenen Wegumbau.

Updatepruefung: echte Assembly-/Extender-Sichtbarkeit, Enumwerte, Ressourcen- und
Gebaeudefeldlayout, alle drei Vollfunktionen/Calls und beide Ziel-/Auswahlpfade
erneut pruefen. Danach Vanilla, CheatMod, ExtremePowers und beide zusammen im
Spiel testen, einschliesslich geaenderter Kosten und Gold-Ersatz mit Kartenziel.


## Gemeinsame Zuschauer-Auswahl (2026-10-21)

Spectator owns its perspective runtime, report hooks, ally hooks, statistics and
defeat promotion. No BugfixesAndQoL reflection adapter or presence gate exists.
The user will remove overlapping spectator features before testing both mods together;
foreign troop HUD, surrender and lobby return remain with BugfixesAndQoL.

PlayerSelectionController is the shared cancellation/publication boundary. APIShared
SubscribeChanges reports confirmed external transitions; the initial snapshot and
GetViewedPlayerId retain their existing API contract. Main-thread presentation is
queued and session checked; unchanged IDs do not rebuild the toolbar. Native mutation
remains forbidden in real multiplayer, independently of read-only spectator rights.

Only the new save entry Spectator_Serp-SaveIdentity is written. A valid old
SpectatorPerspectiveTest-SaveIdentity entry may be read; ambiguous human registrations
never become spectator saves merely because a native view slot looks synthetic.

The fresh power-selection gateway is written once before exposure and flushed once.
It is retained with the published hooks for process lifetime, including partial failures.
The workspace validator allows only that specific allocation/publication sequence;
NativePowerContext fixtures still audit every inline span, incoming edge and generated
register/branch contract. No existing game instructions are rewritten on activation.

StatisticsPreviewController owns the current preview identity and generation. A real
Vanilla end notification revokes that ownership without rewriting the new end screen.
Settings use the shared preset base: local tools activation is PresetLocal, perspective,
statistics and eliminated-player permissions are SyncHostOnly. No foreign settings migrate.

The development package is not on the main-mod release whitelist. Build via build.bat
with the verified SDK selected through SHCDE_API_SHARED_DIR; /noinstall preserves the
current game installation. Automated tests do not establish in-game delivery, effects,
layout at different resolutions or interoperability after the user cleans BugfixesAndQoL.
