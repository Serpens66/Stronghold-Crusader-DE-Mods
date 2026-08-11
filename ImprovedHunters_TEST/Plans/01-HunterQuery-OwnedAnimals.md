# Feature 01 – Spielereigene Hühner als Jagdziele

## Arbeitsauftrag

Erlaube Jägern, Hühner unabhängig vom Besitzer als Beute auszuwählen, ohne die Hühner auf Besitzer Natur umzuschreiben. Entferne damit zugleich die Ursache für die unbegrenzte Neuproduktion der Kornkammer. Kühe bleiben von der Jägerlogik ausgeschlossen.

Dieses Dossier ist eigenständig. Vor Arbeitsbeginn dennoch den verbindlichen Ausgangszustand und die allgemeinen Qualitätsregeln in ../PLAN.md prüfen.

## Gewünschtes Verhalten

- Ist HuntChicken aktiv, darf die Hunter Query ein lebendes, nicht reserviertes Huhn jedes Besitzers berücksichtigen.
- Die Besitzer-/Farbwerte eines von einer Kornkammer erzeugten Huhns bleiben unverändert.
- Die Kornkammer darf durch die Mod keine unbegrenzten Ersatzhühner mehr erzeugen.
- Kaninchen, Kamel, Hirsch und Ziege behalten ihre vorgesehene Behandlung.
- Kühe werden nicht an die Hunter Query zugelassen.
- Auswahl und Bewertung laufen weiterhin über die bestehende Fleisch-pro-Zeit-Logik.
- Bei deaktivierter Mod ist das Verhalten vollständig Vanilla.

## Warum die aktuelle Lösung scheitert

ImprovedHuntersRuntime.OnUnitCreate und der wiederkehrende native Scan rufen für Hühner NeutralizePlayerOwnedChicken auf. Dabei werden Besitzer beziehungsweise Farbe auf 0 gesetzt. Erst dadurch gelangen die Tiere durch die Vanilla-Filter der Hunter Query. Für die Kornkammer wirken die Tiere anschließend nicht mehr als ihr Bestand, weshalb neue Hühner nachproduziert werden können.

Die bestehende Script-Extender-Callback OnUnitHunterQueryTarget ist zu spät. Sie wird an der nativen Typprüfung ausgelöst und kann nur Kandidaten verändern, die frühere native Filter bereits passiert haben.

## Bestätigter nativer Befund

Referenz-DLL:

- SHA-256 33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469
- Steam-Build 24651686
- Hunter-Query-Funktion RVA 0x18AF00
- Aufrufstelle VA 0x18012FD6C, Ziel VA 0x18018AF00
- Extender-Signatur an der Aufrufstelle: E8 ? ? ? ? 49 0F BF 8C 3E

Die native Kandidatenprüfung erfolgt in dieser Reihenfolge:

1. AliveState bei +0x88 muss 2, also IsAlive, sein.
2. Corpse-Flag bei +0x29C muss 0 sein.
3. Flags bei +0x92 müssen 0 sein.
4. Unit-Typ bei +0x8A muss Hirsch 0x2C oder Ziege 0x56 sein.
5. Reservierung bei +0x448 muss 0 sein.
6. Danach folgen Distanz-, Reichweiten-, Geometrie- und Pfadprüfungen.

Es gibt in diesem Teil keine ausdrückliche Besitzerabfrage. Spielereigene Hühner fallen wegen des Besitz-/Kontrollstatus in Flags +0x92 aus. Der bestehende Extender-Hook in BulkUnitDetours.cs sitzt erst an der Typprüfung und kann den früheren Flag-Filter nicht korrigieren.

## Geplante Architektur

Eine neue, modinterne Klasse HunterTargetEligibilityHook.cs setzt einen frühen Inline-Hook vor den Corpse-, Flag- und Typ-Ablehnungen. Sie darf ausschließlich die bekannte Kandidatenberechtigung erweitern und muss die nachfolgenden nativen Distanz-, Pfad- und Reservierungsprüfungen erhalten.

Als technisches Muster dient die atomare Zhuqiaomon-Transaktion. Die endgültige
Umsetzung verwendet wegen der x64-Mindestgröße von 14 Byte einen nativen
`AddInline`-Stub statt eines Context-Callbacks, mit:

- Iced.Intel und Zhuqiaomon.Extensions
- Zhuqiaomon.Hooks und Zhuqiaomon.Hooks.Transaction
- HookTransaction und HookRef<X64InlineHook>
- AddInline
- einem unmanaged Ein-Byte-Schalter für `EnableMod && HuntChicken`
- atomarer Apply-/Rollback-Pfad

Der genaue Hookpunkt, die Registerbelegung und beide Sprungziele müssen vor der Implementierung anhand eines kleinen Disassembly-Ausschnitts der kanonischen DLL bestätigt werden. Die oben genannten Filter reichen nicht aus, um Register oder überschreibbare Instruktionslänge zu erraten.

Die neue Entscheidung soll konzeptionell lauten:

1. Mod aktiv?
2. Kandidaten-ID und Rohslot plausibel?
3. Typ durch aktive Modsettings zugelassen?
4. Für dieses Feature: Huhn darf unabhängig von Flags beziehungsweise Besitzer passieren.
5. AliveState muss weiterhin IsAlive sein.
6. Reservation muss weiterhin 0 sein.
7. Kühe immer ablehnen.
8. Danach in Vanillas gemeinsame Distanz-/Pfadstrecke springen.

Nur die konkret validierten frühen Filter werden übersteuert. Unbekannte Typen, ungültige IDs, Layoutabweichungen und Callbackfehler müssen fail closed in Vanillas Pfad zurückfallen.

## Umsetzungsschritte

### 1. Präflight

- Git-Status und vorhandene Benutzeränderungen prüfen.
- Hash der installierten CrusaderDE.dll prüfen.
- aktuellen Mod- und Extender-Stand dokumentieren.
- ImprovedHunters.csproj und verfügbare Zhuqiaomon-Referenzen prüfen.

### 2. Hookpunkt validieren

- Rizin-Wrapper mit iI-Smoke-Test verwenden.
- Nur Hunter Query RVA 0x18AF00 analysieren.
- Frühen gemeinsamen Kandidatenpunkt, relevante Register, abgelehnte Sprungziele und Eintritt in den gemeinsamen Geometriepfad dokumentieren.
- Native ID-zu-Slot-Auflösung gegen die im Mod bestehende Unit-Manager-Logik validieren.
- Referenzbytes und ein für abweichende DLLs geeignetes, eindeutiges Section-bounded Pattern bestimmen.

### 3. Diagnostische Hookfassung

- Zunächst keine Verhaltensänderung.
- Einmal hook confirmed mit Jäger-ID, Kandidaten-ID, Typ, Besitzer/Flags, AliveState und Reservation loggen.
- Mindestens je einen Vanilla-Hirsch/Ziege- und Huhn-Kandidaten beobachten.
- Ungültige Kontexte einmal als Fehler loggen, danach drosseln.
- Callback darf Vanilla nie verhindern.

### 4. Verhaltensänderung

- HunterTargetEligibilityHook modular implementieren und aus ImprovedHuntersRuntime.Apply initialisieren.
- Kein Dispose in OnDisable oder OnDestroy.
- OnUnitCreate-Neutralisierung und NeutralizePlayerOwnedChicken vollständig entfernen, nicht als veralteten Fallback behalten.
- Den wiederkehrenden Scan von der Besitzerumschreibung bereinigen.
- Bestehende OnUnitHunterQueryTarget-Bewertung für die Zielrangfolge zunächst weiterverwenden, soweit sie nach dem frühen Hook zuverlässig erreicht wird.
- IsOwnerAllowed-Hilfen eindeutig implementieren oder entfernen, wenn sie nur immer true zurückgeben.
- Kühe explizit ausgeschlossen lassen.

### 5. Dokumentation und Metadaten

- UpdateToNewDLL.md um Hash, Hunter-Query-RVA, Hook-RVA, Referenzbytes, Pattern, Registersemantik und Updateprüfung ergänzen.
- info.json erst bei fertigem Feature mit Versions- und Changelog-Eintrag aktualisieren.

## Laufzeittests

Mindestens folgende Fälle auf einer kontrollierten Karte testen:

| Fall | Erwartung |
|---|---|
| HuntChicken aus | Huhn wird nicht durch die Mod ausgewählt |
| spielereigenes Huhn, HuntChicken an | Jäger kann es auswählen |
| neutrales Huhn | Jäger kann es auswählen |
| gegnerisches Huhn | Jäger kann es auswählen |
| Huhn bereits reserviert | kein zweiter Jäger wählt es |
| Kuh in Reichweite | kein Jagdziel |
| nur Hühner auf der Karte | Jäger arbeitet, sofern Hütte gebaut werden kann |
| Kornkammer erzeugt Hühner | Besitzer bleibt unverändert, keine modbedingte Endlosschleife |
| Mod deaktiviert | Vanilla-Verhalten |

Zusätzlich prüfen:

- Fleischmenge entspricht ChickenMeat.
- Keine doppelten Reservierungen.
- Bestehende Abschuss-, Despawn- und Pickup-Korrekturen funktionieren.
- Kein Callbackfehler im BepInEx-Log.
- Hook-confirmed-Marker enthält plausible IDs und Zustände.

## Abnahmekriterien

- Kein Codepfad schreibt Hühner allein für die Jagd auf Natur um.
- Hühner aller Besitzer erreichen die bestehende Zielbewertung.
- Native Reservierungs-, Distanz- und Pfadlogik bleibt erhalten.
- Kühe bleiben ausgeschlossen.
- Hook ist hash-/signaturgesichert, fail closed und bei deaktivierter Mod inaktiv.
- UpdateToNewDLL.md und Testprotokoll sind vollständig.
- Alle jeweils möglichen Prüfungen sind vor dem Abschlussbuild erledigt. Nach
  einem Buildfehler oder einer testbedingten Code-/Projektkorrektur darf
  `build.bat` erneut laufen, bis Build und Installation erfolgreich sind;
  identische Wiederholungen ohne neue Diagnose oder Änderung unterbleiben.

## Übergabe an Feature 02

Feature 02 erweitert denselben frühen Hook um tote, abholbare Kandidaten. Deshalb am Ende hier festhalten:

- endgültiges Hook-RVA und überschriebene Instruktionen
- Registerbelegung
- Name und Signatur der zentralen Eligibility-Methode
- Verzweigungsziele für Vanilla-Ablehnung und gemeinsamen Pfad
- bestätigte ID-/Slotformel
- bekannte Alive-/Corpse-/AI-Zustände aus dem Test

