# KI-Verkaufsfix nach BugfixesAndQoL übertragen

Stand: 22.10.2026. Dies ist der Umsetzungsplan; die Integration in BugfixesAndQoL
und die neue öffentliche API sind noch nicht implementiert. Die genannten
Quellpfade und bestehenden APIs wurden im aktuellen lokalen Arbeitsstand geprüft.
Bei Umsetzung Branch, HEAD, Arbeitsstand, ausgewähltes SDK und Hookbesitzer erneut
feststellen. Bestehende Nutzeränderungen erhalten; keine automatischen Commits,
Branchwechsel oder Veröffentlichungen.

## 1. Ergebnis der erneuten Prüfung

Die beiden fachlichen Sicherungen bleiben bestehen:

- Finale wirksame KI-Verkaufsreserve bei `0x3F1F3..0x3F202` auf mindestens null
  begrenzen. Erst danach prüft Vanilla `Bestand > Reserve`; leere frühere Waren
  blockieren dadurch keine späteren verkaufbaren Waren.
- Unmittelbar vor Preisberechnung und Buchung bei `0x29723..0x29732` die Menge auf
  den vorzeichenbehaftet interpretierten, nichtnegativen Gesamtbestand begrenzen.
  `EBX` und `R9D` müssen denselben Wert erhalten. Bei null den bestehenden Epilog
  `0x2978B` benutzen; keine Preisabfrage, Buchung oder Verkaufsfolgewirkung.
- Alle acht Kategorien und alle 20 Einträge der nativen Verkaufstabelle sind
  abgedeckt. Normale Verkäufe einschließlich Popularitäts-, Notverkaufs-,
  Kaufreserve- und `sell_all`-Zweigen behalten die geprüfte Vanilla-Semantik.

**Gefundener und im Testmod korrigierter Fehler:** Der bisherige Konstruktor
installierte über `Commit()` bereits ausführbare Sprünge, erlaubte aber bis zum
späteren `Publish()` noch `Dispose()` und Speicherfreigabe. Eine Ausnahme bei der
Prüfung nach dem Schreiben konnte somit erreichbaren Code zurückpatchen bzw.
dessen Zustandsspeicher freigeben. Der erfolgreiche Spielstart hatte diesen
Fehlerpfad nicht durchlaufen.

Jetzt bereitet `InstallOnCommit=false` alle Hooks zunächst ohne Codeänderung vor.
`HookPublication` verwurzelt den vollständigen Kandidaten vor dem ersten `Enable`.
Sobald die Veröffentlichung begonnen hat, bleiben Code und Zustand bis Prozessende
erhalten, auch nach Teilinstallation, Backend-Ausnahme oder fehlgeschlagener
Nachprüfung. Der Aktivierungswert bleibt dann null; keine Wiederholungsinstallation.
Nur vollständig vorbereitete, noch nicht veröffentlichte Kandidaten dürfen aufräumen.
Ein Fehler der zusätzlichen Tick-Beobachtung deaktiviert keinen bereits aktiven Fix.

Die produktiven Generatoren wurden auf vollständige Instruktionsspannen,
Register-Liveness, Stack, Flags, SIMD, Mengenübergabe und Abbruchepilog geprüft.
Neue Fehlerpfadtests führen die echte Veröffentlichungsgrenze einschließlich
GC-Verwurzelung und nach Teilfehler weiterhin ausführbarem privatem Native-Code aus.
Normale und DEVDIAG-Testvariante bestehen jeweils 12.746 Prüfungen. Nachweise:
`.work/a/aisell-review/001/` im Workspace; frühere Nachweise bleiben unverändert.

