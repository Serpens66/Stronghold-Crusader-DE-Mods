# Einheitenrettung in BugfixesAndQoL testen

**Aktueller Integrationsstand, 27.11.2026:** Der separate Testmod automatisiert ausschließlich die tatsächliche BQoL-Runtime. Er besitzt keine eigene Rettungsruntime und keinen EarlyProvider. Maßgeblich sind der aktive [RESCUE-PLAN.md](RESCUE-PLAN.md), [SP-ACCEPTANCE.md](SP-ACCEPTANCE.md) und das tatsächliche ausgewählte Paket; historische Prototyp-Ergebnisse ersetzen keine neue Produktionsabnahme.

Die produktive Hostoption `EnableTrappedUnitRescue` ist standardmäßig AN. Zusammen mit `EnableMod` steuert sie sowohl die allgemeine SP-/Editor-Rettung als auch die entfernbare Fixes-Reiterkorrektur. Der Clientmaster steuert diese Hostfunktionen nicht. OFF führt keine eigene historische Arbeiterbereinigung aus. Im Testmod gibt es keine zusätzliche Produktoption; ohne gültige eigene Automationsanforderung beginnt kein Szenario.

## Aktueller Abschlussstand nach Normalabnahme215 (27.11.2026)

Das integrierte künstliche NoJesters9871-Profil mit konstanter1000er-Walkingkohorte ist tatsächlich abgeschlossen: acht ABAB-Fenster, fünf genaue Welt-/Ready-Handoffs, vier Endpunktprüfungen und alle9871 Originalidentitäten; Wiederherstellung und Desktopabschluss bestätigt. Die [aktuelle181-Checks-Qualifikation](../../.work/a/rescue-game/213/0ad2d176595b/performance-validation-current.json) ist positiv. Nach Generic-Trampolinfix und37 fokussierten/vollständigen SDK-Prüfungen erschien `NOTIFICATION_IDENTITY_READY` im Spiel.

Die [Zusammenfassung213](../../.work/a/rescue-sp/001/n8-perf213-summary.json) enthält die Messwerte: OFF/ON-Wall-Median der Fenstermittel Idle5.011/4.8025ms, Walking5.3465/5.3395ms; keine kausale Verbesserung behaupten. Je ON-Fenster321/916 Erfassungen, aber0 Versuche/Rettungen/Entfernungsaufträge. Drei Werte über50ms ausschließlich im letzten ON-Walkingfenster, Maximum71.220ms,0 über100ms; keine Ursachenzuordnung.

Genau ein früher verifizierter PitchLine-Error durch den PerfProbe-Monitorwrapper begrenzt die positive Qualifikation auf dieses identische OFF/ON-Diagnoseprofil. Keine volle BQoL-/Pitch-Kompatibilitätsfreigabe oder allgemeine FPS-/Normalweltgarantie daraus ableiten. Der frühere Pending-Vermerk der Zusammenfassung ist durch die aktuelle Qualifikation überholt. Das normale Endpaket besteht inzwischen ebenfalls:214/0714d5b63904 (Planner14 Fälle) und215/29cd44bd7f5b (Minimal4 Fälle), jeweils ohne Error-/Fatal-Zeilen, mit Notification-/PitchLine-READY sowie bestätigter Wiederherstellung. Main- und Acceptance-Normalbuild, NoDevGate und Ausschluss der Diagnosehelfertypen sind bestanden. Siehe [Abschlussabnahme](FINAL-ACCEPTANCE.md). Die bisherigen historischen Laufbelege bleiben erhalten.

## Ausgewählte Pakete bauen und ausführen

Vom Workspace-Root aus ein vollständig geprüftes APIShared-SDK samt passendem Bootstrap und ein aktuelles BQoL-Paket auswählen. Folgendes Beispiel verwendet den datierten lokalen Rescue-SDK-Pfad; ein anderer geprüfter SDK-Quellstand darf entsprechend ausdrücklich gewählt werden. Die beiden Auswahlvariablen verhindern, dass der Consumer-Precheck versehentlich den anderen APIShared-Checkout prüft.

```powershell
$sdk = (Resolve-Path 'APIShared').Path
$env:SHCDE_BRANCH_API_ROOT = $sdk
$env:SHCDE_BRANCH_API_COMMIT = (git -C $sdk rev-parse HEAD).Trim()
$api = Join-Path $sdk 'BepInEx/plugins/APIShared_Serp'
$patcher = Join-Path $sdk 'BepInEx/patchers/APIShared.DependencyBootstrap.dll'
$main = (Resolve-Path 'BugfixesAndQoL/BepInEx/plugins/BugfixesAndQoL_Serp').Path
& './Testmods/TrappedUnitRescueTest/build.bat' /source:working /nopause /noinstall -ApiSharedDir $api -BugfixesDir $main
& './Testmods/TrappedUnitRescueTest/Run-Tests.ps1' -Layout Planner -Scenarios 'rescue-aivbridge-default-on,rescue-aivbridge-hostmaster-off,rescue-aivbridge-clientoff-on,rescue-aivbridge-off,rescue-aivbridge-on' -Map '4spielerkarte.map' -PeaceTimeMinutes 60 -ApiPluginPackage $api -ApiPatcherPackage $patcher -BugfixesPackage $main -SkipBuild
```

