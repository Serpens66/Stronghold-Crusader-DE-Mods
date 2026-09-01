# Verbesserungspotenzial der CrusaderDE-Native-Baseline

## Zweck

Dieses Dokument beschreibt, wie semantische Zuordnungen in
`_inspect/CrusaderDE-Native-Baseline` belastbarer gemacht werden können. Es
ändert weder die kanonische Rohbaseline noch vorhandene Vertrauensstufen. Eine
Hochstufung soll ausschließlich durch reproduzierbare Evidenz erfolgen.

## Zwei getrennte Vertrauensdimensionen

Die Baseline sollte zwei Aussagen ausdrücklich getrennt bewerten:

- **Versionszuordnung:** Ist eine Funktion in zwei DLL-Versionen nachweislich
  dieselbe beziehungsweise die fortgeschriebene Implementierung?
- **Semantische Zuordnung:** Ist die fachliche Bedeutung, Signatur oder
  Benennung der Funktion nachweislich richtig?

`versionMatch: confirmed` bestätigt daher nicht automatisch einen
Funktionsnamen. Identische oder eindeutig zugeordnete Bytes können weiterhin
nur eine unbekannte Funktion beschreiben.

## Woran erkennen wir einen belegten Kandidaten?

Ein Kandidat ist belastbar belegt, wenn mehrere voneinander unabhängige
Evidenzketten dieselbe Bedeutung stützen und keine Gegenindizien bestehen. Als
unabhängig gelten insbesondere:

1. **Direkter Datenfluss:** Argumente, Strukturfelder, globale Daten und
   Rückgabewert ergeben im vollständigen Pseudocode die behauptete Bedeutung.
2. **Callgraph:** Fachlich bekannte Caller übergeben passende Werte und werten
   den Rückgabewert entsprechend aus. Callees passen ebenfalls zur Zuordnung.
3. **Managed-/Export-Brücke:** Eine exportierte native Funktion, ein
   Script-Extender-Hook oder ein eindeutig zugeordneter Managed-Aufruf führt
   direkt zu der Funktion.
4. **Eindeutige Konstanten und Tabellen:** Verwendete Enumwerte,
   Strukturgrößen, Tabellenindizes oder Grenzwerte stimmen mit unabhängig
   bekannten Spielverträgen überein.
5. **Kontrollfluss und Seiteneffekte:** Alle relevanten Ein- und Ausgangspfade
   führen zu den erwarteten Änderungen, nicht nur ein zufällig passender
   Teilblock.
6. **Versionsübergreifende Stabilität:** Eine bestätigte Funktionszuordnung
   bleibt bei veränderten Adressen über Funktionshash, normalisierte
   Instruktionen, Callgraph und Datenzugriffe semantisch konsistent.
7. **Laufzeitbeobachtung oder fokussierter Test:** Ein gezielter Test bestätigt
   Argumente, Seiteneffekte und Rückgabeverhalten, ohne die Bedeutung nur aus
   dem beobachteten Endergebnis zu erraten.

Ein einzelner Pattern-Treffer, String in Funktionsnähe, ähnlicher
Funktionsumfang, identischer Versionshash oder eine plausible Formel reicht
nicht aus. Solche Hinweise können einen Kandidaten auffinden, aber nicht seine
Semantik bestätigen.

## Vorgeschlagene Einstufung

### `candidate`

- mindestens ein nachvollziehbarer Suchhinweis;
- Funktionsgrenzen und Fundstelle sind bekannt;
- fachliche Bedeutung, ABI oder Parameterrollen sind noch nicht durchgehend
  belegt;
- widerspruchsfreie Plausibilität genügt, wird aber ausdrücklich als unsicher
  gekennzeichnet.

### `probable`

- mindestens zwei unabhängige Evidenzketten stützen dieselbe Bedeutung;
- vollständiger relevanter Kontroll- und Datenfluss wurde geprüft;
- Argumentanzahl, Registerbelegung und wesentliche Parameterrollen sind
  konsistent;
- keine plausiblen konkurrierenden Deutungen oder Gegenindizien sind bekannt;
- mindestens eine Evidenzkette ist stärker als Stringnähe oder Patternähnlichkeit.

### `confirmed`

- direkte Export-, Managed- oder eindeutig dokumentierte Aufrufbrücke; oder
- mindestens drei unabhängige starke Evidenzketten einschließlich vollständigem
  Datenfluss und Callgraph sowie zusätzlicher Laufzeit-/Versionsbestätigung;
- ABI, Rückgabewert, Strukturfelder und relevante Seiteneffekte sind konkret
  dokumentiert;
- die Zuordnung wurde gegen den exakten DLL-Hash geprüft und ist
  reproduzierbar.

