# Fixes MountedKeep: früher Runtime-Scanner-Fix — Audit und Entscheidung

Stand26.11.2026; Analyse, KEINE Implementierung/Installation/neuer Spieltest.
Die externe Fixes-Quelle und alle aktiven Produktionsquellen bleiben unverändert.
Probe141 hat laut Root den tatsächlichen deklarativen Einstieg vor Fixes/Owner-Awake
bei noch nicht initialisiertem APIShared sowie denselben Thread1 belegt. Diese Probe
installiert keinen Scanner-Fix und belegt dessen native Wirkung noch nicht.

## Geschlossener aktueller Pfad

Gelesen: Fixes Bootstrap/Plugin und PluginOptions; DetourManager.ApplyNative;
FixesUnitDetours-Konstruktor, vollständiger Installer, verzögerter Generator und
EmitMountedKeepRecovery; aktuelle TemporaryFixes/Program.cs samt Vertragsprüfungen;
installierte DataScanner- und Transaktionshelfer; öffentlicher SafeIlHook sowie
BQoL-Awake/Settings-/LibraryLoaded-Reihenfolge. Installation aktuell Fixes1.31.0.0,
MVID2d340ff7-1d46-418e-9447-470dc017ca54. Das aktuell gelesene installierte IL hält
weiterhin einen DataScanner im Feld relocate und liest dessen Adresse verzögert.
Version/MVID sind Herkunft, KEINE künftige Freigabebedingung.

Der kanonische native Hash stimmt frisch mitCURRENT überein:
FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2.
Das vollständige Relocate-Muster des Installers hat genau EINEN Dateitreffer:
RVA187110. Dies ist NICHT der19BA40-Helfer unseres Rettungsmods.

1. FixesUnitDetours ruft InstallMountedKeepRecovery nur bei seinem eigenen
   ConfigEntry EnableMountedUnitsKeepRecovery.Value auf. Kein eigener General-
   Rettungsschalter wird dort gelesen. Originaloption ist standardmäßigtrue.
2. scanner.Scan verändert _currentAddress und gibt exakt this zurück. Die aktuelle
   installierte IL schreibt das Feld, liest die Adresse erst später im Generator.
3. AddInline registriert nur einen InlineDescriptor; dessen Prepare ruft Generate.
   Commit führt die Pipeline erst nach den Unit- UND Pathing-Konstruktoren aus.
   Alle verwenden den übergebenen selben Scanner. Weitere Scans überschreiben ihn.
4. Der Generator reicht dann seine falsche CurrentAddress an den UNVERÄNDERTEN
   Emitter. Historische046/047 belegten tatsächlichCALL E2610 statt187110. Diese
   historischen Livebelege werden hier nicht als neue Messung ausgegeben.
5. Der Emitter hakt vier Reiter-Update-Einstiege ein, prüft Large-Profil, echtes
   Keep an Current bzw. aktivem Next und echtes eigenes Campground. Er verwendet
   dessen gespeicherte Richtung und überprüft aktive Zielkoordinaten/Oberfläche.
   Er bereitet Next/Bewegungsfelder vor, belässt alten Current/aux bis Cleanup und
   ruft187110(manager,id). Seine anschließenden Originalinstruktionen bleiben bestehen.
6. Vollständig gelesenes187110 löscht Traversal-Felder, hängt aux und alten Current
   über186AF0/186AD0 aus, übernimmt vorbereitete738/73A nach71C/71E/72C und
   Feinposition und hängt über180230 wieder ein;18BE50 aktualisiert Darstellung.
   19BA40 hat dagegen fünf Argumente/einen anderen Vertrag und ist kein Ersatz.

Die vorhandenen CastlePlanner-Richtungs-/Campground-/Belegungsgrenzen bleiben
unverändert. Dieser Auftrag rechtfertigt weder neue native Emitterlogik noch eine
zweite19BA40-Eigentümerschaft, Reparatur veröffentlichter Stubs oder Settingsänderung.

## Laufzeitweg ohne DLL-Kopie: eine engere Möglichkeit als Feldtypänderung

Die bisherige Cecil-Dateikopie ändert ein Feld von DataScanner zuUInt64. Dies ist
an einem bereits geladenen CLR-Typ keine zulässige reine ILHook-Transformation.
Diese Dateikopie bleibt deshalb ein historischer Testweg und wird kein Produktweg.