Der offizielle Build prüft die Serienorchestrierung, die echte Dispatcher-Emission und die Abwesenheit eines zweiten Runtime-/Scannerbesitzers im Acceptance-Paket. Die produktiven Kern-/Arbeiter-Guard-/Scannerprüfungen gehören zu `BugfixesAndQoL/tests/TrappedUnitRescue.Tests`; sie bleiben beim Entfernen des Prototyps erhalten.

Alle Layouts enthalten jetzt BQoL: `Minimal` verwendet Script Extender, APIShared und BQoL; `Fixes` fügt Fixes hinzu, `Integrated` zusätzlich Pathfinding und `Planner` zusätzlich CastlePlanner. RandomEvents ist aus dem Testlayout ausdrücklich ausgeschlossen. Die Szenarien setzen Welt, Modsettings, Frieden, Lage und erforderliche AIC/AIV-Voraussetzungen selbst und prüfen sie vor der jeweiligen Wirkung. Der Runner nutzt einen getrennten Win32-Desktop ohne globale Maus-/Tastatureingaben, sichert Dateien und stellt Plugins, Konfiguration und Einstellungen nach der Serie überprüft wieder her. Eine laufende Nutzerpartie wird nicht übernommen.

Eine `rescue-series` umfasst bis64 expandierte Fälle in einem Spielprozess und einem Layouttausch. `rescue-mountedkeep-off/on` expandiert jeweils in13 Partitionen. In dieser Matrix bedeutet OFF eine testlokale Pause der allgemeinen Rettung bei tatsächlicher Hostoption/Scannerkorrektur AN; echte Produkt-OFF-Fälle sind die eigenen AIV-/Masterprofile. Der Liveprüfer bindet Gateadresse, ursprünglichen und korrigierten CALL sowie Zweigführung und Wiederherstellung, bevor Reiter erzeugt werden.

Normale Pakete und Diagnosepakete getrennt qualifizieren. Für Diagnose-/NoJesters-Performanceprofile müssen BQoL und Acceptance beide über ihren jeweiligen offiziellen `build.bat /devdiag` gebaut werden. Die alte Prototyp-Diagnoseklasse darf nicht zusätzlich kompiliert werden. Das9871-Profil verwendet die800er-Karte Back to Back, ein ausdrücklich künstliches Jester-freies Testweltprofil und ein explizites PerfProbe-Paket; seine Zahlen sind keine allgemeine Normalwelt-/FPS-Garantie. Performanceprofile laufen einzeln mit eigenen ABAB-Fenstern und strengem Logvalidator.

## Save-/Load-Fälle einzeln starten (27.11.2026)

Der aktuelle Fixturevertrag erlaubt genau eine neue Datei `SerpAcceptance-<tatsächliche AutomationRun-ID>.sav` pro Runnerlauf. Mehrere Szenarien derselben Serie teilen diese Run-ID. Persönliche Dateien werden weder überschrieben noch durch frei gewählte Suffixe aus der Inventarprüfung ausgenommen. Der Runner archiviert und entfernt ausschließlich diese bekannte eigene Datei; seine Hash- und Wiederherstellungsprüfungen bleiben unverändert.

Die neue Pending-Entfernungsabnahme deshalb in zwei getrennten Aufrufen ausführen:

    .\Run-Tests.ps1 -Layout Planner -Map '4spielerkarte.map' -PeaceTimeMinutes 60 -Scenarios rescue-removal-persistence-off
    .\Run-Tests.ps1 -Layout Planner -Map '4spielerkarte.map' -PeaceTimeMinutes 60 -Scenarios rescue-removal-persistence-on

Auch andere tatsächlich speichernde Fälle wie `rescue-legacy-civilian-off`, `rescue-persistence-parked-on` und `rescue-persistence-off/on` dürfen nicht dieselbe Run-ID für eine zweite Save-Datei verwenden. Die Legacy-OFF-Vorrichtung benötigt zusätzlich eine geeignete natürliche Quarry-/Tether-Testwelt; ihre belegte Karte ist `2 Penninsula.map`. Editorfälle separat von einer bereits speichernden Serie ausführen: Der aktuelle Editor-Pfadhelfer leitet seinen eigenen Kartennamen ebenfalls über den geschützten Save-Pfad ab.

