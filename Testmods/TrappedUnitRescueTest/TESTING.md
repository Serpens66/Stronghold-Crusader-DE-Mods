# Einheitenrettung testen

Fortlaufender Plan: [RESCUE-PLAN.md](RESCUE-PLAN.md). Aktueller Entwicklungsstand: [IMPLEMENTATION.md](IMPLEMENTATION.md). Vollständige offene Spielabnahme: [INGAME-PENDING.md](INGAME-PENDING.md).

Der Testmod bleibt ein isolierter Versuchsstand; die experimentelle Einstellung ist standardmäßig aus. Die Quellen vom 25.11.2026 erweitern den älteren AIV-Brückenpiloten auf gemeinsame Bau-/Geländeereignisse, lokale Current-/Next-Erkennung, qualifizierte Arbeiter-/Befehlsfortsetzung und einen getrennten neuen Fluchtauftrag für untätige Einheiten auf frischem Moat. Sie brauchen das gemeinsam geprüfte Entwicklungs-SDK APIShared 0.74.0. Der aktuelle Singleplayerstand samt verbleibenden Grenzen steht in [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md); ältere Spielversuche des früheren Prototyps sind keine pauschale Abnahme des neuen Systems.

Erfassung und Rettung sind getrennt: Originale einschließlich Fixes laufen zunächst unverändert, der Coordinator wartet auf den vollständigen äußeren Batch und versucht eigene Rettung erst im zugelassenen Simulationstick. Die Fixes-Unterdrückung bleibt auf eigene synchrone Rettungs-/Fortsetzungsaufrufe begrenzt und wird in finally ausgeschaltet. Die Fixture hat ihre gesonderte kontrollierte Testisolation.

Neue Vorherdaten werden nur an tatsächlichen lokalen Änderungsereignissen erfasst. Bestehende Fallen ohne eigene kausale Daten werden nicht durch permanente Einheiten-/Kartenscans gesucht. Qualifizierte Arbeiterreisen, normale Schritte, begrenzte Queues und eigene Savedaten sind implementiert; Kontaktarbeit, legale Sonderbewegungen, echter Multiplayer und bisher nicht abgenommene Zustände sind damit nicht pauschal freigegeben. Die genaue Abdeckung und laufenden Abschlussarbeiten stehen in IMPLEMENTATION.md.

## Bauen und starten

`build.bat /nopause` baut das Paket und führt die spielunabhängigen nativen und Regeltests aus. Es installiert nichts dauerhaft.

In PowerShell im Testmod-Verzeichnis:

    .\Run-Tests.ps1 -Layout Fixes -Scenarios rescue-off,rescue-on

`Minimal` verwendet Script Extender + APIShared, `Fixes` zusätzlich Fixes, `Integrated` zusätzlich BugfixesAndQoL und Pathfinding, `Planner` zusätzlich CastlePlanner. Verwendet werden Kopien des aktuell installierten Abhängigkeitsstands. Die Läufe finden nacheinander statt; ein bereits laufendes Spiel wird nicht übernommen.

Für längere Wirtschaftsläufe kann `-Layout Integrated -PeaceTimeMinutes 60` die reguläre Lobby-Friedenszeit anfordern. Diese Prüfung benötigt eine Lobby, die Friedenszeit anbietet; eine fehlende Option wird als Fehler gemeldet. Farm und Kornspeicher müssen während der gesamten Beobachtung dieselbe globale Identität behalten.

Weitere Szenarien: `rescue-group-off/on`, `rescue-aiwall-off/on`, `rescue-moat-off/on`, `rescue-moatworker-off/on`, `rescue-worker-off/on`, `rescue-ox-off/on`, `rescue-siege-off/on` (jeweils die vollständigen Namen wie `rescue-siege-on` übergeben). `-Map` wählt eine vorhandene lokale Gefechtskarte; Standard ist `test AI.map`. Die Vorrichtungen prüfen freie, flache Flächen tatsächlich. Fehlende Voraussetzungen sind Fehler, keine bestandenen Tests.

Der Runner erzeugt verwerfbare Gefechte, sichert Logs und stellt Plugins, Einstellungen und eigene Config wieder her. Er steuert keine globale Maus/Tastatur. Produktive Saves werden nicht geschrieben. Der Testmod bleibt nach dem Lauf nicht installiert; das Paket unter `BepInEx/plugins/TrappedUnitRescueTest_Serp` bleibt zum erneuten Testen erhalten.