## Ergebnisse und offene Punkte

Bearbeitet am 11. August 2026. Ausgangscommit war
`87125f27cc813b75c7e1aabdf8e2fd8768f46ed8`. Vor Arbeitsbeginn waren
`ImprovedHunters/PLAN.md` und `ImprovedHunters/Plans/` unversioniert; diese
Benutzerdateien wurden erhalten. Der Benutzer übernahm die Pläne zunächst als
Commit `aa71ff271befc25efe899931e9089ede445e8bd2` (`237`) und die erste
Featurefassung anschließend als Commit
`772ff84ca488e917b4a09127d1579305f6fa53cb` (`238`). Die nach dem ersten
In-Game-Test ergänzten Diagnosen liegen als Arbeitsbaumänderungen auf diesem
Commit. Der kanonische Script-Extender-Fork war und blieb sauber auf `main` bei
`368124119be230306f3f2593efa2a270b0e3dfb1` / Tag `v1.40.0`; dort wurde
nichts geändert.

Bestätigter Ausgangsstand:

- Modversion vor der Änderung: `1.1.20`; Featureversion: `1.1.21`.
- Installierte kanonische DLL: 3.450.880 Bytes, SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
- Rizin-`iI`-Smoke-Test und gezielte Analyse ausschließlich gegen diese DLL
  waren erfolgreich.

Implementierung:

- Neue Klasse `src/HunterTargetEligibilityHook.cs`.
- Semantischer Pattern-Anker: RVA `0x18AF70`, genau ein Treffer in den
  ausführbaren PE-Sektionen; eigentlicher Hook: RVA `0x18AF88`.
- Überschriebener Block: 14 Byte aus
  `cmp word [rbx - 0x20A], 0` und dem folgenden `jne 0x18B08D`.
- Ein nativer Inline-Stub bildet Vanillas Flagvergleich und Ablehnung exakt
  nach. Nur bei nichtnull Kontrollwort und Typ Huhn liest er einen unmanaged
  Ein-Byte-Schalter, der `EnableMod && HuntChicken` abbildet. Eine zusätzliche
  Besitzerprüfung gibt es ausdrücklich nicht: Besitzer 0 und alle Spielerwerte
  folgen unabhängig vom vollständigen Wort `+0x92/+0x93` derselben Route. Ist
  der Schalter gesetzt, geht der Kandidat zur bestehenden Typprüfung;
  anderenfalls zum Vanilla-Ablehnungsziel.
- Vanillas Ablehnungsziel ist `0x18B08D`; der bestehende Typcallback beginnt bei
  `0x18AF96`, die Reservierungsprüfung bei `0x18AFAD` und die gemeinsame
  Distanz-/Geometrie-/Pfadstrecke bei `0x18AFBB`.
- Registersemantik: `R14` = Unit Manager, `R13 = R14 + hunterId * 0x490`,
  `ESI` = 1-basierte Kandidaten-ID und `RBX` = Kandidat `+0x29C`. Der Stub
  verwendet `RAX` nur kurz als Pointer auf den Ein-Byte-Schalter; beide
  Sprungpfade überschreiben `EAX` vor einer beobachtbaren Verwendung.
- Bestätigte Slotformel:
  `GameUnitArray = UnitManager + 0xAEC` und
  `GameUnit(id) = GameUnitArray + (id - 1) * 0x490`. Zusätzlich ist der
  Jägerslot `R13 + 0x65C` und der Kandidat `RBX - 0x29C`.
- Zentrale Eligibility-Erzeugung:
  `GenerateEligibilityFilter(Assembler, ReadOnlySpan<Instruction>, ulong, ulong, ulong, ulong)`.
  Sie validiert beim Hookaufbau exakt zwei überschriebene Instruktionen mit
  Längen `8 + 6` sowie das gemeinsame Rücksprungziel `0x18AF96`.
- Hash-, Pattern- oder Instruktionsabweichungen verhindern die Hookinstallation
  atomar. Zur Laufzeit bleibt Mod-aus identisch zu Vanilla.
- Einmalige `hook confirmed`-Logs sind für Vanilla-Hirsch/Ziege und Huhn
  vorhanden; ein eigener Marker protokolliert das erste tatsächlich durch den
  Flagfilter zugelassene Huhn mit Besitzer/Farbe, IDs, Zustand und Reservierung.
- `OnUnitCreate`, der Scan-Aufruf und `NeutralizePlayerOwnedChicken` wurden
  vollständig entfernt. Es existiert kein Jagdcodepfad mehr, der Besitzer oder
  Farbe eines Huhns auf 0 schreibt. Die drei immer `true` liefernden
  Owner-Allow-Helfer und ihre redundanten Prüfungen wurden ebenfalls entfernt.
- Kühe bleiben durch `IsRuntimeHuntingEnabled` ausdrücklich ausgeschlossen.
- Bei deaktivierter Mod nimmt der Stub für alle nichtnull Flags das Vanilla-
  Ablehnungsziel; die bestehende Typbewertung greift ebenfalls nicht ein. Das
  Verhalten bleibt Vanilla.

Statische Prüfungen:

- Referenzpattern am RVA und Eindeutigkeit in ausführbaren PE-Sektionen:
  bestanden (`1` Treffer bei `0x18AF70`).
- Rizin-Instruktionsgrenze und Kontrollfluss am Hook: bestanden.
- Owner-/Farb-Schreibaudit, tote Owner-Helfer, Kuh-Ausschluss, Mod-aus-Schranke,
  Reservierungsprüfung und Projektaufnahme der neuen Datei: bestanden.
- `UpdateToNewDLL.md` enthält Hash, RVA, Pattern, Register, Slotformel,
  Branchziele und Updateprüfung.
- Der erste `build.bat`-Lauf erreichte den Compiler, brach
  aber vor Ausgabe und Installation mit `CS0012` ab, weil Zhuqiaomons öffentliche
  Hooksignatur `Iced.Intel.Instruction` exponiert. Der in den lokalen
  Hook-Mods bereits verwendete `Iced.dll`-Projektverweis wurde daraufhin ergänzt.
- Die anschließende vertiefte Prüfung erkannte vor einem erneuten Build zwei
  weitere Fehler der ersten Fassung: Zhuqiaomon überschreibt auf x64 mindestens
  14 statt 8 Byte, und der zunächst notierte Hühnerwert war falsch. Die
  endgültige Fassung verwendet deshalb den oben beschriebenen 14-Byte-Inline-
  Stub und den kompilierten Enumwert `eChimps.CHIMP_TYPE_CHICKEN` (`0x3E`).
- Der abschließende Feld- und Effizienzaudit trennt den nativen
  16-Bit-Vanillavergleich von der Feldsemantik: `+0x92` ist das Besitzerbyte,
  `+0x93` bleibt unbekannt. Gemäß Featureziel wird das vollständige Wort für
  jedes Huhn übergangen, ausdrücklich auch für Besitzer 0. Im Kandidaten-
  Hot-Path gibt es keinen Managed-Callback, keine Allokation und keine
  Owner-/Farbschreiboperation.
- Der korrigierte `build.bat`-Lauf war erfolgreich (`0` Warnungen, `0` Fehler)
  und installierte Version `1.1.21`. Lokale und installierte SHA-256-Werte sind
  für DLL, PDB und `info.json` identisch; die DLL hat SHA-256
  `FED180FBAB7FB81EA500E659151E7EED12DBE4C1B20E1BBC41D2555EAC705088`.
- Ein anschließender `ilspycmd`-Audit des abschließenden Buildartefakts bestätigte
  `AddInline`, den kompilierten Hühnerwert `62`, den Word-Vergleich und die
  Lebensdauerverwaltung des unmanaged Schalters.
  Die entfernten Symbole `NeutralizePlayerOwnedChicken`, `OnUnitCreate` und
  `OwnerAllowed` sind auch in der gebauten DLL nicht mehr vorhanden. Der Hooktyp
  enthält weder einen Owneroffset noch einen Zugriff auf
  `r_ControllableForPlayerId`.
- Die erneute Gesamtprüfung ergänzte außerdem einen Rollback um
  `ImprovedHuntersRuntime.Apply`: Schlägt nach installierter Hook eine spätere
  Initialisierung fehl, werden Hook, Subscriptions und bereits angewandte
  Runtimepatches wieder abgebaut, statt einen halb aktiven Modzustand zu lassen.
- Nach dem ersten In-Game-Test wurden sämtliche neuen Featurediagnosen als
  `Info` beziehungsweise bei Diagnosefehlern als `Error` ausgeführt. Der zuvor
  einzige `Debug`-Lifecyclemarker in `OnDestroy` ist jetzt ebenfalls ein
  zeitgestempelter `Info`-Marker. Ein statischer Log-Level-Audit fand danach
  weder `Debug`- noch `Trace`-Logging im Modquellcode.
- Die neue Diagnose verfolgt nur Jäger, die den Hunter-Query-Callback erreicht
  haben. Sie protokolliert Änderungen von AI-Zustand, Position, nativem
  Zielslot/-Global-ID, Zielidentität, Gesundheit, Flags, Corpse-Flag und
  Reservierung. Akzeptierte Hühner werden mit denselben relevanten Zuständen
  bis zu einem festen Limit von 96 Identitäten beobachtet. Projectile-Spawn,
  Projectile-Damage und Projectile-Kill sowie Pickup und Dropoff besitzen
  eigene Marker. Zielabbrüche haben ein vom Query- und Lifecycle-Logging
  unabhängiges Kontingent, damit sie nicht erneut durch Kandidatenlogs
  verdrängt werden. Fehler im Watch-Logger deaktivieren nur diese Diagnose,
  leeren deren begrenzte Caches und lassen den funktionalen Native-Scan
  ausdrücklich weiterlaufen.
- Der erste Diagnosebuild deckte `CS0136` durch zwei gleich benannte lokale
  Zielvariablen auf und installierte nichts. Nach eindeutiger Umbenennung und
  wiederholter CRLF-/Diff-/Sicherheitsprüfung war der gemäß Wiederholungsregel
  erneut ausgeführte Build erfolgreich (`0` Warnungen, `0` Fehler).
