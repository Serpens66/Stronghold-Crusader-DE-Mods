# Modoptionen und Performance — 2026-10-08

## Implementierung und Nachweisgrenzen

BugfixesAndQoL 1.0.179, keine Versionsänderung. Keine zusätzlichen nativen Hooks,
keine APIShared-Interfaceänderung, kein Hookteardown bei Optionen oder Kartenwechsel.
Formations-/Einheitbefehlsänderungen im Workspace gehören zu anderen Arbeiten.

PlaguePopularityFix: keine eigene OnTick-Verarbeitung. Ereignisgestützte Erfassung,
indexierte Slotzuordnung, spielerbezogene Liveness-Prüfung bei Popularitätsberechnung,
vollständiger Abgleich beim Speichern. NeedsInit/IsAlive und Global-ID bleiben
maßgeblich; ToDelete zählt bereits vor dem Delete-Post nicht mehr. Herdenaufteilung,
Source-Owner und ManagedPlayerIds bleiben im bestehenden MessagePack-v1-Format.
Deaktivierte Partien erfassen weiterhin seltene Herdenerzeugungen und Slotlöschungen,
damit spätere Aktivierung beim Laden funktioniert. Keine Korrektur/Watchdogs im
deaktivierten Popularitätscallback. Diagnose-Watchdogs sind nicht gespeicherte,
einmalige 3000-ms-Simulationstimer; Reset/Disable/erfolgreiche Korrektur beendet sie.
Generations- und Revisionsprüfungen verwerfen veraltete Callback-Zustände.

HUD: atomare kombinierte Aktivitätszustände; Cleanup auf dem Render-/Unity-Hauptthread.
Deaktivierte Countdown-/Fremdtruppenanzeigen führen nach Cleanup keine wiederholten
Abfragen/Updates aus. Fremdtruppen/Lebensanzeige aktiv weiterhin alle 0,1 Sekunden;
Countdown aktiv weiterhin pro Renderframe. Zuschauerbestätigung und ausstehende
Lobby-Rückkehr bleiben unabhängig von Statistikabzeichen erhalten.

ModOptions.Tests linkt die produktiven PlaguePopularityFix-, Ledger-, Save-Formatter-
und Countdown-Quellen mit simulierten Publishern/Spiel-Speicheransichten. Die Tests
installieren keine nativen Hooks und beweisen keine Ingame-FPS oder echte HUD-Darstellung.
Geprüft: 8046 Assertions inklusive 1000 gemischter Slot-/Spielerwechsel, aus/an/aus/an,
Save-Rundläufe in beiden Aktivierungsrichtungen, zuletzt leere Herden, terminale
Zustände vor Delete, umgeleitete Delete-Posts, Spielerisolierung, Watchdog-/Logfehler,
veraltete Timer, ungültige Save-Daten und ruhender Countdown bei erhaltenem Aktivtakt.

## Aktivierungsmatrix

Alle Umschaltungen außerhalb einer Partie; danach dieselbe Karte bzw. denselben
Spielstand erneut starten/laden. Statische Einträge sind keine Ingame-Abnahme.

| Mod/Bereich | Bestehender Aktivierungsweg | Nachweis in diesem Umbau | Ingame aus/an/aus/an |
| --- | --- | --- | --- |
| BugfixesAndQoL: Popularitätsfix | SettingChanged, Session-/Save-Publisher, dauerhafte Hooks | produktive Handler mit simulierten Publishern und echten Save-Formattern | offen |
| BugfixesAndQoL: Countdown | klientlokales SettingChanged, statischer Renderpublisher | produktiver Handler; deaktiviert 0 wiederholte Reads/Writes | offen |
| BugfixesAndQoL: Fremdtruppen/HP | Client-Schalter unabhängig vom Host, atomare Oberflächenmaske | Quellregression; aktiver 0,1-s-Takt unverändert | offen |
| BugfixesAndQoL: Statistik/Zuschauer | eigene Teilfunktions-Gates, pending Lobby-Exit | Quellregression; pending Aufgaben erhalten | offen |
| UnitCosts / BuildingCosts | SettingChanged und Wiederherstellen der Vanilla-Datentabellen | Quellprüfung, bestehende Preset-Prüfungen | offen |
| UnitLimit / BuildingLimit | Aktivieren/Deaktivieren logischer Effekte | Quellprüfung, bestehende Preset-Prüfungen | offen |
| ExtraFeatures / ImprovedHunters | SettingChanged, Refresh-/Apply- und Missionpfade | bestehende Aktivierungswege; kein Umbau | offen |
| RandomEvents / StartConditions | Konfiguration und Mission-/Session-Initialisierung | Startwirkung erst in der folgenden Partie beurteilen | offen |
| CastlePlanner / ExtendedData | Missioninitialisierung bzw. SetEnabled und Vanilla-Restore | bestehende Aktivierungswege; kein Umbau | offen |
| APIShared | eigene Capability-Gates und notwendiges Pending-Cleanup | unverändert, kein neues Polling-Event | offen |
| StatsTweaker (fremder Provider) | Provider meldet ggf. Neustartbedarf | bestehende Provider-/Preset-Regressionen; keine Neustartfreiheit zugesichert | offen |
| CheatMod / ExtremePowers / Testmods | nicht Teil der regulären installierten Vergleichskonfiguration | keine Installation oder Änderung durch diesen Umbau | ausgenommen |