## Aussage der Tests

- Zugbrücke: tatsächliche native Brückenplatzierung über kontrolliert platzierten Einheiten; Erstzustand, echte Ausgangssperre und Bewegung nach Rettung werden unabhängig geprüft.
- Gruppen: drei echte Soldaten im selben Verband, getrennte Fälle für blockierten tatsächlichen Anführer und Nicht-Anführer; native Einzelziele und wirkliche Ankunft werden geprüft. Die Off-Gegenprobe misst die freien Mitglieder, ohne eine universelle Gruppenblockade vorauszusetzen.
- KI-Mauerbau: tatsächlicher nativer KI-Baupfad; Off dokumentiert vorhandenes Fixes-Verhalten, On prüft lokale Rettung statt Bergfriedteleport.
- Graben: echter nativer Grabenpfad als Gegenprobe. Ein fertiger Graben darf nicht pauschal als unzulässiger Standort gelten; ein neuer Bewegungsbefehl kann Vanillas Grabenmodus aktivieren.
- Grabenarbeiter: natürlich beschäftigter Apfelbauer mit echtem Graben am Standort. Vergleicht Fixes' tatsächlichen Löschmarker mit dem aktivierten Schutz und anschließendem regulärem Verlassen des Grabens zum bisherigen Arbeitsziel.
- Weitere Szenarien prüfen Arbeiteraufträge/Waren und Gerät/Besatzung. Konkrete `source=`- und Abdeckungsmarker im jeweiligen Log unterscheiden natürliche Abläufe, kontrollierte native Aufrufe und künstliche Zustandsvorrichtungen.
- No-op und Einschluss auf unverändert gültigem Boden dürfen keine Rettung auslösen. Ein erfolgreicher Teleport allein belegt weder Auftragsfortsetzung noch korrekte Arbeit.

`PASS` gilt ausschließlich für die aufgeführten Szenarioassertionen. Abdeckungsmarker weisen ausgelassene Fälle aus; insbesondere sind aktive Sonderzustände, alte Saves und echter Host/Client-Multiplayer nicht durch einen erfolgreichen lokalen Lauf abgenommen. Unbekannte Native-/Fixes-Verträge sperren die Ersatzfunktion, statt fremde Hooks blind zu beeinflussen.

## Fixes-Updates und Bergfried-Reiterrettung

Historischer Ausgangsstand der Prüfung war Fixes 1.30.0; der Runtime-Scannerpfad ist inzwischen mit unveränderter Fixes-1.31-DLL in Lauf 149 geprüft. Die Kompatibilitätsprüfung bindet sich an die tatsächlich beeinflussten Methoden samt relevanten Helfern und den vollständig geprüften installierten Worker-Stub. Eine neue Fixes-Version mit unveränderten relevanten Verträgen bleibt zulässig. Ändert sich ein solcher Vertrag, bleibt unsere Ersatzfunktion aus; ein Update wird dadurch nicht pauschal als sicher erklärt.

Die Fixes-Reiterrettung von Bergfrieden und die Bauernrettung an Torhäusern bleiben aktiv. Der Testmod verändert dafür keine fremden Einstellungen und führt keinen weiteren Schalter ein. Den eigenen nativen Hook prüft er vor Veröffentlichung und bei jedem Missions-/Editorstart auf überlappende Fremdpatches.

    .\Run-Tests.ps1 -Layout Fixes -Scenarios rescue-mountedkeep-off,rescue-mountedkeep-on

Die Bergfriedmatrix prüft Ritter, berittene Bogenschützen und beide Kameltypen, drei Bergfriedtypen, vier Lagerfeuerausrichtungen sowie stehende Einheiten und begonnene Bewegungsschritte. Der kleinste Bergfried erzeugt auf zuvor freiem Boden keine erhöhte Dachfläche und ist deshalb eine Gegenprobe gegen einen unzulässigen Fixes-Teleport. Die beiden größeren sind die positiven Fixes-Rettungsfälle. Identität, Gesundheit, Verband, Ziel und Kachelbelegung werden kontrolliert; hinzu kommen ein blockierter Lagerfeuerausgang und die Kontrolle gegen einen zweiten Teleport nach eigener lokaler Rettung. Die tatsächliche Installation der Fixes-Reiterhooks ist Voraussetzung. Die Szenarien verändern den Fixes-Schalter nicht.