- Lokale und installierte Diagnose-DLL sind bytegleich mit SHA-256
  `A0DCAF37C172BA43BB388C43B3C9EE1662D6DB57246E3ADED1D8C0EC2CF79163`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte die Movement-, Damage- und
  Kill-Abonnements sowie alle neuen Marker. Die alten Symbole
  `NeutralizePlayerOwnedChicken`, `OnUnitCreate` und `OwnerAllowed` fehlen auch
  weiterhin im gebauten Artefakt.

Laufzeitteststatus:

- Ein erster In-Game-Test mit Version `1.1.21` fand statt. Der Jäger bewegte
  sich sichtbar auf eigene Hühner zu, brach die Jagd aber wieder ab.
- Das BepInEx-Log bestätigt die frühe Hookfunktion: Der Hook wurde bei RVA
  `0x18AF88` aktiv initialisiert. Kandidat `98` war ein lebendes Huhn von
  Besitzer 2 mit `flags92=2` und Reservierung 0; der Marker
  `Hunter-query nonzero-flags chicken admitted` erschien. Die bestehende
  Bewertung ließ unter anderem die Hühner `142`, `157` und `163` zu und wählte
  `163` als bestes Ziel. Damit liegt der beobachtete Fehler nach frühem
  Flag-/Typfilter und Managed-Zielbewertung.
- Im selben Lauf erschien kein Shot-Intent-, Projectile-, Damage-, Kill-,
  Pickup- oder Dropoff-Marker. Der damalige gemeinsame Grenzwert von 160
  Kandidatenmeldungen war bereits ausgeschöpft und unterdrückte den späteren
  Abortmarker; dieser Diagnosefehler ist mit den getrennten Kontingenten der
  neu installierten Fassung behoben.
- Ein weiterer interaktiver Test mit der Diagnose-DLL ist erforderlich. Die
  maßgeblichen Marker sind `hunter lifecycle` mit den Quellen
  `native-target-acquired`, `movement-post` und `native-target-cleared`,
  `target abort`, `watched chicken state` sowie `chicken projectile event` mit
  `spawn-post`, `damage-pre` oder `kill-pre`. Fehlt bereits
  `native-target-acquired`, scheitert die Übergabe aus der Query; wird das Ziel
  danach bei unverändert lebendem Huhn geräumt, liegt eine spätere
  Hunter-State-/Zielvalidierung nahe. Ein Spawn ohne Damage grenzt den Fehler
  auf Projektil/Geometrie ein, Damage/Kill ohne Pickup auf den Kadaverpfad.
- Die übrigen kontrollierten Kartenfälle aus der Tabelle bleiben offen,
  insbesondere Besitzer 0, gegnerischer Besitzer, bestehende Reservierung,
  unveränderte Kornkammer-Besitzwerte und Nachproduktion, Fleischmenge sowie
  Mod-aus-Verhalten.

Auswertung des zweiten Diagnose-Testlaufs und Folgekorrektur:

- Das Log vom 11. August 2026 enthielt weder globale Fehler noch Callback-,
  Diagnose-, Projektil-, Pickup- oder Dropofffehler. Es zeigte vier reale
  Abbrüche nach erfolgreicher nativer Zielzuweisung: Die Jäger bewegten sich
  zu den lebenden Hühnern, Ziel-ID und Global-ID blieben konsistent,
  Reservierung `2` und Gesundheit blieben unverändert, danach wechselte der
  Jäger nach 17 bis 121 ms in Zustand `6` und räumte das Ziel. Es wurde kein
  Projektil erzeugt.
- Die gezielte Rizin-Analyse der kanonischen DLL bestätigte den vollständigen
  Pfad: Die Auswahl schreibt Ziel-ID/-Global-ID in Jäger `+0x39A/+0x39C`,
  Reservierung `2` in Ziel `+0x448` und Jägerzustand `1`. Im Nahbereich ruft
  der Zustandszweig die Query erneut auf. Weil Hühner ihre Reservierung vor
  dieser zweiten Query nicht wie Vanillas Beutetiere freigeben, scheitert die
  Reservierungsprüfung bei RVA `0x18AFAD`; Rückgabe `0` führt beim Aufrufer an
  RVA `0x12FF5F` zu Zustand `6`. Dies erklärt exakt das beobachtete
  Hinbewegen-und-Abbrechen.
- Der bestehende Native-Hook besitzt nun einen zweiten, ebenfalls
  hash-/instruktionsgesicherten Stub bei RVA `0x18AFAD`. Reservierung `0` folgt
  unverändert Vanilla. Reservierung `2` darf nur ein aktiviertes Huhn
  passieren, dessen Kandidaten-ID und Global-ID exakt mit dem bereits im
  aufrufenden Jäger gespeicherten Ziel übereinstimmen und dessen Jäger noch im
  nativen Verfolgungszustand `1` ist. Andere Jäger,
  anderweitig reservierte Tiere und wiederverwendete Slots bleiben abgelehnt.
- Die Managed-Bewertung erkennt denselben eng begrenzten Retarget-Fall und
  behält ihn unabhängig vom inzwischen abgelaufenen Pathfinding-Cache bei. Ein
  einmaliger Info-Marker `retained the Hunter's reserved chicken` bestätigt
  ihn im nächsten Test.
- Der Loglauf enthielt außerdem einen ungültigen vom bestehenden
  Script-Extender-Callback gelieferten Jägerwert `657880064` (`0x27367400`).
  Der Mod validiert deshalb nun Jäger-ID, Alive-State und Typ vor jeder
  Featureentscheidung. Bei ungültigem Kontext bleibt `IsValidTarget`
  unangetastet; Hühner bleiben damit über die Vanilla-Grundbewertung
  fail-closed, statt durch den bisherigen Managed-Fallback irrtümlich
  zugelassen zu werden. Am Script Extender wurde nichts geändert.
- Der erneute statische Audit bestätigte den kanonischen DLL-Hash und die
  Größe, genau einen ausführbaren Pattern-Treffer bei RVA `0x18AF70` sowie die
  beiden vollständigen 14-Byte-Instruktionsgrenzen bei `0x18AF88` und
  `0x18AFAD`. Quellcodeprüfungen bestätigten Mod-aus-Schranke, Jägerzustand `1`,
  ID-/Global-ID-Gleichheit, Reservierung `2`, lebendes Huhn ohne Corpse-Flag,
  Kuh-Ausschluss, fehlende Owner-/Farbschreibzugriffe und ausschließlich
  Info-/Error-Featurelogging. Alle betroffenen Textdateien wurden ordinal auf
  CRLF verifiziert.
- `build.bat` war nach den Prüfungen erfolgreich (`0` Warnungen, `0` Fehler)
  und installierte weiterhin Version `1.1.21`. Lokale und installierte DLL,
  PDB und `info.json` sind bytegleich. Die neue DLL hat SHA-256
  `A09BA95ECF25DD197D7EF32BF2BFE401A6FA5AD4B41F2863AB6129CBA2D4E907`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte beide `AddInline`-Aufrufe,
  deren gemeinsame Transaktionserfolgskontrolle, Zustands-, ID- und
  Global-ID-Vergleiche sowie Managed-Retarget- und Fail-closed-Pfade im
  tatsächlich gebauten Artefakt.

Zu diesem Zeitpunkt war ein interaktiver Test der Reservierungsbehandlung noch
offen. Der nachfolgende dritte Test hat diese Arbeitshypothese für den aktuell
reproduzierten Abbruch widerlegt und ersetzt den hier beschriebenen erwarteten
Pfad. Die übrigen Matrixfälle (Besitzer 0, Gegner, zweiter Jäger, Mod aus,
Kornkammer-Nachproduktion und Fleischmenge) bleiben weiterhin abzunehmen.
Feature 01 ist noch nicht endgültig freigegeben; in diesem Chat erfolgte
ausdrücklich keine Extender-Änderung.

Auswertung des dritten Testfalls und korrigierter nativer Befund:

- Die ersten beiden Spiele enthielten näher beziehungsweise wirtschaftlich
  besser bewertete andere Tiere und testeten den Hühnerabschluss daher nicht.
  Im dritten Spiel wurde das lebende eigene Huhn `148/7332016` bei Distanz `7`
  tatsächlich ausgewählt. Besitzer `1`, Flagswort `1`, Identität und
  Reservierung `2` waren plausibel und konsistent.
- Nach der echten Zielübernahme erschien weder ein weiterer Hunter-Query-
  Callback noch der Marker `retained the Hunter's reserved chicken`. Der
  Reservierungs-Hook bei `0x18AFAD` wurde in diesem Abbruchpfad nicht erreicht.
  Die unmittelbar vor der Zielübernahme doppelt sichtbare Kandidatenfolge war
  die zweite Radiusrunde der initialen Query. Damit ist die oben dokumentierte
  Reservierungsursache für den aktuell reproduzierten Fehler widerlegt; der
  zweite Filter bleibt nur als eng begrenzte defensive Behandlung eines
  tatsächlich auftretenden Requery-Falls bestehen.
- Der Jäger wechselte 20 ms später von Zustand `1` nach `6`, räumte sein Ziel,
  ließ das Huhn aber lebend und reserviert. Es gab weiterhin keinen Projektil-,
  Damage-, Kill-, Pickup- oder Dropoffmarker. In diesem Prozess wurden nur
  plausible Callback-Jäger-IDs (`1`) beobachtet; der separate Extenderfehler
  war an diesem Abbruch nicht beteiligt.
- Die erneute Rizin-Analyse fand den passenden späteren Writer: Zustand `1`
  setzt Befehl `4`, ruft bei RVA `0x13013D` die generische Unit-Order-Funktion
  RVA `0x18E950` auf und verlangt sowohl einen Rückgabewert ungleich null als
  auch Hunter-Byte `+0x3FE == 0`. Erfolg schreibt Zustand `9` bei `0x130163`;
  andernfalls beginnt bei `0x130171` der Pfad, der Zustand `6` bei `0x130191`
  schreibt. Ein erfolgreicher Hirschlauf nutzte denselben Pfad und erreichte
  bei Distanz `5` Zustand `9`.
- Eine rein diagnostische, hash- und instruktionsgesicherte Inline-Hook bei
  RVA `0x130171` protokolliert nun auf Info den noch in `EAX` vorhandenen
  Unit-Order-Rückgabewert sowie Hunter `+0xF2`, `+0xF4`, `+0x398` und `+0x3FE`,
  Zielidentität, Besitzer, Reservierung und Distanz. Danach bildet sie die drei
  überschriebenen Instruktionen mit zusammen 18 Byte exakt nach. Die Diagnose
  ändert weder Entscheidung noch Zustand; bei deaktivierter Mod oder
  Hühnerjagd überspringt eine native Ein-Byte-Schranke den Managed-Callback
  vollständig. Der nächste Test muss mit dem Marker
  `Improved Hunters chicken state-6 branch` klären, ob die generische
  Befehlsfunktion fehlschlägt oder erst `+0x3FE` den Erfolg verwirft.
