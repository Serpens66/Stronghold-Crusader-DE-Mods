# Code-Prüfungen: Befunde und Korrekturen verifizieren

**Lesen, wenn:** Code, ein Mod, ein Diff oder ein Konzept auf Fehler, Probleme, Risiken oder Verbesserungen geprüft werden soll (Review, Audit, „Bugs suchen“), auch als Teil eines größeren Auftrags, oder wenn Korrekturen für gefundene Fehler vorgeschlagen bzw. eingebaut werden. Gilt zusätzlich zur Root-`AGENTS.md`.

Ziel: Schon die erste Antwort enthält nur belastbare Befunde. Eine spätere Nachfrage „nochmal genau prüfen“ darf keine gemeldeten Fehler mehr als Fehlalarm entlarven.

## Ablauf: erst sammeln, dann verifizieren, dann melden

- **Phase 1 – Kandidaten sammeln:** Auffälligkeiten intern notieren. Ein Kandidat ist noch kein Befund und wird nie ungeprüft ausgegeben, auch nicht als Zwischenstand.
- **Phase 2 – jeden Kandidaten einzeln verifizieren** (Pflicht vor der Meldung):
  - den vollständigen Pfad lesen: alle Aufrufer, vorgelagerte Guards und Validierungen, Initialisierungs- und Teardownreihenfolge, Thread-/Dispatcherkontext, reale Wertebereiche der Eingaben;
  - prüfen, ob der Fall anderswo bereits behandelt ist: APIShared, `Shared/`, Script Extender, Fixes-Mod, Vanilla. Hängt der Befund von Vanillas nativem Verhalten ab, gilt der Audit aus [native-analysis.md](native-analysis.md);
  - bewusste Projektentscheidungen berücksichtigen, statt sie als Fehler zu melden (z. B. dauerhaft installierte Hooks mit logischer Aktivierung → [native-hooks.md](native-hooks.md); Diagnose nur in `/devdiag`-Builds; absichtlich vanillagleiches Verhalten);
  - **Widerlegungsversuch:** aktiv nach dem Code, Vertrag oder Zustand suchen, der den Befund entkräften würde. Erst wenn das ernsthaft gescheitert ist, gilt der Befund;
  - einen **konkreten Fehlerfall** formulieren: welche Eingabe bzw. welcher Zustand führt über welchen Pfad zu welchem falschen Ergebnis oder Absturz. Ohne konkreten Fehlerfall kein Fehlerbefund.
- **Phase 3 – melden:** nur das Ergebnis der Verifikation.

## Einstufung und Format

- **Bestätigt:** Pfad nachverfolgt, Fehlerfall konkret, Widerlegung gescheitert → als Befund melden.
- **Plausibel, nicht bestätigt:** nur in einem getrennten Abschnitt und mit Angabe, was zur Bestätigung fehlt (z. B. Laufzeittest, nicht verfügbare Quelle). Nie mit bestätigten Befunden mischen.
- **Alles andere verwerfen**, ohne es zu melden.
- Je Befund: Ort (`datei:zeile`), Fehlerfall, Beleg (die relevanten Stellen des Pfads), warum vorhandene Guards nicht greifen, Schwere und Auswirkung.
- Stil-, Lesbarkeits- und Geschmacksfragen sowie Verbesserungsideen getrennt als „Hinweise“, nie als Fehler.
- **Qualität vor Menge:** keine Mindestanzahl. „Keine bestätigten Fehler gefunden“ ist ein gültiges Ergebnis. Zum Schluss den Prüfumfang nennen: was geprüft wurde und was nicht.

## Typische Fehlalarme in diesem Workspace

Vor der Meldung gezielt ausschließen:

- Null-Checks oder Initialisierungsprüfungen, die Lifecycle oder Aufrufreihenfolge bereits garantieren.
- Vermutete Races in Code, der nachweislich nur auf dem Main Thread läuft.
- „Fehlendes Unhook/Unsubscribe/Dispose“ bei bewusst dauerhaft verwurzelten Hooks und Subscriptions ([runtime-lifecycle.md](runtime-lifecycle.md), [native-hooks.md](native-hooks.md)).
- Annahmen über Unity-, BepInEx-, Extender- oder Fixes-Verhalten ohne Prüfung der Quelle bzw. des lokalen Klons.
- Verhalten, das absichtlich Vanilla entspricht.
- Fehler im Script Extender oder Fixes-Mod als Fehler des eigenen Mods ([external-components.md](external-components.md)).
- Rein theoretische Überläufe oder Indexfehler außerhalb der realen Wertebereiche; IDs, Indizes und Enums nach [ids-enums.md](ids-enums.md) prüfen.

## Korrekturen genauso streng prüfen

Für Korrekturen gilt dasselbe zweiphasige Verfahren wie für Befunde:

- Nur für bestätigte Befunde vorschlagen.
- Nachweisen, dass die Korrektur den konkreten Fehlerfall tatsächlich behebt: den Pfad mit der Korrektur erneut durchgehen.
- Alle Nebenwirkungen prüfen: alle Aufrufer und Nutzer des geänderten Codes, später gelesene Zustände und Felder, Thread und Timing, MP-Synchronität, Kompatibilität mit Script Extender, Fixes-Mod und APIShared, Verhalten im Karteneditor und beim Save-Laden.
- **Widerlegungsversuch auch hier:** aktiv nach einem Fall suchen, den die Korrektur verschlechtert oder neu kaputt macht.
- Verhaltensgleichheit außerhalb des Fehlerfalls belegen. Verhaltensänderungen nur vorschlagen; der Nutzer entscheidet.
- Betroffene vorhandene Tests nennen und anpassen (Root-`AGENTS.md` §4).
- Nicht ausreichend abgesicherte Korrekturen ausdrücklich so kennzeichnen, statt sie als Lösung zu präsentieren.

## Unteragenten

Bei größeren Prüfungen ist ein unabhängiger Prüf-Unteragent sinnvoll: Er erhält die Kandidaten bzw. Korrekturen mit dem Auftrag, jede einzeln zu widerlegen; gemeldet wird nur, was diese Prüfung übersteht. Der koordinierende Chat kontrolliert das Ergebnis selbst (Root-`AGENTS.md` §3); die Delegation ersetzt die eigene Verifikation nicht.