Eine Hochstufung sollte feldweise möglich sein. Beispielsweise können
Funktionsgrenzen und ABI bereits `confirmed`, der fachliche Name aber nur
`probable` sein.

## Empfohlene zusätzliche Baseline-Felder

- `semanticConfidence`, getrennt von `versionMatchConfidence`;
- `abiConfidence` und vollständige native Signatur;
- Parameterrollen mit Register beziehungsweise Stackposition;
- Rückgaberegister, Rückgabebedeutung und beobachtete Wertebereiche;
- relevante Strukturfelder mit Offset, Größe, Zugriffstyp und Provenienz;
- Funktionsgrenze, Blockgrenzen und alle externen Branch-Einstiege;
- Register-Liveness für bekannte Patch-/Hookspans;
- `evidence[]` mit Quelle, DLL-Hash, RVA/VA und kurzer Begründung;
- `counterEvidence[]` für widersprechende oder noch offene Beobachtungen;
- `verifiedBy` für reproduzierbare Query-, Test- oder Analyseartefakte;
- getrennte Aussagen für Funktionsidentität, fachlichen Namen und einzelne
  Parameterrollen.

## Konkreter Verbesserungsbedarf aus dem ExtraFeatures-Audit

Die folgenden Zuordnungen besitzen bereits starke Binärevidenz, sind in der
semantischen Baseline aber teilweise nur als Kandidaten geführt. Sie sollten
nach Abschluss des Audits anhand ihrer Evidenzpakete neu bewertet werden:

- AI-Sleep-State und zugehöriger Manager-weiter Synchronisationshelfer;
- Emergency-Demolition-Gate und Hovel-/Inaccessible-Building-Löschung;
- AIV-Build-Step und Placement-Helfer einschließlich sechs nativer Argumente;
- AI-Marktpreishelfer für Kauf und Verkauf;
- AI-Seuchen-Erzeugung sowie Apotheker-Distanzentscheidung;
- Mönchs-Bewegungsentscheidung;
- Quarry-Pile-Kandidatenhelfer;
- Gatehouse-Automationshandler und dessen vier Timing-Immediates.

Zusätzlich hat der Audit einen belegten API-Grenzfehler außerhalb der
semantischen Funktionsnamen gezeigt: Der Script-Extender-Hook bei RVA
`0xB7B4B` bildet `GatehouseQueryEventArgs.UnitId` mit
`GetIndexByOffset(...)`, also 0-basiert. Die native Instruktion bei `0xB7B34`
multipliziert den Kandidatenslot direkt mit der `GameUnit`-Größe `0x490` und
belegt damit die Indexbasis. Die Baseline sollte solche Eventfeld-Provenienz
und ID-/Index-Konvertierungen als eigene maschinenlesbare Verträge erfassen.

Für jede dieser Funktionen sollte das Evidenzpaket mindestens Caller, Callees,
Pseudocode, exakte Instruktionen am Hookspan, ABI, Strukturfelder und
Versionsmatch enthalten. Erst danach ist eine semantische Hochstufung
gerechtfertigt.

## Reproduzierbarer Aufwertungsablauf

1. Exakten SHA-256 der installierten DLL mit `CURRENT.json` vergleichen.
2. Das Evidenzpaket mit `query.ps1 function`, `callers` und `callees` erzeugen.
3. Funktionsgrenzen und alle Hook-/Branch-Einstiege gegen die Rohdisassemblierung
   prüfen.
4. Argumente vom Caller bis zu jedem relevanten Zugriff und Rückgabewert
   verfolgen.
5. Strukturfelder gegen Types, Script-Extender-Interop und mindestens eine
   weitere unabhängige Verwendung abgleichen.
6. Gegenhypothesen und abweichende Caller ausdrücklich suchen.
7. Evidenz und Gegenindizien mit Hash und RVA dokumentieren.
8. Erst anschließend die jeweils belegte Teilaussage hochstufen; keine
   pauschale Hochstufung der gesamten Funktion.

## Offene Qualitätsverbesserungen

- Automatischer Bericht über Funktionen, deren `versionMatch` stärker als ihre
  semantische Einstufung ist.
- Validierung, dass Signaturen aller Caller miteinander vereinbar sind.
- Maschinenlesbare Hookspan-Datensätze mit Originalbytes, Instruktionsgrenzen,
  Branch-Einstiegen und Register-Liveness.
- Diff-Berichte für geänderte Strukturfelder und globale Datenreferenzen.
- Kleine Regressionstests für bekannte Funktionen und Immediates statt bloßer
  RVA-Prüfungen.
- Klare Kennzeichnung, ob ein Name aus Exporten, Managed-Code, Communitywissen
  oder eigener semantischer Rekonstruktion stammt.