- Der zuvor beobachtete Fantasie-Jägerwert ist unabhängig davon ein bestätigter
  Script-Extender-Bug. Der Hook bei RVA `0x18AF96` liest über
  `[rsp + 0xF0]` den vom nativen Prolog gespeicherten Caller-`RBX` statt des
  ursprünglichen zweiten Arguments `EDX`. Ein veröffentlichungsfertiger
  englischer Bericht liegt in
  `ImprovedHunters/ScriptExtender-HunterQuery-HunterId-BugReport.md`. Der
  kanonische Script-Extender blieb unverändert.
- Das neue exakte Diagnosepattern hat in den ausführbaren PE-Sektionen genau
  einen Treffer bei RVA `0x130171`. Rizin bestätigte die überschriebenen
  Instruktionslängen `7 + 6 + 5`. Diff-, Altcode-, Owner-Write-, Log-Level- und
  CRLF-Audits waren erfolgreich.
- Der erste Diagnosebuild war bereits mit `0` Warnungen und `0` Fehlern
  erfolgreich. Der nachgelagerte Effizienzaudit ergänzte anschließend die
  native Ein-Byte-Schranke, damit Mod aus/Hühnerjagd aus keinen Managed-
  Diagnosecallback ausführt. Gemäß der in allen Plänen korrigierten
  Wiederholungsregel wurde `build.bat` nach dieser testbedingten Codekorrektur
  erneut ausgeführt; auch dieser Build hatte `0` Warnungen und `0` Fehler und
  installierte Version `1.1.21`.
- Lokale und installierte DLL, PDB und `info.json` sind bytegleich. Die
  endgültige DLL hat SHA-256
  `BE71E1207E9D3A23243B183C7FC300A66EEB03FDB9F7A5476AF911BA1FE32B4A`.
  Der abschließende `ilspycmd`-Audit bestätigte Patternauflösung, dritten
  `AddInline`-Hook, native Aktivierungsschranke, registererhaltenden Callback,
  die drei nachgebildeten Instruktionen sowie den Info-Marker im tatsächlich
  gebauten Artefakt.

Auswertung des vierten Testlaufs und nächste Eingrenzung:

- Im ersten Spiel wurden zwei eigene, lebende Hühner bei Distanz `10` und `11`
  korrekt ausgewählt, als Ziel mit passender ID/Global-ID übernommen und mit
  Reservierung `2` geführt. Die Jäger wechselten dennoch nach `10` bzw. `21` ms
  von Zustand `1` zu `6` und räumten das Ziel. Die Hühner blieben lebendig und
  reserviert; es gab keinen Schuss.
- Der Marker des späteren Hooks bei RVA `0x130171` hatte in beiden Fällen null
  Treffer. Damit ist die Hypothese, dass der Writer bei `0x130191` den aktuell
  reproduzierten Abbruch auslöst, widerlegt. Der Hook bleibt vorerst als
  Vergleichsmarker installiert, verändert aber kein Verhalten.
- Das zweite Spiel lieferte einen erfolgreichen Kamel-Kontrollfall mit der
  vollständigen Kette Zielwahl, Annäherung, Zustand `9`, Schuss, Tod/Kadaver,
  Aufnahme und Fleischabgabe. Der allgemeine Jäger-, Projektil- und Kadaverpfad
  funktioniert; der offene Fehler ist hühnerspezifisch.
- Rizin bestätigte erneut den separaten frühen Query-Nullpfad: Aufruf der Query
  bei `0x12FF2E`, Nulltest bei `0x12FF33`, Sprung nach `0x12FF53` und Schreiben
  von Zustand `6` bei `0x12FF64`. Ein neuer verhaltensneutraler Inline-Hook bei
  `0x12FF53` protokolliert auf Info nur dann
  `chicken query returned zero before state 6`, wenn das gespeicherte Ziel noch
  als Huhn auflösbar ist. Er bildet die überschriebenen `5 + 7 + 5` Byte exakt
  nach und ist über denselben nativen Enable-Byte bei Mod aus/Hühnerjagd aus
  callbackfrei.
- Der Reservierungs-Stub protokolliert nun ausschließlich für aktivierte
  reservation-2-Hühner den Marker `reserved-chicken filter` mit transientem
  Jägerzustand, Ziel-ID/Global-ID, allen Gleichheitsprüfungen und `willAllow`.
  Der häufige Reservation-0-Pfad bleibt ohne zusätzlichen Managed-Callback.
- Die kanonische DLL ist weiterhin Steam-Build `24651686`, Größe `3.450.880`
  Byte und SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
  Der vorgeschriebene Rizin-`iI`-Smoke-Test sowie die gezielte erneute
  Disassembly beider Hookbereiche waren erfolgreich. Besitzer-/Farbumschreibung
  bleibt vollständig entfernt; der Script Extender wurde nicht geändert.
- Ein erneuter Ingame-Test mit Version `1.1.22` ist offen. Er muss die drei
  Marker `reserved-chicken filter`, `chicken query returned zero before state 6`
  und `chicken state-6 branch` gemeinsam auswerten. Erst deren Kombination
  entscheidet belastbar, ob die frühe Query erneut läuft, ob sie das reservierte
  Ziel am Filter zulässt und welcher Writer den Zustandswechsel tatsächlich
  ausführt. Die übrigen Abnahmematrixfälle bleiben ebenfalls offen.
- Die abschließenden statischen Prüfungen bestätigten je genau einen Treffer der
  neuen Query-Nullsignatur bei RVA `0x12FF53` und der Vergleichssignatur bei RVA
  `0x130171`, passende `5 + 7 + 5`- beziehungsweise `7 + 6 + 5`-Grenzen,
  fehlende Owner-/Farbschreibzugriffe, ausschließlich Info-/Error-Featurelogs,
  gültiges JSON, sauberen Diff und einen unveränderten Script-Extender-
  Arbeitsbaum. `build.bat` baute und installierte Version `1.1.22` mit
  `0` Warnungen und `0` Fehlern. Lokale und installierte DLL, PDB und `info.json`
  sind bytegleich; die DLL hat SHA-256
  `13C1E713BBE65E068D96B5C1D9CBFDA10433DF456DF49EFAD40F091189044B90`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte im tatsächlichen Artefakt vier
  gemeinsam abgesicherte `AddInline`-Hooks, die native Ziel-vorhanden-Schranke,
  beide registererhaltenden Diagnosecallbacks, die Vergleichsreihenfolge und
  das vollständige Instruktions-Replay. Die Warnung von `ilspycmd`, dass eine
  geringfügig neuere Toolversion verfügbar ist, beeinflusste die Dekompilierung
  nicht.

Auswertung des fünften Testlaufs und erweiterte Ein-Lauf-Diagnose:

- Der relevante Hühnerfall trat erneut viermal auf. Die Jäger übernahmen
  lebende Hühner bei Distanzen `14`, `16`, `19` und `20`, wechselten jedoch
  bereits nach `8` bis `22` ms von Zustand `1` zu Zustand `6` und löschten die
  Ziel-ID. Die Hühner blieben lebend und mit Reservierung `2` zurück. Es gab
  keinen Schuss, Schaden, Kill oder Pickup.
- Weder `chicken query returned zero before state 6` noch
  `chicken state-6 branch` erschien. Die späteren
  `reserved-chicken filter`-Marker betrafen erst Folgesuchen nach dem Abbruch:
  Der Jäger war dann Zustand `0`, hatte kein Ziel mehr und wurde deshalb
  korrekt abgelehnt. Diese Reservierungsprüfung verursacht den initialen
  Abbruch nicht; sie zeigt zusätzlich die verwaiste Reservierung `2` als
  Folge des Abbruchs.
- Die Nulltreffer der beiden Branchmarker waren noch kein belastbarer Beweis,
  dass die Branches nicht liefen: Beide Managed-Callbacks verlangten bislang
  ein zu diesem Zeitpunkt noch im Jäger gespeichertes Hühnerziel. Vanilla kann
  die Ziel-ID jedoch unmittelbar vor dem Zustandswriter löschen; der frühe
  Query-Null-Stub übersprang den Callback in diesem Fall sogar bereits nativ.
  Diese Diagnoseblindheit ist in Version `1.1.23` entfernt.
- Jede akzeptierte Hühnerquery hinterlegt nun für höchstens zehn Sekunden die
  stabile Kombination aus Jäger-ID, Hühner-ID und Hühner-Global-ID im
  Diagnosehook. Die beiden verhaltensneutralen State-6-Callbacks dürfen damit
  auch nach einer nativen Zielräumung den zuvor akzeptierten Kandidaten
  identitätssicher korrelieren. Der Log nennt ausdrücklich
  `targetSource=native-target` oder
  `targetSource=recent-query-cache-after-native-clear`. Andere oder
  wiederverwendete Slots werden weiterhin nicht als Hühnerfall protokolliert.
- Zusätzlich protokolliert die Runtime auf Info und jeweils begrenzt:
  akzeptierte Query-Pre/Query-Post-Zustände, Movement-Pre und Movement-Post,
  Zustandsänderungen vor und nach dem 100-ms-Nativscan sowie den bereits
  vorhandenen Idle-Requery-Schreibzugriff `state 6 -> 0` mit nahem Beutetier.
  Hunter-Snapshots enthalten Zustand, Timer, Wanderzustand, Pfadfelder
  `+0xF2/+0xF4`, Rohfelder `+0x2AC/+0x340`, letzten Befehl `+0x398`,
  Blockadebyte `+0x3FE`, native und gecachte Zielidentität sowie den
  unmittelbar vorher protokollierten Snapshot. Query-Post wird nur für
  tatsächlich akzeptierte bekannte Tiere geloggt, damit Kandidatenscans das
  Diagnosebudget nicht vor dem Fehlerfall aufbrauchen.
- Die vorhandene Idle-Requery-Korrektur ist nun explizit sichtbar: Sie ist der
  einzige Mod-Schreibzugriff auf den Hunter-Zustand und setzt erst einen bereits
  ziellosen Zustand `6` bei naher unreservierter Beute auf Zustand `0` zurück.
  Sie erklärt wiederholte Zielversuche, aber nicht den initialen Übergang
  `1 -> 6`.
