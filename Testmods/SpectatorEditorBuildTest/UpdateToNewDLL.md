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
validated in `SpectatorEditorBuildTestPlugin.Native.cs`. Published hooks remain
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
must be exercised with the real game. No APIShared source change is required;
this task targets unchanged main 0.9.2.
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

The test mod therefore intercepts only the managed ExtremePower action through
the existing APIShared main 0.9.2 GameActionEvents broker. It vetoes local selection
and schedules the existing explicit-AI targeting/dispatcher path outside the
publisher. No new native hook or global-player mutation is needed. Session,
actor and cancellation generation are checked again before execution.
Vanilla button bindings are saved and restored, while the test context supplies
button eligibility and an AI/unlimited label. ExtremePowers' sprite/hover and
dispatcher hooks remain installed; the match setter continues to notify CheatMod.
Recheck keyboard ownership, button bindings, command payload and competing HUD
updates after managed or optional-mod updates. Automated tests do not establish
in-game visual rendering or the eventual effect of a native void call.

## Existing native contracts

Referenz bleibt ausschliesslich die installierte DLL mit SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Alle neuen Adapter sind Call-only; sie installieren keinen nativen Hook und aendern
weder globale Spieleridentitaet noch Diplomatie oder native Auswahl-IDs.

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