## Kontrollierter Ingame-Vergleich (noch offen)

1. FPS-Vergleich vor/nach Umbau bei identischem Save, Kamera/Zoom, Geschwindigkeit,
   Pausezustand und Debuglogging. Lade-/Aufwärmphase auslassen, mehrere gleichlange
   Abschnitte vergleichen; Median und langsame Frames erfassen.
2. Ohne Prozessneustart aus/an/aus/an: Host und Client getrennt, dann Einzeloptionen.
   Save-eigene Modsettings beachten; wirksame Werte nach dem Laden protokollieren.
3. Seuche überlappt für mehrere Spieler, läuft aus; Bericht und tatsächliche
   Popularität prüfen. Ausgeschaltet speichern/eingeschaltet laden und umgekehrt.
4. Fremdtruppen-/Lebensanzeige und Countdown während Pause, nach Save-Laden,
   Kartenwechsel, im Editor und als Zuschauer prüfen; im ausgeschalteten Zustand
   keine hängen gebliebenen Overlays. Bei Statistikwechsel alle Abzeichen entfernen;
   ausstehende Lobby-Rückkehr darf weiterlaufen.
5. Host/Client mit Fixes testen: identische Hostwerte, lokale Clientwerte, keine
   Callbackfehler oder Desyncs. Ein FPS-Effekt ist bis dahin nicht kausal nachgewiesen.

## Abschluss der lokalen Prüfung

Der neue Code wurde vollständig erneut gelesen und gegen Git HEAD verglichen.
Hookadressen, native Erzeugung, Korrekturformel und Save-Formatter sind unverändert.
Keine Änderungen an README, Versionen oder Formations-/Einheitbefehlsimplementierung.
Presets und Save-Einstellungsübernahme schreiben weiterhin über die Property-Setter,
deren SettingChanged-Publisher die neuen effektiven Aktivierungszustände aktualisiert.

Die vollständige kanonische build.bat /nopause wurde nach den Quellprüfungen erhöht
ausgeführt und hat Build und Installation erfolgreich beendet (build.log).
Der erste Lauf scheiterte an einer alten Countdown-Quellassertion; diese wurde auf
den neuen Aktivierungssnapshot-Vertrag aktualisiert und vor dem zweiten Lauf geprüft.
Die zweite vollständige Testfolge bestand einschließlich der 8046 neuen Assertions,
Preset-, Native-, Formations-, Wasserträger-, Werkstatt-/Gerber-, Friedenszeit-,
Shift-, Assassin- und Moat-Regressionen. Runtime-Präflight, workspaceweite permanente
Hooks, echte Assembly-/Interop-Verträge, XAML und CRLF bestanden; git diff --check sauber.
Build: 0 Fehler, 882 Warnungen (insbesondere doppelte Shared-Typen/CS0436 und
Assemblyversionskonflikte/MSB3277); keine Warnungsbereinigung in diesem Umbau.

Lokales Paket und installierte BugfixesAndQoL.dll haben denselben SHA256:
464098A5C3F55E82ACB790F46ABA2AB4F65C71C424C7F61EA695C49172DF3646.
Version bleibt 1.0.179, Mindest-Extender bleibt 2.14.0.

Die globale Modoptionsprüfung fand außerhalb des Umbaus in ExtraFeatures bei
KeepBuildRangeValueText eine fehlende Sync-/Preset-Klassifizierung bzw. Proxyroute.
Die gezielte BugfixesAndQoL-/APIShared-Optionsprüfung bestand. Daraus folgt keine
vollständige Abnahme aller anderen Mods. Der UnitAccess-Prüfer meldet den veralteten,
nicht mehr vorhandenen Testmods/FormationTest.csproj-Inventareintrag jetzt ausdrücklich
als SKIP; vorhandene Testmods und alle produktiven Inventarprojekte bleiben geprüft,
fehlende produktive Projekte bleiben Fehler. Die Ingame-Matrix oben bleibt offen.