- Eine gezielte ausführbare-Sektionssuche bestätigte 59 reale Schreiber des
  nativen Ziel-ID-Feldes `R13 + 0x9F6` über verschiedene Unit-AI-Funktionen.
  Innerhalb der Hunter-Funktion weist der Schreiber bei RVA `0x13050F` das von
  RVA `0x18B5F0` gelieferte neue Ziel zu und wechselt anschließend in Zustand
  `11`; er ist daher kein direkter Nullschreiber des beobachteten
  `1 -> 6`-Abbruchs. Die bereits dokumentierten State-6-Branchpunkte bleiben
  die präzisesten Writer-nahen Marker, nun ohne den Ziel-vorhanden-Blindfleck.
- Der nächste Ingame-Test soll möglichst einen fehlerhaften Hühnerfall und
  einen erfolgreichen Kamel-/Vanillafall im selben Spiel erzeugen. Maßgeblich
  sind die chronologische Folge von `chicken-query-pre-accepted`,
  `query-post`, `movement-pre/post`, `native-scan-pre/post`, einem der beiden
  State-6-Branchmarker, `native-target-cleared` und
  `idle requery mutation`. Fehler- und Diagnose-failed-Marker müssen null
  bleiben. Script-Extender-Code wurde weiterhin nicht geändert.
- Der Abschlussbuild über `build.bat` war erfolgreich (`0` Warnungen,
  `0` Fehler) und installierte Version `1.1.23`. Lokale und installierte DLL,
  PDB und `info.json` sind bytegleich. Die DLL hat SHA-256
  `BA97597AF9D5E0DA4E46222A3E91103E355C6835345008B759788CC2C16F1B83`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte im tatsächlichen Artefakt die
  beiden Branchcallbacks ohne native Ziel-vorhanden-Schranke, die
  zehnsekündige ID-/Global-ID-Korrelation, Query-Post-, Movement-, Scan- und
  Idle-Requery-Marker sowie ausschließlich Info-/Error-Ausgabe. Der Hinweis
  auf eine geringfügig neuere ilspycmd-Version beeinflusste die Prüfung nicht.

Auswertung des sechsten Diagnose-Testlaufs und enger Verhaltensfix:

- Der relevante Lauf mit Version `1.1.23` enthielt drei reproduzierte
  Hühnerabbrüche und keine Mod-, Callback- oder Diagnosefehler. Alle drei
  Hühner waren lebend, identitätstreu und reserviert. Die native Zielübernahme
  funktionierte; die Abbrüche traten nach `5` bis ungefähr `2.000` ms und bei
  Distanzen `12`, `18` beziehungsweise `18` auf. In jedem Fall erschien nun
  der zuvor blinde Marker `chicken state-6 branch` mit
  `issueOrderResult=0`, `orderBlocked=0` und `lastCommand=3`. Die generische
  Unit-Order-Funktion, nicht das Blockadebyte und nicht die Query, verursachte
  somit den bestätigten Übergang `1 -> 6`. Es gab weiterhin keinen Schuss,
  Schaden oder Kill. Spätere `reserved-chicken filter`-Treffer zeigten nur die
  nach dem Abbruch verwaiste Reservierung `2`.
- Die gezielte Analyse von `c_game_unit_issue_order` bei RVA `0x18E950`
  erklärt zugleich den Unterschied zum erfolgreichen Kamel. Im Command-4-
  Pfad prüft RVA `0x18EB82/0x18EB8A` die Relation der beiden Besitzer. Gleiche
  Relation springt bei `0x18EB92` vorzeitig in den generischen Friendly-Unit-
  Pfad `0x18EC57`. Erst danach liegt bei `0x18EB98` der bereits vorhandene
  native Sonderfall für Jäger (`CHIMP_TYPE_HUNTER == 6`), der direkt den
  Jagdzielpfad `0x18EBD2` benutzt. Neutrale oder gegnerische Beute erreichte
  diesen Pfad bereits; eigene Hühner bislang nicht.
- Version `1.1.24` ergänzt deshalb einen fünften gemeinsam transaktionalen,
  hash- und instruktionsgesicherten Inline-Hook bei RVA `0x18EB82`. Er bildet
  die beiden überschriebenen Relationstabellen-Instruktionen (`8 + 8` Byte)
  exakt nach. Nur wenn Feature aktiv, Quelltyp Jäger, Zieltyp Huhn und beide
  Relationswerte gleich sind, wird in Vanillas vorhandenen Hunter-Zielpfad
  `0x18EBD2` umgeleitet. Bei verschiedener Relation läuft der bestehende
  native Vergleich ab `0x18EB98`; alle anderen gleichen Relationen springen
  unverändert nach `0x18EC57`. Der Fix schreibt weder Besitzer/Farbe noch
  Relation, Ziel, Zustand, Pfad oder Reservierung um. Damit sind auch
  Naturhühner mit Besitzer `0` explizit abgedeckt, unabhängig davon, ob ihre
  Relation gleich oder verschieden ausgewertet wird.
- Jeder tatsächlich angewandte Redirect erzeugt begrenzt einen Info-Marker
  `same-relation chicken order redirected to native Hunter path` mit stabilen
  Jäger-/Hühneridentitäten, beiden Besitzern, Alive-State, Reservierung und
  Distanz. Der native Enable-Byte hält Mod-aus und `HuntChicken`-aus frei von
  Managed-Callbacks. Die vorhandene breite Diagnose bleibt für den nächsten
  Lauf aktiv und kann dadurch zusätzlich Projektil-, Damage-, Kill-, Pickup-
  und Dropoffpfad in demselben Spiel belegen.
- Der statische Audit bestätigte die kanonische DLL weiterhin mit Steam-Build
  `24651686`, Größe `3.450.880` und SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
  Das neue Pattern hat in ausführbaren Sektionen genau einen Treffer bei RVA
  `0x18EB72`; Rizin bestätigte die Hookgrenze bei `0x18EB82` und die nativen
  Ziele `0x18EB98`, `0x18EBD2` und `0x18EC57`. JSON-, Diff-, CRLF-,
  Altcode-, Besitzer-/Farbschreib- und Log-Level-Audits waren erfolgreich;
  der Script-Extender-Arbeitsbaum blieb sauber und wurde nicht geändert.
- `build.bat` baute und installierte Version `1.1.24` mit `0` Warnungen und
  `0` Fehlern. Lokale und installierte DLL, PDB und `info.json` sind
  bytegleich. Die DLL hat SHA-256
  `71D9549D390F6BB03173FD0E79DD1C4397DF0090DB6F4D8769D05EC9B22635E9`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte im gebauten Artefakt die
  Relationstabellenzugriffe, Typwerte Jäger `6` und Huhn `62`, die native
  Aktivierungsschranke, den registererhaltenden Info-Callback und die drei
  getrennten Sprungziele. Die alten Symbole und Besitzerzuweisungen fehlen.
- Offen bleibt der interaktive Laufzeittest der installierten Fassung. Erwartet
  wird nach `chicken-query-pre-accepted` genau ein Redirect-Marker, danach
  Hunter-Zustand `9`, Projektil, Schaden/Kill, Aufnahme und Fleischabgabe.
  `chicken state-6 branch`, Query-Null-, Diagnose-failed- und globale
  Fehlermarker müssen für diesen Versuch ausbleiben. Zusätzlich bleiben die
  Matrixfälle Besitzer `0`, gegnerischer Besitzer, zweiter Jäger, Mod aus,
  Kornkammer-Nachproduktion und Fleischmenge abzunehmen. Feature 01 ist bis zu
  diesem Ingame-Nachweis noch nicht endgültig freigegeben.

Laufzeit-Initialisierungsfehler in `1.1.24` und Korrektur `1.1.25`:

- Der erste Start mit `1.1.24` erklärte das scheinbare Ignorieren vollständig:
  Die DLL und alle vier nativen Pattern wurden geladen, anschließend brach die
  gemeinsame Hook-Transaktion mit
  `Unexpected Hunter order relation hook boundary` ab. Dadurch blieb die
  gesamte ImprovedHunters-Runtime inaktiv; folgerichtig gab es weder
  Query-Kandidaten noch Lifecycle- oder Redirect-Marker. Das beobachtete
  Verhalten war kein neuer Query- oder KI-Fehler.
- Die Grenzprüfung hatte zwei unterschiedliche Adressen vermischt. Der Hook
  beginnt bei `0x18EB82` und überschreibt nur `8 + 8 = 16` Byte, daher liefert
  die Hookbibliothek korrekt `0x18EB92` als technischen Return. Der Stub bildet
  den dort beginnenden ursprünglichen `je` jedoch selbst nach und muss bei
  verschiedener Relation logisch bei `0x18EB98` fortsetzen. Version `1.1.25`
  führt deshalb getrennte Konstanten und Argumente für Return `0x18EB92` und
  Fortsetzung `0x18EB98`; die Ziele `0x18EC57` und `0x18EBD2` bleiben
  unverändert.
- Der nach dem Fehler zulässige Wiederholungsbuild war erfolgreich (`0`
  Warnungen, `0` Fehler) und installierte Version `1.1.25`. Lokale und
  installierte DLL, PDB und `info.json` sind bytegleich. Die DLL hat SHA-256
  `D30106B670B0E2B93123FF2EACFF48709295693D4D4B905E3127871C722C674D`.
  Der Artefaktaudit bestätigte die getrennten Return-/Continue-Adressen, den
  Redirect-Marker und Version `1.1.25`. Besitzerumschreibung bleibt entfernt;
  der Script-Extender blieb unverändert.
- Ein neuer Spielstart ist erforderlich. Bereits vor einem Kartenstart muss
  nun der Initialisierungsmarker mit `orderRelationHookRva=0x18EB82` ohne
  anschließenden ImprovedHunters-Fehler erscheinen. Erst danach ist das
  Hühnerverhalten aussagekräftig; die zuvor dokumentierte funktionale
  Testmatrix bleibt offen.

Kontrollierter Neutralhuhn-Vergleich in `1.1.26`:

- Der relevante Altcode ist Commit `d0bb6f7`, nicht der bereits umgebaute
  aktuelle `HEAD`. Dort setzte `OnUnitCreate` jedes nichtneutrale Huhn bereits
  vor dem nativen Spawn auf Besitzer und Farbe `0`; der Scan wiederholte die
  Neutralisierung. Bei einem schon mit Besitzer `0` erzeugten Huhn waren
  beide Operationen wirkungslose No-ops. Target-Auswahl, Projektil-Fallback,
  `KillUnit`, Kadaverfinalisierung, Pickup und Dropoff liefen danach durch
  denselben Modpfad, der im aktuellen Code weiterhin vorhanden ist.