Ein überprüfter öffentlicher API-Weg benötigt nur EINEN managed ILHook am Installer:
Nach seinem bestehenden Scan, unmittelbar vor dem bestehenden stfld, dessen Ergebnis
durch einen privaten unabhängigen Scanner ersetzen. Der aktuelle öffentliche
context.CreateScanner() erzeugt tatsächlich einen NEUEN Scanner über context.Region;
At(adresse) schreibt nur dessen eigene Adresse/Found und gibt dieses neue Objekt
zurück. Der Konstruktor akzeptiert null als Logger und verwendet NullLogger; ohne
Cacheoption wird kein Resolver erzeugt. Keine erneute Suche, keine private RedBird-
Feldmutation und keine Änderung des CLR-Feldtyps sind nötig. Der unveränderte spätere
Getter liest damit dieselbe früh eingefrorene numerische Adresse aus einem Objekt,
das laut vollständigem Closure-Vertrag nur dort gespeichert/gelesen wird.

Der ursprüngliche Scan und seine Cache-/Fehlerseite bleiben einmalig erhalten.
Der Helper erfasst die Adresse JETZT; er darf nie später den ursprünglichen Scanner
zurücksetzen. Stack vorstfld ist [closure,scanner]; context nachladen und ein Helper
(scanner,context)->unabhängigerScanner lässt [closure,neuerScanner] für denselben
Store stehen. Den verzögerten Generator und den nativen Emitter muss man nicht ändern.

Vor Implementierung ist genau dieser kleine Transformationsvertrag in einem echten
DMD/Delegate-Fixture zu testen. Quelle hier ist ein auditiert möglicher Entwurf,
kein bereits bestandener Runtime-Patch. Alternative: scalarer privater Closure-Receipt
plus zweiter ILHook im Generator. Sie braucht zwei koordinierte Veröffentlichungen
und eine Lookup-Lebensdauer, bietet hier keinen erkennbaren Vorteil. Eine Änderung
am Scanner-Getter global oder nachträgliches At am GEMEINSAMEN Scanner ist abzulehnen:
sie beeinflusst die folgenden Pathing-Scans/andere verzögerte Verbraucher.

## Verträge und Fail-closed-Grenzen

Freigabe nach tatsächlich relevanten IL-/Helferverträgen, NICHT Fixes-DLL-Hash oder
exakter Versionsnummer: bekannter Installer/Closure-Datenfluss, einzige Scan-/Store-
Stelle, alle Leser dieses Felds, unveränderter Emitter/Argumente, ursprünglicher
Optionszweig und Transaktionsverzögerung; tatsächliche CreateScanner/Create/At/Getter-
Verträge. Harmlose andere Fixes-Methoden/Versionen dürfen sich ändern. Die vorhandenen
normalisierten Broken-/Fixed-Fingerprints sind Ausgangspunkt, nicht allein ein
vollständiger Runtimebeweis. Bereits bekannt korrekt scalar erfasst: ausdrücklich
überspringen; Installer fehlt: nicht anwendbar; unbekannt relevant: kein heuristischer
Eingriff und keine Unterdrückung des unbekannten fremden Features.

Beim tatsächlichen Installeraufruf zusätzlich Kontext/Found/Adresse, bekannte native
Hash-/187110-/ABI-/Grenzen-/Livebyte-Verträge prüfen. Falls diese Prüfung scheitert,
muss ein vorab mitgeprüfter früher Rücksprung ausschließlich diesen Mounted-Installer
vor seinem ersten AddInline verlassen; kein exceptionbedingter Abbruch des ganzen
Unit-/Pathing-Setups. Keine Originaloption verändern. Dies ist technische
Nichtverfügbarkeit auf einem nicht unterstützten nativen Vertrag, kein General-OFF.
Unbekanntes managed IL kann man ohne geprüften Interzeptionsvertrag nicht ebenso
selektiv überspringen: dann nur die eigene Korrektur als unavailable melden und
Originalcode unangetastet lassen, ohne dessen Fehlerfreiheit zu behaupten.

SafeIlHook wird im tatsächlichen deklarativen BeforeFixes installiert, statisch
verwurzelt und nie nach Veröffentlichung entfernt. Vor Erstaufruf vollständige
IL-/JIT-/Branch-/EH-/Stackprüfung; spätere Rebuilds/IsApplied überwachen. Ein späterer
fremder Rebuild kann eine Korrektur verlieren; bereits emittierte native Konstanten
werden deswegen weder rückgepatcht noch neu veröffentlicht. Keine Zusage über
beliebige zukünftige fremde IL-/Resolveränderungen. APIShared bleibt generischer
Broker; die konkrete Korrektur gehört separat in BQoL bzw. zunächst ins Testmod.