Die spielunabhängigen Vertragstests verändern gezielt relevante IL-Verträge und prüfen deren Ablehnung. Unabhängige Änderungen müssen akzeptiert werden. Diese Prüfung ersetzt weder die Bergfriedmatrix im Spiel noch die bestehenden Brücken-, Gruppen- und Besatzungsläufe.

Die natürlichen Bewegungsschritte prüfen aktuelle und nächste Kachelbelegung getrennt; beobachtete Mittelpunktswechsel werden zusätzlich protokolliert. Ein belegter zusätzlicher Aux-Ketteneintrag wird geprüft, falls er tatsächlich vorkommt. Für normale Reiterschritte ist aber kein Erzeuger eines von null verschiedenen Aux-Feldes belegt. Fehlt ein solcher Fall, steht dessen Bereinigung ausdrücklich auf `not-executed`; sie wird weder erzwungen noch als bestanden ausgegeben.

Die Bergfriedmatrix verwendet einen tatsächlich vorhandenen KI-Spieler des verwerfbaren Gefechts. Fixes überschreibt bei menschlichen Spielern die Bauausrichtung mit der persönlichen Lobbyausrichtung; über den KI-Spieler lassen sich alle vier Richtungen prüfen, ohne diese fremde Einstellung zu verändern oder deren Hook zu umgehen.

CastlePlanner kann beim regulären Kartenstart die native Bergfriedausrichtung vorbereiten und gleicht dafür vorübergehend Fixes' Rotationsdaten ab. Seine Richtungserkennung verwendet ebenfalls das tatsächliche native Feld `+0x92`. Der Testmod greift weder in diesen Ablauf ein noch übernimmt er den ursprünglich angeforderten Winkel als Ersatz für den gebauten Zustand. Das Layout `Planner` ist für die zusätzliche gemeinsame Installation vorgesehen; eine bestandene Prüfung ohne CastlePlanner belegt dessen Startauswahlpfad nicht.

## Fixes-1.30-Befund vom 17.11.2026