- Für einen belastbaren A/B-Lauf sind die drei erst durch Feature 01
  eingeführten Verhaltensausnahmen in dieser Diagnosefassung für Besitzer `0`
  abgeschaltet: Nonzero-Controlword-Bypass, Reservation-2-Retarget und
  Same-Relation-Order-Redirect. Ein neutrales Huhn mit dem üblichen
  Controlword `0` läuft damit durch Vanillas alten Kandidatenpfad. Die
  allgemeinen Managed-Auswahl-, Fallback-, Kadaver- und Fleischfunktionen
  bleiben wie im Altcode aktiv. Besitzer `!= 0` verwenden weiterhin alle
  Feature-01-Ausnahmen.
- Nach Kartenstart erzeugt der erste erfolgreiche Spawn einer Jägerhütte des
  lokalen Spielers einmal pro Karte ein neutrales Diagnosehuhn. Gesucht wird
  in vier Himmelsrichtungen ein begehbares, gebäudefreies Feld im Abstand von
  fünf Tiles; Besitzer und Farbe werden bereits beim Erzeugen als `0`
  übergeben. Der Info-Marker `neutral A/B chicken spawned` enthält Gebäude-,
  Unit- und Global-ID, Besitzer, Farbe, Controlword, Alive-State und beide
  Positionen. Kartenladen und KI-Gebäude sind durch Kartenstart- und
  Local-Player-Schranken ausgeschlossen.
- Zu diesem Zeitpunkt war die Einschränkung von Besitzer `0` nur als
  Vergleichsmaßnahme vorgesehen. Der anschließend erfolgreiche vollständige
  Vanilla-Lauf führte jedoch zur in `1.1.28` dokumentierten Entscheidung, den
  bewiesenen Owner-0-Pfad beizubehalten und nur Nonzero-Owner durch die neuen
  Ausnahmen zu führen.
- Der Hotkey-/Mausspawn wurde nicht ergänzt: Die kurzlebige BepInEx-Komponente
  ist kein zuverlässiger Input-Host, und eine belastbare Maus-zu-Kartentile-
  Umrechnung ist im vorhandenen API-Pfad nicht belegt. Der automatische Spawn
  liefert den benötigten reproduzierbaren Kontrollfall ohne einen weiteren
  unvalidierten Laufzeitmechanismus.
- JSON-, Diff-, Versions-, Altcode-, Log-Level- und planweite
  Wiederholungsaudits waren erfolgreich. Alle sieben geänderten Textdateien
  wurden ordinal verifiziert und enthalten ausschließlich CRLF. Der lokale
  Script-Extender-Arbeitsbaum blieb sauber und wurde nicht geändert.
- Der vorgeschriebene Abschlussbuild baute und installierte Version `1.1.26`
  mit `0` Warnungen und `0` Fehlern. Lokale und installierte DLL, PDB und
  `info.json` sind jeweils SHA-256-identisch. Die DLL hat SHA-256
  `B18C006FEB2A9D2EAE2504917E213D4786AE47F8710510FCA0669C0DED5355C2`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte im installierten Artefakt die
  Kartenstart- und Building-Spawn-Subscriptions, `CreateUnitLocal(0, 0, ...)`,
  die Owner-0-Spawnverifikation, den Info-Marker und die Managed-Schranke des
  Reservation-2-Sonderfalls. Außerdem sind die nativen Owner-Konstanten und
  getrennten Reject-/Vanilla-Labels enthalten.
- Für den nächsten Lauf muss nach einem vollständigen Spielneustart eine neue
  Jägerhütte des lokalen Spielers gebaut werden. Erwartet wird genau ein
  `neutral A/B chicken spawned`-Marker mit `owner=0`, `color=0` und gewöhnlich
  `flags92=0`. Für dessen Jagd dürfen kein `reserved-chicken filter` und kein
  `same-relation chicken order redirected` erscheinen; Projektil-, Damage-,
  Kill-, Pickup- und Dropoffmarker bilden den neutralen Kontrollpfad. Danach
  kann derselbe Logabschnitt direkt mit den vorhandenen Besitzer-1-Hühnern
  verglichen werden.

Auswertung des Neutralhuhn-Vergleichs und Crashkorrektur in `1.1.27`:

- Der Besitzer-0-Kontrollfall lief vollständig durch Vanillas reguläre Kette:
  Zielwahl und Reservierung, echtes Projektil, `damage-pre`, nativer Kill,
  Verarbeitung und Fleischabgabe. Die temporäre A/B-Abgrenzung hat damit
  bestätigt, dass der vorhandene neutrale Hühnerpfad weiterhin funktioniert.
- Bei Besitzer-1-Hühnern entstanden echte Projektile, aber weder `damage-pre`
  noch `kill-pre`. Der Schaden wird folglich vor dem Script-Extender-Damagehook
  unterdrückt. Der verzögerte `KillUnit`-Fallback erzeugte zwar einen plausiblen
  Kadaver, führte den Jäger aber nicht durch Vanillas erfolgreichen
  Trefferübergang zur Abholung; der Jäger suchte stattdessen weitere Beute.
- Der Crash mit zwei Jägern war keine Reservierungskollision. Der Minidump
  enthält `STATUS_BREAKPOINT` an DLL-RVA `0x12FF5A`. Der diagnostische Hook ab
  `0x12FF53` hatte 17 Byte überschrieben, obwohl unveränderte native Sprünge bei
  `0x1304E5` und `0x130585` weiterhin direkt nach `0x12FF58` führten. Dieser
  Seiteneinstieg landete mitten in den acht eingebetteten Zieladressbytes des
  14-Byte-Absolutsprungs; das dortige Byte `CC` wurde als Breakpoint ausgeführt.
- Version `1.1.27` entfernt deshalb den gesamten Query-Failure-Diagnosehook
  einschließlich Patternauflösung, Stub, Callback und Erfolgsprüfung. Der
  separate State-6-Vergleichshook bei `0x130171` bleibt bestehen. Die entfernte
  Hookstelle hatte ausschließlich protokolliert und war an keinem Fixpfad
  beteiligt.
- Als nächster nativer Befund ist die Besitzer-/Relationsprüfung zwischen
  Projektilerzeugung und Damagehook zu lokalisieren. Ein enger
  Hunter-gegen-Huhn-Ausnahmezweig ist gegenüber einer Rückkehr zur dauerhaften
  Besitzerumschreibung vorzuziehen; der neutrale Alternativweg würde zusätzlich
  eine sichere Kornkammer-Bestandsführung und Feature 04s noch nicht belegten
  allgemeinen Auto-Aggro-Filter benötigen.

Gegnerischer Vergleich und engere Order-Diagnose in `1.1.28`:

- Besitzer `0` bleibt dauerhaft der nachweislich funktionierende Vanilla-
  Kontrollpfad. Besitzerunabhängige Jagd verlangt, dass Hühner jedes Besitzers
  funktionieren, nicht dass alle Besitzer durch dieselben neuen Ausnahmen
  laufen. Die drei Owner-0-Schranken in Live-Query, Reservation-2-Retarget und
  Same-Relation-Redirect bleiben deshalb bestehen. Nur wenn später ein reales
  Owner-0-Huhn mit abweichendem Controlword durch Vanilla scheitert, wird
  dieser Sonderfall anhand eigener Laufzeitdaten neu bewertet.
- Das einmalige Vergleichshuhn beim Bau der ersten lokalen Jägerhütte wird nun
  mit Besitzer und Farbe `2` erzeugt und auf beide Werte geprüft. In einem
  Testspiel, in dem Spieler 2 feindlich ist, sollte es Vanillas vorhandenen
  Different-Relation-Hunterpfad ohne den Same-Relation-Redirect verwenden.
  Der Fall trennt damit die Frage „Schaden gegen denselben Spieler blockiert?“
  von den bereits bewiesenen neutralen Jagd-, Kadaver- und Abgabefunktionen.
- Die native Analyse von `c_game_unit_issue_order` bei RVA `0x18E950` hat
  `issueOrderResult=0` weiter eingegrenzt. Der direkte Hunterpfad ruft die
  interne Routine RVA `0xA06F0` auf. Deren Ergebnis wird bei `0x18ED1F` nach
  `EDX` kopiert; ein Wert `<= 0` verzweigt zu `0x18EE14`. Dort führt Quelltyp
  Hunter (`6`) nach `0x18F928`, das Null zurückgibt. Für Hunter/Huhn ist dieser
  Pfad daher ein direkter Nachweis einer Ablehnung durch RVA `0xA06F0`.
- Ein neuer rein diagnostischer Inline-Hook beginnt am Basic-Block-Einstieg
  `0x18EE14`. Er überschreibt exakt `10 + 6` Byte, bildet Typvergleich und
  bedingten Sprung unverändert nach und protokolliert nur aktivierte
  Hunter/Huhn-Fälle auf Info. Erfasst werden Helper-Rückgabewert, Identitäten,
  Besitzer/Farbe, AI-/Pfadzustände, Zielreferenz, Gesundheit, Controlword,
  Reservierung, Tilepositionen sowie die vier signierten nativen Bounds beider
  Units bei `GameUnit +0xB2/+0xB4/+0xB6/+0xB8`. Ein vollständiger nativer Referenzscan fand
  keinen Sprung zur einzigen inneren Instruktionsgrenze `0x18EE1E`; damit
  besteht der Seiteneinstiegsfehler des entfernten Hooks hier nicht.
- RVA `0xA06F0` ist ein Wrapper, der die Kernroutine RVA `0x9E350` mit zwei
  Orientierungen derselben Koordinatengrenzen versucht. Der untersuchte
  Kernpfad rechnet Koordinaten um, liest Tile-Arrays und prüft Belegungsflags;
  eine Besitzer- oder Relationstabelle ist dort nicht sichtbar. Der
  `issueOrderResult=0`-Abbruch ist damit statisch eher als fehlende gültige
  Interaktions-/Geometrieposition denn als direkte Owner-Sperre einzuordnen.