Lauf175 (`175/d642428c038e`) hat KI-Arbeiter OFF/ON sowie Pending-Save OFF tatsächlich abgeschlossen. Pending-Save ON wurde danach **vor seinem Weltaufbau** abgelehnt, weil OFF bereits den gemeinsamen Save-Pfad angelegt hatte. Das ist eine korrekt greifende Überschreibsperre und eine Grenze dieser Serienzusammenstellung, kein bestandener ON-Test und kein nachgewiesener Rettungsfehler. Die ON-Abnahme bleibt als eigener Runnerlauf erforderlich; vorhandene Laufbelege werden nicht umgedeutet.

## Aussage der Tests

- Zugbrücke: tatsächliche native Brückenplatzierung über kontrolliert platzierten Einheiten; Erstzustand, echte Ausgangssperre und Bewegung nach Rettung werden unabhängig geprüft.
- Gruppen: drei echte Soldaten im selben Verband, getrennte Fälle für blockierten tatsächlichen Anführer und Nicht-Anführer; native Einzelziele und wirkliche Ankunft werden geprüft. Die Off-Gegenprobe misst die freien Mitglieder, ohne eine universelle Gruppenblockade vorauszusetzen.
- KI-Mauerbau: tatsächlicher nativer KI-Baupfad; Off dokumentiert vorhandenes Fixes-Verhalten, On prüft lokale Rettung statt Bergfriedteleport.
- Graben: echter nativer Grabenpfad als Gegenprobe. Ein fertiger Graben darf nicht pauschal als unzulässiger Standort gelten; ein neuer Bewegungsbefehl kann Vanillas Grabenmodus aktivieren.
- Bauern/Arbeiter: echte kausale Änderung, eigene Löschmarkierung und anschließende vollständige native Entfernung, freie alte Ketten sowie Mitarbeiterbindungen und gegebenenfalls natürliche Neubesetzung. Eine vorherige Vanilla-/Fixes-Markierung gewinnt ohne zweite eigene Markierung.
- CurrentSafe-/Next-only-Gegenproben verlangen an der ersten qualifizierten Recoveryboundary eine lebende ursprüngliche Identität auf sicherem Current und null eigene Markierungen. Wird später die akzeptierte Next-Kachel zum unsicheren Current, kann eine Entfernung legitim werden. Alte Arbeiteraufträge, Cargo oder Waren werden nicht wiederhergestellt oder kompensiert.
- Weitere Szenarien erhalten Soldaten, Verbände sowie Gerät/Besatzung und prüfen tatsächliche Bewegung. Konkrete `source=`- und Abdeckungsmarker im jeweiligen Log unterscheiden natürliche Abläufe, kontrollierte native Aufrufe und künstliche Zustandsvorrichtungen.
- No-op und Einschluss auf unverändert gültigem Boden dürfen keine Rettung auslösen. Ein erfolgreicher Teleport allein belegt weder Auftragsfortsetzung noch korrekte Arbeit.

`PASS` gilt ausschließlich für die aufgeführten Szenarioassertionen. Abdeckungsmarker weisen ausgelassene Fälle aus; insbesondere sind aktive Sonderzustände, alte Saves und echter Host/Client-Multiplayer nicht durch einen erfolgreichen lokalen Lauf abgenommen. Unbekannte Native-/Fixes-Verträge sperren die Ersatzfunktion, statt fremde Hooks blind zu beeinflussen.

## Fixes-Updates und Bergfried-Reiterrettung

Historischer Ausgangsstand der Prüfung war Fixes 1.30.0; der Runtime-Scannerpfad ist inzwischen mit unveränderter Fixes-1.31-DLL in Lauf 149 geprüft. Die Kompatibilitätsprüfung bindet sich an die tatsächlich beeinflussten Methoden samt relevanten Helfern und den vollständig geprüften installierten Worker-Stub. Eine neue Fixes-Version mit unveränderten relevanten Verträgen bleibt zulässig. Ändert sich ein solcher Vertrag, bleibt unsere Ersatzfunktion aus; ein Update wird dadurch nicht pauschal als sicher erklärt.

Die fremden Einstellungen für Fixes-Reiterrettung und Torhausbauern werden nicht verändert. BQoL besitzt die Scannerkorrektur und die Hook-/Fremdpatchprüfung; der Acceptance-Mod besitzt keinen eigenen Rettungshook oder EarlyProvider. Die eigene Korrektur folgt `EnableMod && EnableTrappedUnitRescue`; OFF lässt den ursprünglichen Fixes-Aufruf bestehen.

    .\Run-Tests.ps1 -Layout Fixes -Scenarios rescue-mountedkeep-off,rescue-mountedkeep-on

In diesen beiden Matrixprofilen bedeutet OFF ausschließlich eine testlokale Pause der allgemeinen Rettung; tatsächliche BQoL-Hostoption und Scannerkorrektur sind beide AN. Das ist keine Abnahme der ausgeschalteten Produktoption. Tatsächliche Options-/Master-OFF-Fälle verwenden `rescue-aivbridge-off`, `rescue-aivbridge-hostmaster-off` und die Legacy-Save-/Load-Gegenproben; sie verlangen keine eigene Korrektur oder Bereinigungsaktion.

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