## Gating: ausdrücklich notwendige Schlussfolgerung

| Gate | Konsequenz |
| --- | --- |
| Neue General-Rettungssetting aus/an | DARF den Scanner-Fix NICHT beeinflussen. MountedKeep bleibt die ursprüngliche fremde Funktion. |
| Original Fixes.EnableMountedUnitsKeepRecovery | Bleibt alleinige ursprüngliche fachliche Autorität für deren Setup; keine Änderung/zusätzliche Setting. |
| Korrigierter unterstützter Capture-Vertrag | Korrekte Adresse unabhängig von General-OFF und eigener Recovery-Readiness. |
| BQoL-Master/Settings-Readiness | Früh vor Fixes-Awake noch nicht zuverlässig verfügbar; darf nicht als implizite neue Vorbedingung eingeführt werden. |

Empfohlen: Diese kleine Integritätskorrektur gilt bei ausgewähltem BQoL und
unterstütztem Fremdvertrag unabhängig von General-Setting UND BQoL-Gameplay-Master.
Die fachliche Funktion aktiviert weiterhin allein Fixes. Das bewahrt das aktuelle
Testverhalten: TemporaryCorrection wirkte auch mit eigener General-RettungOFF.
Ein später fehlgeschlagenes BQoL-Awake ist kein Grund, eine schon korrekt eingefrorene
fremde Funktionsadresse wieder zu zerstören oder fremde Hooks abzuschalten.

Ein Master-Opt-out wäre eine wesentlich andere Produktregel: beiOFF würde erneut
der bekannte Originalfehler gelten. Zudem wirkt die Adresse nur während einmaligem
Native-Setup; ein späterer Masterwechsel kann bereits veröffentlichte Stubs nicht
logisch zurücksetzen. Diese Alternative müsste Root ausdrücklich entscheiden und
als Grenze dokumentieren; sie ist keine unsichtbare Implementationseinstellung.
Mounted komplett wegen GeneralOFF/MasterOFF zu unterdrücken verletzt den Vertrag
und ist ausgeschlossen. Keine Runtime-Bool darf absichtlich zwischen gutem und
bekannt falschem nativen CALL-Ziel umschalten.

## Nachfolgende konkrete Schritte, noch nicht durchgeführt

1. Root bestätigt Gating/Ein-ILHook-Entwurf. Korrektur als eigenes leicht entfernbares
   Modul mit deklarativem Provider; kein UI-/Settings-/Plugin-Init in seinem frühen
   Einstieg. BQoL-harte API-Mindestversion bei Integration konsistent anheben.
2. Produktionswrapper-Tests: gemeinsamer Scanner wird danach mehrfach verändert,
   alle vier Generatoren behalten187110; Original-Optionfalse erzeugt keine neuen
   Mounted-Hooks; GeneralOFF/ON gleich; unterstütztes scalarFixed skip; unabhängiges
   Update akzeptiert; manipulierte relevante IL/API/native Guards vor Veröffentlichung
   ablehnen; nur Mounted-Setup überspringen, andere Unit-/Pathing-Features erhalten.
3. Wrapper/JIT/Stack/EH sowie Original-Scan genau einmal/kein globaler Scannerreset,
   private Scanneridentität/At-Zustand/fehlende Bindung, Capture/Emitter und tatsächliche
   Helperadresse testen. Keine nativen Live-Proben an kopierten Puffern beim Startup.
4. Erst danach Runner-Dateikopie für diesen Abnahmelauf abschalten; tatsächliche
   veröffentlichteCALLs187110 und vollständige Reiter-/Keep-/Rotation-/Schritt-/blockierter
   Ausgang-Matrix mit GeneralOFF/ON belegen. Identität/HP/Tribe/Ziel/Belegung und keine
   doppelte spätere Versetzung prüfen. Keine bestandene Migration daraus ableiten.
5. Nach vollständiger Abnahme zu BQoL; ausschließlich diese Datei/Providerdeklaration
   und ihre Tests später entfernen, sobald ein überprüfter upstreamFix den Fehler
   selbst behebt. Keine externen Quellen oder DLLs mutieren.