- Offen bleibt der Ingame-Vergleich. Bei feindlicher Relation wird kein Marker
  `same-relation chicken order redirected` erwartet. Tritt
  `Hunter-order internal helper rejected chicken` auf, liefert sein
  `helperResult` den unmittelbaren Grundpfad für den Order-Abbruch. Erfolgt die
  Order, kann anschließend geprüft werden, ob Projektilschaden, nativer Kill,
  Kadaverannahme und Abgabe für Besitzer 2 vollständig funktionieren.
- Der statische Abschlussaudit bestätigte die kanonische DLL mit Größe
  `3.450.880` und SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
  Das korrigierte Pattern hat im PE genau einen Treffer; Rizin bestätigte die
  Instruktionen bei `0x18EE14`/`0x18EE1E` und das Sprungziel `0x18F928`.
  JSON-, Diff-, CRLF-, Log-Level-, Owner-/Farb-Schreib-, Owner-0-Routing- und
  Versionsaudits waren erfolgreich. Der Audit fing vor dem Build eine zunächst
  semantisch äquivalente, aber bytefalsche SIB-Kodierung im Pattern ab; der
  ausgelieferte Anker entspricht der DLL (`66 42 ... 26`).
- `build.bat` erzeugte und installierte Version `1.1.28`. Der äußere versteckte
  PowerShell-Wartewrapper des ersten Laufs kehrte nach Ende von `cmd` und
  Buildprozess nicht zurück und wurde beendet. Nach der anschließend erkannten
  Changelog-Korrektur war ein Wiederholungsbuild erforderlich; dieser endete
  beobachtbar mit Exitcode `0`, `0` Warnungen und `0` Fehlern und installierte
  das korrigierte Manifest. Die aus der vertieften nativen Analyse abgeleitete
  Rohbounds-Erweiterung machte einen weiteren erlaubten testbedingten Build
  nötig; auch dieser endete mit Exitcode `0`, `0` Warnungen und `0` Fehlern.
  Lokale und installierte DLL, PDB und `info.json` sind bytegleich. Die finale
  DLL hat SHA-256
  `A1C22E3FD8C1B6E7D13E52F83BE3E167FB87EA8DC502004FAF89464CC812429F`.
  Der `ilspycmd`-Audit bestätigte im gebauten Artefakt Patternauflösung,
  transaktionalen fünften Hook, Callback/Info-Marker, Player-2-Spawn und die
  Prüfung von Owner und Farbe auf `2` sowie beide vierteiligen Raw-Bounds.
- Ein Laufzeittest von `1.1.28` ist noch offen. Nach einem vollständigen
  Spielneustart muss der Initialisierungsmarker
  `orderHelperFailureDiagnosticHookRva=0x18EE14` ohne Hookfehler erscheinen.
  Danach ist beim Bau der ersten lokalen Jägerhütte
  `player-2 comparison chicken spawned` mit `owner=2, color=2` zu erwarten.
  Für die Auswertung sind Relation-Redirect, Helper-Reject, State-6,
  Projektil-, Damage-/Kill-, Kadaver-, Pickup- und Dropoffmarker gemeinsam zu
  vergleichen.

Auswertung des Spieler-2-Vergleichs und Eigenbesitz-Schadenskorrektur in
`1.1.29`:

- Der jüngste Lauf von `1.1.28` bestätigt den feindlichen Kontrollfall. Das
  Diagnosehuhn wurde mit `owner=2`, `color=2`, `flags92=2` erzeugt. Der Jäger
  schoss Projektil `212`; nach rund 410 ms folgten der native `damage-pre` mit
  Schaden `15000` und `kill-pre`. Die vollständige Verarbeitung und Abgabe
  wurden im Spiel beobachtet. Der Same-Relation-Redirect griff nicht.
- Im selben Lauf wurden fünf eigene Hühner über den Same-Relation-Redirect
  ausgewählt und mit echten Pfeilen beschossen. Für keines erschien ein
  `damage-pre` oder `kill-pre`; jeweils nach rund einer Sekunde griff der alte
  `KillUnit`-Fallback und erzeugte Zustand `0x6F`. Der Jäger sammelte diese
  Kadaver nicht ein. Damit liegt die verbleibende Abweichung weiterhin vor dem
  öffentlichen Projektil-Damagehook und nicht in Query oder Ordererteilung.
- `issueOrderResult=0` trat in diesem Lauf nicht erneut auf: weder der Marker
  `Hunter-order internal helper rejected chicken` noch ein echter State-6-
  Übergangsmarker erschien. Das passt zur Beobachtung ohne sichtbares Hin-und-
  Herlaufen. Der ältere Nullrückgabefall bleibt als intermittierender
  Geometrie-/Interaktionsfehler dokumentiert, ist aber nicht Ursache dieses
  reproduzierbaren Schadensfehlers.
- Die frühen Live-Query-Ausnahmen bleiben für Hühner mit nichtnull Controlword
  notwendig: Ohne sie erreichte auch das nachweislich funktionierende
  Spieler-2-Huhn den Typcallback nicht. Alle späteren Verhaltensausnahmen sind
  nun enger: Reservation-2-Retarget und Orderredirect verlangen einen
  identischen, nichtnull Besitzer von Jäger und Huhn. Der Managed-Retargetpfad
  prüft dieselbe Invariante. Besitzer 0 und fremde Besitzer behalten danach
  ihren bewiesenen nativen Ablauf.
- Die installierte DLL wurde gezielt im Projektilupdate ab RVA `0x9C730`
  untersucht. Die vier direkten Aufrufe des nativen Projektilschadens bei
  `0x9CA14`, `0x9CAC9`, `0x9CB98` und `0x9CC65` gehen alle zu RVA `0x192700`.
  Der zunächst verdächtige Vergleich bei `0x9C9C6` ist kein Besitzervergleich,
  sondern vergleicht den Projektiltyp mit dem konstanten Wert `1`; er wird
  ausdrücklich nicht gepatcht. Ein enger, belastbar identifizierter
  Friendly-Fire-Branch wurde in dieser Funktion nicht gefunden.
- Statt eines weiteren nativen Inline-Hooks nutzt `1.1.29` den bestehenden
  öffentlichen `GameUnitManagerAPI.DamageUnitRanged`-Einstieg. Nur für ein
  echtes Jägerprojektil gegen ein lebendes Huhn mit exakt demselben nichtnull
  Besitzer wird im `ProjectileDelete`-Pre-Callback der native Schadenspfad
  aufgerufen, solange die Projektil-ID noch gültig ist. Das Pending-Intent wird
  vorher entfernt, sodass eine etwaige rekursive Projektilbereinigung keinen
  Doppelschaden auslösen kann.
- Falls der Delete-Callback nicht rechtzeitig eintritt, versucht der vorhandene
  Ein-Sekunden-Fallback für denselben eng geprüften Eigenbesitzfall zuerst
  ebenfalls `DamageUnitRanged`. Nur wenn dieser Aufruf fehlschlägt, bleibt
  `KillUnit` als bisheriger Sicherheitsfallback aktiv. Bei erfolgreichem
  nativen Schaden wird `TryFinalizeShotIntentCorpse` ausdrücklich nicht
  aufgerufen; der native Kadaverzustand wird dadurch nicht erneut zu `0x6F`
  überschrieben. Neutral-, Feind- und andere Beutepfade behalten ihr bisheriges
  Fallbackverhalten.
- Beide neuen Erfolgsmarker laufen auf Info und enthalten Besitzer,
  Identitäten, Projektil-ID, API-Ergebnis, Gesundheit sowie Kadaverzustand. Der
  Eventcallback kapselt Fehler getrennt, sodass ein Diagnose-/API-Fehler die
  native Projektillöschung nicht verhindert.
- Der Abschlussaudit bestätigte erneut alle sechs nativen Patterns mit jeweils
  einem eindeutigen Treffer in der kanonischen DLL sowie deren Größe
  `3.450.880` und
  SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
  JSON-, Versions-, Diff-, CRLF-, Log-Level-, Besitzer-/Farbschreib- und
  Script-Extender-Audits waren erfolgreich. Der vorgeschriebene
  `build.bat`-Lauf baute und installierte `1.1.29` mit `0` Warnungen und `0`
  Fehlern. Lokale und installierte DLL, PDB und `info.json` sind jeweils
  SHA-256-identisch; die DLL hat SHA-256
  `47910BCAF4C0B60636999186DAAD74400DC44EDCB668DF81B77C0FEB5CD92D98`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte die Delete-Pre-Subscription,
  Owner-/Global-ID-/Projektilidentitätsprüfungen, den nativen
  `DamageUnitRanged`-Aufruf, den nur nach ausgebliebenem nativen Kill aktiven
  `KillUnit`-Fallback sowie die exakten Ownerbedingungen in Managed-Retarget
  und nativen Stubs.
- Offen ist der Laufzeittest von `1.1.29`: Erwartet werden für ein eigenes Huhn
  `same-owner chicken order redirected`, danach `damage-pre`, `kill-pre` und
  entweder `same-owner chicken ranged damage at projectile delete` oder der
  verzögerte Marker mit `rangedDamageApplied=True`. Anschließend müssen der
  native Kadaver angenommen, Fleisch abgeholt und abgegeben werden. Feindliche
  und neutrale Kontrollhühner dürfen keinen Same-Owner-Schadensmarker erzeugen.

Auswertung von `1.1.29` und aktive Flugphasenkorrektur in `1.1.30`:

- Der Lauf mit `1.1.29` enthielt fünf vollständig identifizierte Schüsse auf
  eigene Hühner. Projektil-Spawn, Jäger-ID, Ziel-ID, beide Global-IDs und
  Besitzer `1` stimmten jeweils überein. Kein Schuss erreichte `damage-pre`
  oder `kill-pre`; alle Ziele behielten exakt `2500/2500` Gesundheit. Damit
  bestätigt der Lauf die im Spiel sichtbare Beobachtung, dass der Pfeil das
  eigene Huhn in der nativen Projektilkollision nicht als Treffer akzeptiert.
- Der Vergleichsschuss auf das Huhn von Spieler 2 erzeugte dagegen rund 459 ms
  nach dem Spawn den regulären `damage-pre` mit Schaden `15000`, unmittelbar
  danach `kill-pre` und anschließend den normalen Hunter-Kadaverablauf. Query,
  Order, Projektilerzeugung und Zielkoordinaten sind daher nicht die gemeinsame
  Fehlerquelle; die Abweichung hängt von der identischen Besitzerrelation ab.
