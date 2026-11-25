# Offline-Prüfung der Einheitenrettung

Stand: 25.11.2026. Auftrag: relevante Prüfungen ohne Installation und ohne Ingame-Tests. Ergebnis: Die unten aufgeführten abschließenden Läufe bestehen. Das ist eine Prüfung des vorhandenen Prototyps und seiner Integrationsgrenzen, keine Abnahme der noch nicht implementierten allgemeinen Rettung.

## Quellstand und Umfang

- Mod-Repository: `develop`, HEAD `2617f87e40ae070933e703a0c40e55d084afca18`, vorhandener Arbeitsstand; keine Commits erstellt. Fremde Arbeitsänderungen bleiben erhalten.
- Pathfinding: Quellversion `0.1.16`, APIShared-Mindestversion `0.65.0`. Der vollständige Offline-Build verwendet das geprüfte lokale APIShared-Paket, SDK-Commit `681f57f36cdd349bc540a52651d81b3effad04a6`.
- Installierte Referenzen des Testmod-Builds: Script Extender `2.14.4`, Fixes `1.31.0`, APIShared `0.73.3`. Keine Änderung dieser Pakete.
- Kanonischer nativer DLL-Hash unverändert: `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
- Kompakte Laufnachweise: [Ergebnis](../../.work/a/rescue-offline/001/result.json), [Logverzeichnis](../../.work/a/rescue-offline/001), [Installationskontrolle](../../.work/a/rescue-offline/001/installed-verification.json). Buildprotokoll Pathfinding: [working.build.json](../../.release-output/builds/Pathfinding/working.build.json); dieser wiederverwendete Pfad bezeichnet jeweils den letzten Build, die Identität dieses Laufs steht im Ergebnis.

## Durchgeführte Prüfungen

| Prüfung | Abschließendes Ergebnis | Nachweis im Logverzeichnis |
|---|---|---|
| Testmod-Build `/nopause /noinstall` | PASS; 1.538 Policy-, 263 native Worker-Guard- und 6 AIV-Receipt-Prüfungen, zusammen 1.807 Einzelprüfungen | `rescue-build-final.log` |
| Fixes-Kompatibilität | PASS; relevante IL-Verträge akzeptiert, unabhängige Versions-/Featureänderung akzeptiert, manipulierte Root-/Helper-/verzögerte Verträge abgelehnt | im selben Buildlog |
| Temporäre Fixes-Korrektur | PASS; Cecil-Korrektur, Idempotenz, serialisierte Closure, unbekannter Vertrag unverändert, Originaldatei und nativer Emitter unverändert | im selben Buildlog |
| Testmod-Quellgrenzen | PASS; JSON, Lifecycle, dauerhafte Hooks, Referenzen, CRLF, UnitAccess/BuildingAccess, XAML-Wurzeln; 21 Tooltip-Popups | `rescue-boundaries-final.log` |
| Locale-Parität | PASS; 21 Sprachen mit jeweils denselben 4 eindeutigen, nichtleeren Schlüsseln | `locales.log` |
| NativePatchGuard mit installiertem RedBird | PASS; fremde Patches innerhalb/außerhalb der Spanne, späteres Überschreiben, fehlgeschlagene Vorbereitung | `native-patch-guard.log` |
| APIShared: AIV-Abschluss, Missions-Lifecycle, echte Managed-Member-Verträge, Hook-Veröffentlichung und Fremdpatch-Guards | 43 PASS, 0 Fehler, 0 übersprungen | `api-contracts-final.log`, `api-final-results/rescue-api.trx` |
| APIShared Core: Kachelregeln, Suchereignisse, Engine-Information, versionierte Savedaten | 43 PASS, 0 Fehler, 0 übersprungen | `api-route-core.log`, `core-results/route-core.trx` |
| APIShared: Bau-Wrapper, native HumanConstruction-Grenzen, Assassin-Builder-Grenzen | 19 PASS, 0 Fehler, 0 übersprungen | `api-construction-boundaries.log`, `construction-results/construction-boundaries.trx` |
| HumanConstruction-Ledger | 62 Verhaltensprüfungen PASS | `human-construction-ledger.log` |
| Vollständiger Pathfinding-Build aus dem Arbeitsstand | PASS; sämtliche vom Standardtreiber ausgewählten Offline-Suiten und Paketprüfung; ausdrücklich `Installation skipped` | `pathfinding-build-final.log` |

Der Pathfinding-Lauf umfasst Engine/Suche, Cache-/Oberflächen-/Save-/Wartefälle, CommandDispatch, Formationen, Speech, Shift-Queue, Issue6, AssassinPathfinding, FriendlyMoatMovement, DrawbridgeDeck, Interop, CommandSplit, GateClimb, GatehouseDiagnostics, GateRouting samt Migration an/aus und Movement. Die nativen Anchor-/Assassin-Kerne wurden gebaut; 25 Paketassets stimmen mit den Quellen überein. Testassertionen und Parameterkombinationen sind keine entsprechend große Zahl unabhängiger Spielszenarien.

Native Offline-Tests führen Maschinenkode ausschließlich auf privaten Testseiten beziehungsweise isolierte Wrapper aus. Im Spiel wurde kein Hook veröffentlicht. Reale Map-/Einheiten-/Netzwerkzustände werden dadurch nicht geprüft.

## Korrektur und verworfene Laufversuche

Die erste zusätzliche Tooltip-Prüfung scheiterte: Vier ausdrücklich erzeugte `ToolTip`-Objekte hatten keine ausdrückliche lokale Stilzuweisung. Der vorhandene Stil ist jetzt als `ModPopupStyle` referenzierbar; diese vier Popups referenzieren ihn. Ein davon abgeleiteter impliziter Stil erhält die übrigen Text-Tooltips. Bindings, Presets, Rollen und Rettungslogik bleiben gleich. Quellprüfung und Testmod-Build bestehen anschließend. Das belegt keine tatsächliche Noesis-Darstellung; ein späterer Hover-/Skalierungstest bleibt erforderlich.

Zwei VSTest-Läufe scheiterten am Testhost-IPC-Start innerhalb der Sandbox, bevor Tests ausgeführt wurden. Dieselben Tests liefen außerhalb der Sandbox erfolgreich; danach bestand auch der vollständige Pathfinding-Build. Ein erster Pathfinding-Aufruf verwendete irrtümlich das installierte API-Verzeichnis als SDK-Paketwurzel; der Paketvertrag verlangt ein verifiziertes SDK mit Repository-/Buildnachweis. Der abschließende Lauf verwendet das lokale Workspace-SDK. Diese Versuche stehen als fehlgeschlagene Versuche in den Nachweisen und werden nicht als bestandene Tests gezählt.

Vorhandene Warnungen über Mono.Cecil-Referenzauflösung und Test-Fixturefelder verhindern den Testmod-Build nicht. Einzelne Restores melden `NU1900`, weil der NuGet-Sicherheitsfeed nicht erreichbar ist: Die Builds bestehen, eine vollständige Online-Sicherheitsprüfung der Pakete ist damit nicht belegt. Absichtliche Error-/MOD_FAILURE-Logs in negativen Hook-Tests gehören zum geprüften Fehlerpfad.

## P1: offline geprüfte Beobachtungsgrenzen

| Quelle | Festgestellter Vertrag | Folgerung |
|---|---|---|
| APIShared `AivBuildStepCapability` | Original genau einmal; Abschluss in `finally` einschließlich Fehlerflag | Für den bestehenden AIV-Piloten geprüft; kein allgemeiner Bauabschluss aller Quellen |
| Extender `BulkAIDetours` / KI-Mauerbau | Pre; Original in `finally`; Post danach, bei Originalexception nicht garantiert; Post verwendet ursprüngliche Argumente | Vorher-/Nachher-Paar nicht ungeprüft als Transaktion verwenden; Fixes-Pre-Rettung berücksichtigen |
| Extender `BulkBuildingDetours` / Menschenbau und Mauern | Spiegel-Originale können vor dem normalen Pre laufen; effektive Pre-Parameter gehen ins Original, Post enthält Originalargumente; kein garantierter Post bei Fehler | Einfaches Pre/Post-Abonnement erfasst nicht zuverlässig jede wirksame Bauänderung |
| APIShared `HumanConstructionRuntime` | Interne Wrapper erfassen finale Original-/Mirror-Closures; öffentliche Kosten-/Zahlungsschnittstelle ist kein allgemeiner Rettungsbroker | Vor Wiederverwendung geeigneten funktionalen Vertrag definieren |
| Extender `BulkMapEditorDetours` | Gelände-/Höhenereignisse vorhanden; Spiegeländerungen können vorher laufen; übersprungenes/fehlgeschlagenes Original hat keinen garantierten Abschluss | Editor nicht allein aufgrund vorhandener Eventnamen freigeben |
| Pathfinding `NativeMovementRecovery` | Topologie-/Moat-/Maskenbeobachter invalidieren nach Schreiboperationen, teilweise während unfertiger Änderungen | Vorhandene Hookbesitzer nutzen; endgültigen Grabenabschluss und alten Fluchtbereich zusätzlich belegen |
| Extender UnitMove / Pathfinding Bewegungskontext | Abgefangene Aufträge lassen sich einer Einheit zuordnen; keine Garantie für jede native Suche oder jeden physischen Schritt | Keine universelle Feststeckerkennung daraus ableiten |
| APIShared `RouteSearchEvents` | Zusatzsuchen ohne allgemeine UnitId-/Vanilla-Schritt-Abdeckung; keine Weltmutation im Callback | Nur innerhalb des dokumentierten Suchvertrags verwenden |

Die vollständige native Erzeuger-/Leser-/Fehlerpfadanalyse für zusätzliche automatische Quellen ist damit **noch nicht abgeschlossen**. P1 bleibt teilweise geprüft. Insbesondere alte erreichbare Ausweichfelder, ausschließlich betroffene Next-Belegung, laufende Schritte und endgültiger Bauabschluss benötigen weitere Verträge vor Implementierung.

## Kompatibilitätsbefunde und verbleibende Arbeit

1. **Fixes 1.31.0:** Im aktuellen lokalen Quellstand und der installierten Assembly ist die veränderliche `DataScanner`-Capture des Reiter-Installers weiterhin vorhanden. Die temporäre Korrektur erkennt den unterstützten defekten Vertrag; unbekannte Änderungen werden weiterhin abgelehnt. Der tatsächlich veröffentlichte CALL und die Spielwirkung von 1.31.0 wurden hier nicht getestet. Die echten 1.30.0-Belege vom 17.11. bleiben historische Belege und werden nicht auf 1.31.0 umetikettiert. Vor Entfernen der Korrektur Installer und Spielwirkung erneut prüfen.
2. **Baseline-Provenienz:** Native DLL und bisherige Hashbindung stimmen. Die Interop-Provenienz im vorhandenen Baseline-Dataset nennt noch Extender 2.14.3; der aktuelle Quellstand ist 2.14.4 (`7c3a252794974ee7af5cca02f446ba994fb9d22d`, Tree `267778591884769c54b60d84123647a825112a81`). Im eingegrenzten Vergleich wird die gefundene Tribe-Feldänderung von den Rettungs-/Pathfinding-Pfaden nicht konsumiert. Das beweist keinen allgemeinen Defekt, ersetzt aber vor weiteren nativen Integrationen keine Aktualisierung beziehungsweise vollständige Validierung der relevanten Dataset-Provenienz. Die globale Baseline wurde nicht auf Basis einer Teilprüfung umgeschrieben.
3. **Noch zu implementieren:** allgemeine budgetierte Kandidatenverwaltung, weitere Bau-/Geländequellen, Next-/Schrittzustände, automatische Arbeits-/Besatzungspfade und zusätzliche Fehlersignale gemäß [Arbeitsplan](RESCUE-PLAN.md). Nicht vorhandene Implementierungen können keine bestandenen Implementierungstests haben.
4. **Spätere Spielabnahmen:** physische Bewegung, Ladung/Arbeitsfortsetzung, Belegung, KI-Gruppen, CastlePlanner-Keep-Rotation, Save/Reload, Editor, echter Multiplayer und reale Last mit 10.000 Einheiten/800×800. Offline-Parität und lokale Mikrotests sind kein FPS- oder MP-Nachweis. Die offenen Grabenarbeiter-/Ochsenfälle bleiben offen.

Die Installation wurde nicht ausgeführt und das Spiel nicht gestartet. Die 324 zu Beginn erfassten Dateien aus Extender, APIShared, Fixes und Patchern sind hashgleich; in diesen Verzeichnissen fehlen keine Dateien und kamen keine hinzu. Pathfinding war im anfänglichen Hashsnapshot nicht enthalten: Seine 29 installierten Dateien haben sämtlich Schreib-/Erstellungszeiten vor diesem Auftrag, und der Build bestätigt `/noinstall`; ein Vorher-/Nachher-Hashbeweis für dieses zusätzliche Verzeichnis wird nicht behauptet.

## Wiederholung

Die vollständigen Logs und TRX-Dateien bleiben der genaue Nachweis der ausgewählten Tests. Wesentliche Einstiege:

    Testmods\TrappedUnitRescueTest\build.bat /nopause /noinstall
    Shared\Tools\Validation\Test-SharedBoundaries.ps1 -ModDirectory Testmods/TrappedUnitRescueTest
    dotnet run --project Shared/Tests/NativePatchGuard.Tests/Shared.NativePatchGuard.Tests.csproj --configuration Release
    dotnet run --project APIShared/tests/HumanConstruction.Tests/HumanConstruction.Tests.csproj --configuration Release

Für den Pathfinding-Build in PowerShell `SHCDE_API_SHARED_DIR` auf das bereits geprüfte `APIShared/BepInEx/plugins/APIShared_Serp` im Workspace setzen, dann:

    Pathfinding\build.bat /source:working /apishared:package /noinstall /nopause

SDK, Branch-/Arbeitsstand, installierte Referenzen und Testhost-Ausführungsumgebung bei jeder Wiederholung erneut bestimmen. Ein späterer Build kann diese Momentaufnahme überholen.