Grenzen der Aussage: Die Sicherung verwendet Vanillas Gesamtbestand und ist kein
allgemeiner Reparaturmechanismus für bereits korrupte Lagerdaten. Fremde Mods,
die Warenabzüge später absichtlich ändern/überspringen oder Buchungen ersetzen,
benötigen eigene Verträge. Unabhängige Vanilla-Überläufe bei riesigen Preisen,
Goldständen oder Beständen werden nicht pauschal neu definiert. Spieltest und
fehlerfreies Startlog sind positive Evidenz; konkrete korrigierte Verkaufsbuchungen
wurden im installierten Normalbuild nicht aufgezeichnet. Save-/Editor-/echte
MP-Abnahme bleiben getrennte Prüfungen. Den speziellen nativen Mode-6-Abzug nicht
ohne weiteren Nachweis als allgemeinen Editorpfad bezeichnen.

## 2. Architekturentscheidung: gemeinsame Hookstellen in APIShared

**Empfehlung: ja, ein eigener gemeinsamer Dienst in `APIShared.Economy`.**
Begründete weitere Verbraucher sind KI-Wirtschaftsmods mit dynamischen
Warenreserven, Verkaufssperren für bestimmte Waren, Mengenbegrenzungen oder
Beobachtung der vorbereiteten Verkaufsmenge. Beide Stellen sind natürliche
Erweiterungspunkte dieser Funktionen; mehrere Mods dürfen dort nicht jeweils
eigene Inline-Hooks installieren.

Die vorhandenen `MarketPriceEvents` bleiben unverändert: Sie betreffen Preisabfragen
und auch Bewertungen von Verbündetentransfers. `Amount` ist dort unveränderlich;
ein Ersatzpreis null bricht den umschließenden Verkauf nicht ab. Damit sind sie
kein Ersatz für die beiden Sicherungen. `OnPlayerSubtractResource` liegt nach der
Goldgutschrift und ist ebenfalls zu spät. `NativeSiteRegistry` koordiniert Besitz,
vermittelt für sich allein aber keine gemeinsam ausführbaren Regeln.

### Neuer, additiver Vertrag

Unter dem vorgeschlagenen Namen `AiMarketSellPolicies` einen einzigen Dienst für
beide Hookstellen anlegen, mit einer Eigentümer-GUID und stabiler lokaler
Registrierungs-ID je Registrierung:

- `TryRegister(...)` liefert eine dauerhaft gehaltene Registrierung mit
  `SetEnabled(bool)`, zwei expliziten Sicherheitsoptionen
  `NonNegativeReserve` / `LimitQuantityToStock` und optionalen geordneten
  Anpassungscallbacks für Reserve bzw. Menge. Neue Registrierungen beginnen inaktiv.
- Kontext für die Reserve: validierte 1-basierte Spieler-ID, nativer Warenindex,
  gelesener Gesamtbestand, ursprüngliche effektive Reserve; nur die vorgeschlagene
  effektive Reserve ist veränderlich. Keine öffentlichen Register oder Pointer.
- Kontext für die Menge: Spieler-ID, Warenindex, Bestand und ursprüngliche
  angeforderte Menge; nur die vorgeschlagene Menge ist veränderlich. Dieser Punkt
  wird ausschließlich erreicht, wenn Vanillas vorheriger negativer-Mengen-Abbruch
  nicht gegriffen hat. Er ist kein Ereignis für jede abgelehnte KI-Entscheidung.
- Callback-Reihenfolge: numerische Ordnung, dann GUID und ID ordinal. Fehlerhafte
  Änderungen genau dieses Callbacks zurücknehmen und weitere Registrierungen
  fortsetzen; Fehlerzähler begrenzen. Rekursive Aufrufe aus Callbacks lösen keine
  erneute Benachrichtigung aus, behalten jedoch aktive Sicherheitsgrenzen.
- Erst optionale Anpassungen ausführen, danach die aktiv angeforderten
  Sicherheitsgrenzen anwenden. Keine spätere Registrierung kann die Grenzen eines
  anderen aktiven Verbrauchers aufheben. Menge null verwirft den Verkauf; eine
  Reserve mindestens so groß wie der Bestand lässt Vanilla zur nächsten Ware gehen.