- Der in `1.1.29` beim `ProjectileDelete`-Pre-Callback ausgeführte öffentliche
  `DamageUnitRanged`-Aufruf lieferte bei allen fünf eigenen Hühnern `false` und
  ließ ihre Gesundheit unverändert. Der Code entfernte das Pending-Intent
  trotzdem vor dem Aufruf. Dadurch konnte die verzögerte Auflösung den
  fehlgeschlagenen Versuch nicht mehr sehen. Dieser bestätigte
  Lebenszyklusfehler ist in `1.1.30` entfernt.
- `1.1.30` prüft ausschließlich echte Hunter-Pfeile gegen lebende Hühner mit
  exakt demselben nichtnull Besitzer. Im persistenten 100-ms-Scan wird der
  öffentliche Fernschadenspfad erst aufgerufen, wenn der Pfeil noch
  `AliveState.IsAlive` besitzt und höchstens 64 native Weltkoordinateneinheiten
  vom aktuellen Huhn entfernt ist. Global-IDs, Typen, Quell-/Ziel-Unit-ID und
  Projektilbesitzer werden vor jedem Versuch erneut validiert.
- Pro Projektil sind höchstens drei aktive Versuche erlaubt. Jeder Versuch
  wird vor Eintritt in nativen Code im Intent vermerkt, damit synchron
  ausgelöste Projektilcallbacks keinen rekursiven Doppelaufruf verursachen.
  Nur ein tatsächlich totes Ziel entfernt das Intent sofort. Der Info-Marker
  protokolliert Versuchszahl, Projektilzustand, aktuelle Projektil-, Ziel- und
  Unit-Koordinaten, beide Distanzen, API-Ergebnis und Zielgesundheit.
- Der Delete-Pre-Callback führt keinen verspäteten Schadensaufruf mehr aus. Er
  dokumentiert stattdessen den vollständigen Endzustand eines ohne nativen
  Treffer gelöschten Pfeils. Damit zeigt der nächste Lauf, ob der Pfeil beim
  Delete bereits `MarkedForDeletion` war und wie weit er vom Huhn bzw. seinem
  Zielpunkt entfernt endete.
- Für ein nach allen aktiven Versuchen noch lebendes eigenes Huhn ist der
  `KillUnit`-Fallback deaktiviert. Er erzeugte nachweislich den vom Hunter nicht
  akzeptierten Zustand `0x6F` und hätte den eigentlichen Schadensfehler
  verdeckt. Der bewährte blockierte-Pfeil-Fallback für neutrale, feindliche und
  alle anderen Beutetiere bleibt unverändert.
- Der statische Abschlussaudit bestätigte Manifest-/Pluginversion `1.1.30`,
  gültiges JSON, keine Besitzerzuweisung, den diagnose-only Delete-Callback,
  die exakte Same-Owner-Schranke, die maximal drei zielnahen Alive-Versuche,
  das erneute Auflösen des Zielslots nach nativen Calls, den deaktivierten
  Same-Owner-`KillUnit`-Fallback und einen unveränderten Script-Extender-
  Worktree. Die kanonische DLL bleibt bei Größe `3.450.880` und SHA-256
  `33AA33457F7DFAAA6D316D1D5E4C5AB97094F2C73B68D349990ABF9D0EF3B469`.
- Der vorgeschriebene einzelne `build.bat`-Lauf erzeugte und installierte
  `1.1.30`. Wie beim bereits dokumentierten früheren Infrastrukturfall kehrte
  nur der äußere PowerShell-Wartewrapper nach Ende von `cmd` und MSBuild nicht
  zurück; die Buildartefakte und der installierte Pluginordner wurden um
  `20:37:48` aktualisiert. Lokale und installierte DLL, PDB und `info.json`
  sind jeweils SHA-256-identisch. Die DLL ist `177.664` Byte groß und hat
  SHA-256
  `5FDC7BC26402F3AF01278771EA090D770AF848C7DBA89A6AEC70CCDC80EEF28C`.
  Der nachgelagerte `ilspycmd`-Audit bestätigte im gebauten Artefakt den
  Alive-State `2`, Distanzgrenze `64`, maximal drei Versuche, erneute
  Global-ID-Prüfung, diagnose-only Delete-Pfad sowie `killFallback =
  !sameOwnerChicken && !rangedDamageKilledTarget`.
- Offen ist der Laufzeittest von `1.1.30`. Ein erfolgreicher Pfad muss
  `same-owner chicken active-flight ranged damage` mit
  `projectileAliveState=2`, `damageApplied=True`, `targetKilled=True` und
  `currentHealth=0` zeigen; danach müssen Kadaverannahme, Abholung und Abgabe
  dem Spieler-2-Kontrollfall entsprechen. Bei `damageApplied=False` zeigen die
  bis zu drei aktiven Versuche erstmals belastbar, dass auch der öffentliche
  Schadenseinstieg die identische Besitzerrelation ablehnt und nicht nur der
  späte Delete-Zustand das Scheitern verursacht.

Laufzeitauswertung von `1.1.30` und Übergabestand:

- Der einzige und neueste `1.1.30`-Lauf enthält vier echte Hunter-Schüsse auf
  vier eigene Hühner. Query und enger Same-Owner-Orderredirect funktionierten
  jeweils; alle Projektil-, Hunter-, Ziel- und Global-IDs blieben konsistent.
  Es gab keine Hookinstallations-, Native-Scan- oder Projektil-Diagnosefehler
  und keinen `issueOrderResult=0`-/Helper-Reject-Marker.
- Kein einziger Marker `same-owner chicken active-flight ranged damage`
  erschien. Alle vier Intents endeten mit `activeDamageAttempts=0`. Der neue
  öffentliche Schadensversuch wurde daher nicht abgelehnt, sondern wegen
  einer fehlerhaften Distanzvorbedingung überhaupt nicht aufgerufen. Aus
  diesem Lauf darf ausdrücklich noch keine Aussage darüber abgeleitet werden,
  ob `DamageUnitRanged` bei aktivem Pfeil Eigenbesitz akzeptiert.
- Die Logwerte identifizieren den Fehler eindeutig: `GameProjectile`-
  `r_CurrentTileX/Y` enthält Tilekoordinaten, während
  `r_TargetWorldTileX/Y` und `GameUnit.r_CurrentWorldPositionX/Y` native
  Weltkoordinaten mit Faktor 8 verwenden. Beispiel Projektil `82`:
  `projectileCurrent=438,702`, `projectileTarget=3484,5620` und
  `unitWorld=3484,5620`. Erst `438*8=3504` und `702*8=5616` sind vergleichbar;
  der Pfeil lag damit nur `20` beziehungsweise `4` Weltkoordinateneinheiten
  vom Huhn entfernt und hätte die Grenze `64` erfüllt. Der unskalierte
  Vergleich lag dagegen bei mehreren Tausend und blockierte jeden Versuch.
- Alle vier Delete-Pre-Marker zeigten `projectileAliveState=3`. Dies bestätigt
  die frühere Vermutung: Beim Delete-Callback ist der Pfeil bereits
  `MarkedForDeletion`; der dort in `1.1.29` versuchte Schadensaufruf lief nicht
  mehr mit einem aktiven Projektil. Die aktive Flugphase bleibt deshalb der
  richtige Testort, sobald beide Koordinatenseiten dieselbe Skalierung nutzen.
- Jeder verzögerte Resolver meldete für die eigenen Hühner
  `sameOwnerChicken=True`, `rangedDamageAttempted=False`,
  `killFallback=False`, `corpseFinalized=False`, `stillAlive=True` und
  `currentHealth=2500`. Die Sicherheitsänderung aus `1.1.30` funktioniert:
  Es wurden keine künstlichen `0x6F`-Kadaver erzeugt und der offene
  Schadensfehler wurde nicht durch `KillUnit` verdeckt.
- Das Huhn von Spieler 2 wurde im selben Lauf von mehreren Spieler-1-
  Fernkämpfern beschossen. Projektil `37` erzeugte nach rund 480 ms den
  regulären `damage-pre` mit `15000`, anschließend `kill-pre`. Dieser konkrete
  Kontrollfall stammt nicht von einem Hunter (`hunter=0` im zugehörigen
  Fallback-Intent), bestätigt aber erneut, dass fremdbesitzende Hühner in die
  normale Projektilschadensstrecke gelangen. Der bereits frühere erfolgreiche
  Hunter-/Spieler-2-Kontrolllauf bleibt der maßgebliche Huntervergleich.
- Ein einzelner Fehlerlog trat auf:
  `ignored an invalid Hunter-query context: hunter=644445184, candidate=7`.
  Rund sechs Sekunden später wählte der reale Hunter `94/7331502` dasselbe
  Huhn `7/7331980` regulär aus. Der Callback blieb fail-closed, der Lauf
  stürzte nicht ab und alle eigenen nativen Hooks arbeiteten weiter. Dies ist
  ein weiterer Laufzeitbeleg für den separat dokumentierten Script-Extender-
  Hunter-ID-Fehler, nicht für einen neuen Fehler des entfernten unsicheren
  Diagnose-Hooks.
- Für die Fortsetzung in einem neuen Chat ist die nächste Änderung klar
  begrenzt: Vor dem Distanzvergleich `r_CurrentTileX/Y` in Weltkoordinaten
  umrechnen (beobachteter Faktor `8`) oder beide Seiten in Tilekoordinaten
  vergleichen. Danach denselben Alive-/Owner-/Global-ID-gesicherten Pfad
  erneut testen. Bis dahin bleiben Quellcode und installierte Version
  `1.1.30` unverändert.

## Startprompt für einen neuen Chat

Setze Feature 01 aus ImprovedHunters/Plans/01-HunterQuery-OwnedAnimals.md ab dem dokumentierten Übergabestand von Version 1.1.30 fort. Lies zuerst ImprovedHunters/PLAN.md und prüfe danach Projekt-, installierten DLL-, Log- und Git-Stand. Korrigiere als nächsten eng begrenzten Schritt den nachgewiesenen Mischvergleich zwischen `GameProjectile.r_CurrentTileX/Y` (Tiles) und den Weltkoordinaten von Projektilziel beziehungsweise Unit (beobachteter Faktor 8), ohne Neutral- oder Fremdbesitzerpfade zu verändern. Teste anschließend, ob `DamageUnitRanged` für einen noch aktiven Hunter-Pfeil gegen ein Huhn desselben Besitzers tatsächlich Schaden erzeugt, und verfolge bei Erfolg den nativen Kadaver-, Pickup- und Abgabepfad. Bewahre die fail-closed Behandlung ungültiger Script-Extender-Hunter-IDs; keine Script-Extender-Änderung in diesem Chat.