**Historische Entscheidung vom 17.11.2026:** zunächst auf eine Korrektur durch den Fixes-Autor warten. Der nachfolgende ausdrückliche Nutzerauftrag erlaubt inzwischen eine isolierte temporäre Korrektur der verwerfbaren Testkopie, damit die weiteren Prüfungen fortgesetzt werden können. Die ursprünglichen Fehlernachweise bleiben gültig. Die Nachprüfung des unveränderten Upstream-Pakets steht unter [Follow-up after a Fixes update](UpdateToNewDLL.md#follow-up-after-a-fixes-update).

Die neue Fixes-Reiterrettung ist mit dem installierten Stand noch nicht abgenommen. In den tatsächlichen veröffentlichten Reiterstubs steht als Aufrufziel `0xE2610` statt der vorgesehenen Versetzungsroutine `0x187110`. Die Reiter erhalten das richtige Ziel, ihre aktuelle Position bleibt aber auf dem Bergfried. Dies tritt mit eigener Rettung aus und an auf.

Ursache ist ein gemeinsam verwendeter, veränderlicher `DataScanner`: Fixes speichert das Scannerobjekt und liest dessen Adresse erst beim späteren Erzeugen des Hooks. Dazwischen verändert ein Pathing-Scan die Adresse. Die historischen Läufe verwendeten keine lokale Korrektur. Die neue Rettung wird weiterhin nicht unterdrückt. Eine dadurch fehlschlagende Bergfriedmatrix darf nicht als bestanden ausgewiesen werden. Spätere, noch nicht erreichte Matrixfälle bleiben offen.

Die acht früheren Spielregressionen für Brücke, Gruppe, Geschützbesatzung und KI-Mauerbau sind jeweils aus/an mit Fixes 1.30 bestanden. Das belegt weder die zusätzliche gemeinsame Installation mit CastlePlanner noch die offene Bergfriedmatrix. Grabenarbeiter und Ochsen bleiben ebenfalls offen.

## Aktuelle Runtime-Scannerkorrektur (26.11.2026)

Die frühere Cecil-CLI und ihr DLL-Kopie-Patchweg sind entfernt. `Run-Tests.ps1` verwendet in allen Fixes-Profilen das unveränderte gelieferte Fixes-Paket; `-OriginalFixes`, das zusätzliche EXE-Projekt und dessen Build-/Selbsttestaufrufe entfallen. Historische Läufe mit korrigierten Testkopien behalten ihre ursprüngliche Aussage.

Die vier Dateien unter `src/FixesMountedScanner*.cs` bilden einen gemeinsam entfernbaren Kompatibilitätsbereich. Eine Deklaration in der normalen Testmod-DLL registriert den frühen Provider über den gemeinsamen APIShared-Bootstrap. Vor Fixes-Aktivierung installiert er genau einen verwurzelten `SafeIlHook` am bekannten Installer. Der ursprüngliche `Scan` läuft einmal; unmittelbar vor dem ursprünglichen Feld-Store wird sein Ergebnis durch einen unabhängigen `context.CreateScanner().At(capturedAddress)` ersetzt. Feldtyp, verzögerte Closure, Emitter und Fixes' native Hooks bleiben original. Es gibt keine Fixes-Dateiänderung, zusätzliche Patcherdatei oder zweite native Hookstelle.

Die fremde Mounted-Option bleibt maßgeblich. Diese Korrektur arbeitet unabhängig von eigener allgemeiner Rettung OFF/ON; sie führt keine Modsetting ein und trifft keine spätere BQoL-Masterentscheidung. Geprüft werden vollständige bekannte IL-/Helper-/Feldzugriffsverträge und tatsächliche native Bytes, kein Fixes-DLL-Hash oder pauschaler Versionsvergleich. Unbenutzte neue Überladungen bleiben zulässig. Bei unbekanntem verwaltetem Vertrag bleibt Fixes unangetastet; ein technischer Nativefehler im bekannten Installer überspringt nur dessen Mounted-Setup mit Error. Ein bekannter bereits skalar korrigierter Capture wird unverändert übersprungen.

Der reguläre Build führt `tests/MountedScannerRuntimeFixture.cs` gegen die vier aktiven Produktionsquellen aus. Das ausdrücklich ausgewählte APIShared-/Bootstrap-Paar wird beim Kompilieren referenziert und gegen die tatsächlich geladenen Bytes geprüft. Die 66 Offline-Prüfungen umfassen echte DMD-Präfixausführung, vier unveränderte Generatoren, vollständige verwaltete Hook-Publikation sowie Update-/Fehlerfälle. Skalare Capture-Varianten existieren ausschließlich im Speicher; generierter Game-Nativecode wird offline nicht ausgeführt.

Lauf `149/40d2c8669dae` besteht mit unveränderter Fixes-1.31-DLL im Planner-Profil: Frontend und 26 Mounted-Partitionen OFF/ON, 104 tatsächliche Stubprüfungen mit je genau einem direkten CALL187110 und geprüfter Originalfortsetzung. Dies behauptet weder eine separat belegte Aux-Kachel noch einen echten CastlePlanner-UI-Rotationsbefehl oder eine im Spiel ausgeschaltete fremde Mounted-Option. Einzelheiten und Wiederherstellung: [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md#nachtrag-149-runtime-scannerkorrektur-ohne-fixes-dateikopie-bestanden). Technischer Aufbau und Entfernung: [Audits/FIXES-MOUNTED-RUNTIME.md](Audits/FIXES-MOUNTED-RUNTIME.md).

## Prüfstand vom 15.11.2026

Die Hauptfälle für Brücke, KI-Mauerbau, gemeinsamen Verband, Gerät/Besatzung und den vollständigen Apfelzyklus wurden positiv geprüft. Der Verbandstest unterscheidet einen blockierten tatsächlichen Anführer von einem blockierten anderen Mitglied. Das beweist keine universelle Blockade durch jedes einzelne Mitglied.

Grabenarbeiter und Ochsen bleiben zusätzliche, noch nicht abgenommene Szenarien. Sie dürfen fehlschlagen und liefern dazu echte Zustandsnachweise. Insbesondere bedeutet der Schutz vor Fixes' Löschmarker noch keine erfolgreiche Wegplanung oder Warenlieferung. Diese offenen Fälle sind kein freigegebener Gameplay-Fix und kein Grund, normale Spielstände automatisch zu bearbeiten.

## Prüfstand vom 17.11.2026 nach den Erweiterungen

Dieser Stand ersetzt die vorherige Warteentscheidung und die älteren offenen Aussagen nur für die hier tatsächlich geprüften Fälle. Die Fixes-Spieltests verwenden die ausdrücklich korrigierte, temporäre Kopie; das unveränderte Fixes-Original ist dadurch nicht als fehlerfrei abgenommen.

| Prüfung | Nachweis | Ergebnis |
|---|---|---|
| Brücke, Gruppe, KI-Mauerbau, Graben-Gegenprobe aus/an im Planner-Profil | Läufe052,054,055; Brücke erneut075, Gruppe/KI-Mauer erneut081 | Bestanden |
| Geschütz und ursprüngliche Ingenieure, sofortiges Absitzen und spätere Identität/Belegung |066/067, erneut077 | Bestanden |
| Natürlich arbeitender Apfelbauer, Rettung und vollständiger weiterer Arbeitszyklus |058/059, erneut078 | Bestanden |
| Eng begrenzte kausale Apfelbauerrettung nach neuem echtem Graben und abgelehnter Wegveröffentlichung |072 | Tatsächlich aus dem Graben gegangen und ursprüngliches Arbeitssegmentziel erreicht; Identität, Gesundheit, Arbeitsplatz und Waren erhalten |
| Echter KI-AIV-Zugbrückenaufruf mit No-op, aus/an und anschließendem tatsächlichem Bewegungsziel |062/063, erneut076 | Bestanden; ursprünglicher Bau ohne Fixes-Unterdrückung |
| Derselbe AIV-Pfad mit ausschließlich Pflichtabhängigkeiten |071 | Beide Gegenproben bestanden |
| Bergfriedmatrix mit eigener Rettung aus/an, einschließlich CastlePlanner im Modprofil |073/074 | Je48 stehende und49 begonnene Fälle; vier Reitertypen, drei Bergfriede, vier Ausrichtungen, blockierter Ausgang und Schutz gegen zweite Versetzung bestanden |
| Natürlich beladener Ochse mit tatsächlicher Warenlieferung |079/080, `2 Penninsula.map`,60 Minuten Friedenszeit | Beide Gegenproben bestanden; On liefert alle12 ursprünglichen Steine, ohne künstliche Ladung/AI-Zustände |

Die Bergfriedmatrix weist den kleinen Bergfried auf freiem Boden als Gegenprobe ohne erhöhte Dachfläche aus. Pro Lauf wurden32 stehende und33 begonnene positive Rettungen sowie jeweils16 negative Fälle geprüft. Mit eigener Rettung wurden zusätzlich alle vier Reitertypen lokal von einer echten Brücke gerettet und nach weiteren Fixes-Ticks nicht nochmals versetzt. Tatsächliche natürliche Belegungsübergänge wurden beobachtet; ein belegter zusätzlicher Aux-Zustand wurde nicht erreicht und bleibt ausdrücklich `not-executed`.

Die spielunabhängigen Prüfungen bestehen:1538 Regel-/Zustandsprüfungen,263 native Worker-Guard-Prüfungen, sechs echte Produktions-Receipt-Verträge sowie relevante Fixes-IL- und temporäre Cecil-Verträge. Die Quellprüfungen für Abhängigkeiten, Lifecycle, JSON, XAML und permanente Hooks bestehen. Diese Zahlen belegen keine zusätzlichen Spielmodi.

Der positive Grabenbeleg072 betrifft Apple-AI5 ohne Ladung. Ein beladener Apple-AI7-Fall ist damit noch nicht im Spiel abgenommen.

Noch offen bleiben die automatische Beobachtung weiterer Bau- und Grabenpfade, nur über Next betroffene oder sonstige besondere Bewegungszustände, normale menschliche/Editor-Platzierung, eine vollständige Warenlieferung nach der Grabenrettung und echte Editor-/Save-/Host-Client-Abnahmen. Der automatische Pilot bleibt auf aktuelle, zugelassene KI-AIV-Brückenbelegungen beschränkt. Die Bergfried-Rotationen werden an tatsächlichen nativen Gebäudefeldern geprüft; die Profilprüfung allein belegt keine beliebige menschliche CastlePlanner-Startauswahl.

Die ausführlichen Logs liegen unter `.work/a/rescue-game/<Laufnummer>`; der zusammengefasste neue Arbeitsnachweis liegt unter `.work/a/rescue-next/001`. Jede Spielprüfung stellt ihre ursprünglichen Plugins und Einstellungen wieder her. Es wurde keine dauerhafte Installation, kein Commit und keine Veröffentlichung vorgenommen.

Für den natürlichen Ochsenlauf die tatsächlichen Voraussetzungen ausdrücklich wählen:

    .\Run-Tests.ps1 -Layout Planner -Map '2 Penninsula.map' -PeaceTimeMinutes 60 -Scenarios rescue-ox-off,rescue-ox-on