- Sicherheitsoptionen aktiver Registrierungen logisch ODER-verknüpfen. Ein
  Verbraucher kann mit seinem Schalter nur die eigene Anforderung deaktivieren.
  Ohne aktive Anforderungen und ohne aktive Callbacks vollständiger Vanilla-Pfad.
- Callbacks laufen synchron auf dem nativen Aufruferthread, im normalen KI-Pfad
  im Simulationsthread: keine Noesis-/Unity-Arbeit, keine lokalen Hostentscheidungen,
  keine Netzwerkpakete oder ungebremste Logs. Alle Peers brauchen dieselbe Politik.

Kein öffentliches »Verkauf erfolgreich«-Post erfinden: Diese Inline-Stellen liegen
vor Buchung und Warenabzug. Ein readonly Ergebnis einer Mengenanpassung darf nur
als vorbereitete Menge bezeichnet werden. Ein später benötigter echter
Transaktionsbeobachter wäre eine eigene, vollständig zu auditierende Erweiterung.
Den vorhandenen Diagnose-Detour bei `0x29700` in dieser ersten Integration nicht
zusätzlich übernehmen oder umpatchen.

### Native Umsetzung und Lebensdauer

- APIShared besitzt beide Spannen exklusiv und beansprucht sie gemeinsam über seine
  vorhandene native Besitzregistrierung. BFQoL und fremde Verbraucher registrieren
  Regeln; sie schreiben selbst keine Bytes an diesen Stellen.
- Die vorhandene APIShared-Infrastruktur `NativeHookPublicationState` und
  `NativeHookPublication` verwenden; die lokale Testmod-Kopie nicht in das SDK
  übernehmen. Vorbereitung, Verwurzelung vor jedem Write, Nachprüfung und
  Fail-closed-Verhalten bleiben wie im korrigierten Testmod.
