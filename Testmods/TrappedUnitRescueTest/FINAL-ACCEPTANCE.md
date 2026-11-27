# Abschlussabnahme: Einheitenrettung in BugfixesAndQoL

**API-Integration, 27.11.2026:** Die Rettungs-API einschließlich Trampolinprüfung ist in `APIShared/`, Branch `dev/Serpens66`, Commit `421c1ed0457de7a8b7dfcbff160778a4f1f4d070` integriert. Dies ist derselbe getestete Quellstand; 1452 dateibasierte Build-Eingaben stimmen mit dem bisherigen vollständigen Buildnachweis überein. Buildproof, Buildschritte und TRX-Dateien sind unter `.work/a/api-consolidation/001/` erhalten. Die alte Rettungs-Arbeitskopie wurde nach erfolgreicher Integrationskontrolle mit `git worktree remove` vollständig entfernt. Ältere Pfade und Paketidentitäten unten bleiben historische Nachweise. Die Foreign-Control-Arbeitskopie ist ausdrücklich nicht integriert.

Stand:27.11.2026, nach Lauf215. Der vereinbarte Singleplayer-/Editor-Umfang N0–N8 ist abgeschlossen. Dies ist keine Multiplayer- oder Veröffentlichungsfreigabe.

## Verhalten und Besitz

- Eine standardmäßig aktive Hostoption `EnableTrappedUnitRescue`; wirksam mit `EnableMod`. Der Clientmaster steuert diese Hostregel nicht. Die separat entfernbare Fixes-Reiterkorrektur folgt demselben Schalter und wird im Tooltip erwähnt.
- Nachweislich durch beobachtete Bau-/Geländeänderungen feststeckende gewöhnliche Bauern und menschliche Wirtschaftsarbeiter werden regulär entfernt. Waren/Inputs werden nicht erstattet; natürliche Neubesetzung bleibt bedingt und zeitversetzt.
- Soldaten sowie ursprüngliche Geräte/Besatzungen bleiben erhalten; sichere lokale Befreiung und weiterhin gültige Befehlsabsicht werden geprüft. Gesunde/CurrentSafe-/Transient-/Rekrutierungs-/fremde Identitätszustände erhalten keine blinde Entfernung.
- APIShared besitzt gemeinsame Ereignisse/Hooks. Rescue leiht den bestehenden BQoL-Lord-Relocationguard; kein zweiter Relocationhook und kein dauernder Scan aller Einheiten pro Tick. Ereignis-/Queue-/Aktionsbudgets bleiben verbindlich.
- Der ursprüngliche Testmodkern, dessen Einstellungen und EarlyProvider sind entfernt (66 obsolete Dateien/Ressourcen). Die verbleibende separate Automation referenziert die echte Produktionsruntime; notwendige Purefixtures und Audits bleiben erhalten.

## Verifizierte Abnahme

| Anforderung | Autoritative Belege |
|---|---|
| Native Klassifizierung, Entfernung, Belegung, Mitarbeiter-/Lebensgrenzen | Vollständiger featurebezogener Audit in der aktuellen Native-Baseline;24 Typen, Rekrutierungs-/Slotreuseguards; Vanilla-Wissen getrennt von Mod-Nachweisen; Knowledge-Lint148 Dokumente/9 Themen/0 Fehler |
| Funktionale Produktionsintegration |211/840a4a85a511: alle64 Fälle gemeinsam; Bergfriedrotationen/Schritte, Brücken, menschliche/KI-Gruppen, ursprüngliche Geräte/Crew, Quellen, Editor und zivile Entfernungs-/Gegenproben |
| Wirkliche Host-/Client-/Default-/OFF-Grenzen |212/8435768fbe68 und normales214: fünf tatsächliche AIV-Profile, Hostaktivierung und Nativegate1/0/1/0/1 |
| Rückstände, Fairness, sichere Fortsetzung und Save/Load |600 aktive/passive Originale;600 kanonische Pendingeinträge nach echtem Laden, anschließend600 ursprüngliche Ziele; maximal8 allgemeine Aktionen jeTick;211/214 und vorhandene9999er-Offlinegrenzen |
| Fixes-/Hookverträge |27 Prüfgruppen (25 Purefixtures und2 Maschinenprüfgruppen),263 ausführbare Worker-Guard-Prüfungen,116 Scannerprüfungen,18 produktionsgebundene Decoder-Gegenproben; relevante IL-/Helfer-/Live-Stubverträge statt pauschalem Fixes-Dateihash |
| Korrigiertes SDK | Interner Trampolinprüffehler behoben: bekannte Originalinstruktionen inklusive Rücksprung deterministisch wie RedBird/Iced neu encodieren und vollständig vergleichen.37 gezielte Prüfungen einschließlich echter Backend-Prologe und vollständiger offizieller APIShared-Build; tatsächliches Notification-READY213/214/215 |
| Effizienz im vereinbarten Profil |213/0ad2d176595b:9871 Originale,1000 Walker,8 ABAB-Fenster,4 eigene Ladevorgänge,5 genaue Ready-Handoffs,4 Gruppenendpunktprüfungen; aktuelle181-Prüfungen-Logqualifikation mit ausdrücklich ausgewiesener Diagnosegrenze |
| Normales Endpaket mit relevanten Begleitmods |214/0714d5b63904:14 Fälle mit Fixes/Pathfinding/CastlePlanner,0 Error/Fatal, Notification und PitchLine READY |
| Standalone mit harten Abhängigkeiten |215/29cd44bd7f5b:4 Fälle nur mit Extender/APIShared/BQoL plus Automation; Fixes ausdrücklich abwesend,0 Error/Fatal, Notification und PitchLine READY |
| Artefakt- und Rücksicherungsgrenzen | Normale Main-/Acceptance-Builds und NoDevGate bestanden; keine NoJesters-/Capture-/DevDiag-Helfertypen, kein zweiter Runtime-/Scannerbesitzer. Alle Spieltransaktionen restauriert, Testdesktops geschlossen, EingabedesktopDefault |

