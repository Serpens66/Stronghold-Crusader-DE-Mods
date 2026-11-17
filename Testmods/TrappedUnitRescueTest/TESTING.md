# Einheitenrettung testen

Fortlaufender Arbeits- und Integrationsplan: [Allgemeine Rettung und spätere Integration in Pathfinding](RESCUE-PLAN.md).

Dieser Testmod ist ein isolierter Versuchsstand. Die experimentelle Einstellung ist standardmäßig ausgeschaltet. Aktiviert sie der Nutzer vor einer bekannten lokalen Mission, beobachtet ein begrenzter Pilot den echten KI-AIV-Zugbrückenbau und versucht neu eingeschlossene eigene Soldaten und Reiter lokal zu retten. Bestehende feststeckende Einheiten werden nicht rückwirkend gesucht oder repariert. Das ist noch kein fertiger allgemeiner Spielfix.

Die Fixes-Unterdrückung ist auf eigene synchrone Rettungsaktionen begrenzt und wird in `finally` wieder ausgeschaltet. Der neue Pilot erfasst den Zustand vor dem regulären KI-Bau, lässt sämtliche Vanilla-/Fixes-Bauaktionen unverändert laufen und prüft danach dieselben Einheiten. Erst seine eigene Versetzung beziehungsweise Auftragsfortsetzung öffnet das enge Unterdrückungsfenster. Die bisherigen Testvorrichtungen behalten ihren gesonderten Simulationsmonitor; der Pilot bleibt dort ausgeschaltet, außer im eigens dafür angeforderten AIV-Szenario.

Der Pilot gilt nur für KI-AIV-Zugbrücken und zugelassene kontrollierbare Soldaten/Reiter in bekannten lokalen Missionen, einschließlich Editor. Zivile Arbeiter, Belagerungsgeräte und Besatzungen, menschliche Platzierung, normale Editorplatzierung, Grabenabschluss, rückwirkende Save-Reparatur und echter Multiplayer sind damit nicht abgedeckt. Er beobachtet weder sämtliche Bauereignisse noch beliebige Ursachen unpassierbarer Felder. Fehlende Vorherdaten oder unbekannte Zustände lösen keine Rettung aus.

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

Geprüfter Ausgangsstand ist Fixes 1.30.0. Die Kompatibilitätsprüfung bindet sich an die tatsächlich beeinflussten Methoden samt relevanten Helfern und den vollständig geprüften installierten Worker-Stub. Eine neue Fixes-Version mit unveränderten relevanten Verträgen bleibt zulässig. Ändert sich ein solcher Vertrag, bleibt unsere Ersatzfunktion aus; ein Update wird dadurch nicht pauschal als sicher erklärt.

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

## Temporäre Korrektur nur in der Testkopie

`TemporaryFixes` enthält ein eigenständiges Cecil-CLI. `Build.ps1` baut und prüft es ohne Installation. `Run-Tests.ps1` kopiert für `Fixes`, `Integrated` und `Planner` zuerst die Abhängigkeiten in eine verwerfbare `.work`-Vorrichtung und korrigiert nur deren `plugins/fixes/fixes.dll`, bevor der offizielle Runner sie vorübergehend installiert. `temporary-fixes.log` dokumentiert Anwendung oder Skip. Das Originalpaket und der kanonische Fixes-Quellbaum werden nicht bearbeitet; der Runner stellt das installierte Original nach jedem Szenario wieder her. Ein fehlgeschlagener Vertragscheck bricht vor dem Spielstart ab.

Die Änderung erfasst `CurrentAddress` unmittelbar nach `Scan` als `UInt64`, statt ein später verändertes Scannerobjekt zu speichern. Der native Recovery-Emitter bleibt unverändert. Es gibt keinen Preloader und kein Umpatchen veröffentlichter nativer Hooks. Diese gesondert autorisierte Korrektur gilt für beide Aus/An-Gegenproben im temporären Testlayout. Die eigene experimentelle Einstellung steuert weiterhin nur unsere Rettung und deren bestehende enge Fixes-Unterdrückung; fremde Einstellungen bleiben unverändert.

Gepinnt sind die normalisierten relevanten Installer-/Callback-IL-Verträge, kein gesamter DLL-Hash und keine Versionsnummer. Der bekannte bereits korrigierte skalare Capture wird übersprungen; dabei sind ein entfallener NOP und der Feldname `relocateAddress` zulässig. Auch ein fehlender Installer wird als nicht anwendbar gemeldet. Unbekannte relevante Änderungen werden abgelehnt, auch wenn ein anders aufgebauter Installer möglicherweise bereits korrekt ist. Dafür ist eine erneute Prüfung nötig.

Die spielunabhängigen Prüfungen belegen Transformation, Serialisierung, Idempotenz, unveränderten Emitter und Ablehnung unbekannter Verträge. Das ist noch keine bestandene Bergfriedmatrix oder allgemeine Gameplay-Abnahme. Ergebnisse korrigierter Testkopien müssen getrennt von unverändertem Upstream-Fixes ausgewiesen werden.

Vollständig entfernen: Ordner `TemporaryFixes`, dessen Build-/Selbsttestaufrufe in `Build.ps1` und den Kopie-Patchblock in `Run-Tests.ps1` entfernen. Es werden keine zusätzliche Modsetting und keine dauerhaft installierte Patcher-/Plugin-Datei hinterlassen. Details: [TemporaryFixes/INTEGRATION.md](TemporaryFixes/INTEGRATION.md).

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