- Hash, Livebytes, komplette Spannen, Fortsetzungen, Epilog, Layout und tatsächlichen
  Backendvertrag prüfen. Referenz ist weiterhin SHA-256
  `FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
  Keine Migration fester Strukturadressen allein aufgrund eines Pattern-Treffers.
- Ohne zusätzliche Anpassungscallbacks die geprüften Sicherungen rein nativ ausführen.
  Keinen verwalteten Callback pro Warenkandidat nur für das BFQoL-Bool einführen.
  Gesondertes natives Bedarfsflag schaltet optionale Callback-Brücken frei.
- Die Callback-Brücke benötigt einen eigenen vollständigen Register-/Stack-/SIMD-
  und Flagvertrag sowie echte Backend-Ausführungstests. Nicht ungeprüft RedBirds
  Context-`Rflags` verwenden. Native Sicherheit funktioniert auch ohne Beobachter.
- Hooks beim Spielstart vorbereiten, nicht im ersten Kartentick. Logische Änderungen
  über atomar veröffentlichte Zustandsdaten; kein Uninstall bei Kartenwechsel,
  Startup-Cleanup, ausgeschalteter Mod oder Registrierungsfehler nach Write-Beginn.

Diese API ist additiv. Bestehende Interfaces nicht um neue Pflichtmember erweitern;
`MarketPriceEvents` behalten Signaturen und Verhalten. Katalog, Leitfaden,
öffentliche Verträge, Beispiel eines fremden Verbrauchers und API-Kompatibilitäts-
baseline gemeinsam ergänzen. Erst nach Festlegung der tatsächlich ausgelieferten
SDK-Version die BFQoL-Mindestversion konsistent in Plugin und Manifest setzen.

## 3. BFQoL: Host-Option unter »Fixes?«

- Neue boolesche Property `EnableAiSellThresholdFix`, mit Feldinitialisierung `true`
  und `[SyncHostOnly]`. Das bestehende `SetSetting`-, Preset-, Persistenz- und
  Synchronisierungssystem benutzen; keine neuen Pakete oder Companion-Arrays.
- Checkbox in den vorhandenen **Host**-Abschnitt
  `bugfixes.section.host-possible-fixes` unter `PossibleFixesTitleText` einfügen.
  Nicht die gleichnamige lokale Client-Kategorie oder den normalen KI-Fixes-Bereich
  benutzen. Bestehende `CanEditHostSettings`-/Opacity-Sperre erben.
- Stabiler Suchschlüssel `bugfixes.enable-ai-sell-threshold-fix`, eigene Titel- und
  Hilfetext-Bindings, Tooltipdauer 60000 ms und vorhandener Tooltipstil.
- Deutscher Titel: »KI-Verkäufe auf vorhandene Waren begrenzen«.
  Hilfetext: »Verhindert, dass KI-Lords durch negative Verkaufsschwellen Waren ohne
  Bestand verkaufen und dafür Gold erhalten. Begrenzte Verkaufsschwellen und
  Verkaufsmengen gelten für alle Warenkategorien. Vom Host festgelegt.«
  Englische Entsprechung und alle vorhandenen Locales ergänzen; andernfalls den
  englischen Text als expliziten Übersetzungsfallback verwenden.
- Registrierung unter BFQoLs eigener GUID und ID `ai-sell-threshold-safety`;
  beide Sicherheitsoptionen anfordern, keine eigenen Anpassungscallbacks.
  Wirksam genau bei `settings.EnableMod && settings.EnableAiSellThresholdFix`.
  »Host-Einstellung« bedeutet, dass alle Simulationsteilnehmer den synchronisierten
  Wert anwenden; den Fix niemals nur auf dem Hostprozess ausführen.
- Bei abgeschalteter Hauptmod oder Option die eigene Registrierung logisch
  deaktivieren. Falls kein anderer Verbraucher die Sicherheit anfordert, muss das
  beobachtbare Verhalten vollständig Vanilla entsprechen.
- Initialisierung im bestehenden dauerhaften LibraryLoaded-/WhenReady-Weg,
  Setting-Anwendung im vorhandenen zentralen Aktualisierungspfad. Nicht in einen
  zerstörbaren Plugin- oder kartenbezogenen Dispose-Pfad einhängen.
- Keine Laufzeitänderungen während einer Partie. Bestehende Einstellungen und
  Missionsinitialisierung entscheiden vor Simulationsbeginn. Der Bugfix gilt auch
  im Editor und in Vanilla-Missionen entsprechend BFQoLs Bugfix-Freigaben.
- Neue oder alte Presets ohne diese Property erhalten `true`; ausdrücklich
  gespeichertes `false` bleibt erhalten. Host-/Trail-Werte nicht in Client-Präferenzen
  zurückpersistieren. Diese Fälle im echten Presetsystem testen.

Betroffene Integrationspunkte sind das bestehende BFQoL-ViewModel, die zentrale
Runtime samt dauerhaftem Featureadapter, die aktuelle Settings-XAML und Locales.
Die Reihenfolge und Namen sind Navigationshinweise, keine dauerhaft angenommenen
internen Vertragsbedingungen. README nur auf ausdrücklichen weiteren Auftrag ändern.

## 4. Verträglichkeit und Übergang

- Fixes-Weizen-Kategoriehook `0x3F0C9..0x3F0DC`, BFQoLs Steinreservehook
  `0x3F156..0x3F16A`, APIShared-Preishooks und Diagnose-Einstieg
  `0x29700..0x29709` liegen außerhalb der beiden neuen Spannen. Gemeinsame Läufe
  trotzdem prüfen: Die neue Reservegrenze muss positiv berechnete Steinreserven
  erhalten; Fixes darf die Warenkategorie weiterhin bestimmen.
- Der neue API-Dienst aktiviert von sich aus keinen Fix. BFQoL wählt die Politik;
  APIShared bleibt für beliebige unabhängige Mod-GUIDs und ohne Modpack nutzbar.
- Den standalone Testmod während Entwicklung und Vergleich erhalten. Beim Wechsel
  auf die BFQoL-Integration seine Installation vor dem nächsten Spielstart gezielt
  deaktivieren/entfernen; Quellen und Nachweise erhalten. Keine parallelen Besitzer
  derselben Spannen. Ein belegtes Ziel liefert eine klare Fehlermeldung mit Besitzer,
  statt einen zweiten Hook zu installieren. Keine automatische Plugin-Löschung.
- Erst nach erfolgreicher Integration über eine Stilllegung des Testmods entscheiden.
  Keine Veröffentlichung, kein Versionssprung und keine fremden Quellenänderungen
  allein für diesen Plan.

## 5. Umsetzung und Abnahme

1. APIShared-Dienst samt Verträgen und Tests implementieren; eigenständige SDK-
   Prüfungen und öffentliche API-/Dokumentationschecks bestehen lassen.
2. Die produktiven nativen Generatoren und das ursprüngliche Vanilla-Reproduktions-
   harness nach APIShareds unabhängige Tests übertragen. Verbraucher dürfen keine
   SDK-Implementierungsquellen kompilieren. Den Standalone-Test als Vergleich erhalten.
3. Mehrfachregistrierung, verschiedene Reihenfolgen, OR-Kombination, Deaktivieren
   eines Verbrauchers bei weiter aktivem zweiten, Callbackfehler/Rollback, Rekursion,
   belegte Ziele, vorbereitete und teilweise veröffentlichte Fehlerfälle prüfen.
4. Native Fälle: alle Waren/Kategorien, negative/Null-/positive Schwellen,
   leere/teilweise Bestände, Integergrenzen, Notverkauf, Popularität, Mindestmengen,
   Kaufreservierungen und `sell_all`; gültige Verkäufe und deaktivierte Regeln gegen
   Original-Vanilla vergleichen. Stack, Flags, SIMD und vollständige Spannen ausführen.
5. BFQoL-Option integrieren und relevante HostClientPresetTests erhöht ausführen:
   Default true, fehlende Property, gespeichertes false, beide eigenen Presets,
   Reset, Hauptmod aus, Hostwert vor Clientbeitritt, abweichender Clientdefault,
   Client-Schreibsperre, Trail-Snapshot und Save-Wiederherstellung.
6. XAML-/Tooltip-/Suchzuordnung, Locale-Parität, CRLF, modbezogene Shared-Grenzen,
   dauerhafte Hooklebensdauer, Abhängigkeiten und normale/DEVDIAG-Artefakte prüfen.
   Nur betroffene Verbraucherprüfungen anschließen; keine fachfremde Gesamtsuite.
7. Gemeinsamer Spieltest mit Fixes und BFQoL-Steinreserve: Ale -999 ohne Bestand
   erzeugt kein Gold, vorhandene Ware wird korrekt verkauft, frühe leere Waren
   blockieren spätere nicht. An/aus jeweils vor neuer Partie; danach Save laden,
   zweite Karte, Editor sowie tatsächlicher Host/Client-Lauf mit gleicher Modversion.
8. Leistung separat messen: ohne Anpassungscallbacks kein zusätzlicher verwalteter
   Übergang und keine Allokationen je Warenkandidat; optionale Callbackpfade nach
   Warm-up gegen dieselbe Testszene messen. Keine FPS-Aussage aus statischen Tests.

Abnahme erst, wenn Standardverhalten und deaktivierter Vanilla-Pfad stimmen,
alle Peer-Buchungen übereinstimmen, kein Hook doppelt besitzt und kein Fehlerpfad
nach Veröffentlichungsbeginn Code oder Zustand freigibt. Einzelne Verkaufslogs
nur im DEVDIAG-Build ausgeben; beobachtete Vorbereitung und tatsächlich bestätigten
Warenabzug ausdrücklich unterscheiden.