[Ausführliche Laufnachweise](SP-ACCEPTANCE.md), [Performancegrenzen](PERFORMANCE-ACCEPTANCE.md), [aktueller Plan](RESCUE-PLAN.md) und [Produktions-Nativevertrag](../../BugfixesAndQoL/src/TrappedUnitRescue/UpdateToNewDLL.md).

## Performancegrenzen

Die künstliche Jester-freie Diagnosewelt ist kein allgemeiner Normalwelt-/FPS-Nachweis. Ein vollständig verifizierter früher PerfProbe-Monitorwrapper-Konflikt deaktiviert dort die unabhängige Pitch-/Grabenlinien-Funktion; der aktuelle Logprüfer weist genau diese frühe Diagnosegrenze aus und lehnt andere, spätere oder veränderte Fehler ab. Die normalen Endläufe214/215 bestätigen die normale Kombination ohne PerfProbe und ohne diesen Fehler.

OFF/ON-Median der beiden Fenstermittel: Ruhe5.011/4.8025ms, Bewegung5.3465/5.3395ms. Zeitliche Schwankungen verhindern eine kausale Verbesserungs-/reine Overheadbehauptung. Alle3 Aufrufe über50ms lagen im letzten ON-Bewegungsfenster (max71.220ms), keiner über100ms; die Ursache bleibt unzugeordnet. In den Messfenstern gab es keine eigenen Rettungs-/Entfernungsaktionen. Dichte tatsächliche Rettungsarbeit ist durch die getrennten600er-Fälle abgesichert.

## Quellen, Pakete und verbleibende Grenzen

BQoL-Version1.0.249 benötigt mindestens APIShared0.78. Der geprüfte SDK-Quellstand ist jetzt im kanonischen Checkout `APIShared/` auf `dev/Serpens66` integriert (Commit und gesicherte Buildnachweise siehe Integrationsvermerk oben). Die folgenden Paketidentitäten gehören zur ursprünglichen Spielabnahme.

Normale DLL-Identitäten (vollständige SHA256/Paketpfade im [Endnachweis](../../.work/a/rescue-sp/001/n8-final-normal-summary.json)):

| Paket | MVID |
|---|---|
| BQoL normal |3248c996-023d-4779-8d28-e5dcd98f9884 |
| Acceptance normal |6caae01c-6d76-41da-b4a7-2c7e547845e6 |
| APIShared0.78 |beabf9e1-d26f-4223-9a0a-a1726aa3eed8 |

Keine echte MP-Rettung freigegeben. Kein neuer Vollscan für bereits zuvor feststeckende Einheiten alter Saves ohne kausalen Vorherbeleg. Unbekannte relevante Native-/Fixes-Verträge bleiben fail-closed; ausgelassene Aux-Kettenvarianten werden nicht als bestanden ausgegeben. Warenverlust und bedingter Bevölkerungsausgleich sind akzeptierte Produkteigenschaften.

Kein Commit, Push, Release oder dauerhafter Paket-/Settingswechsel wurde durch diese Abnahme vorgenommen. README blieb unverändert.
