# Gezielte Vier-Match-Serie: verbundene Startgebäude

Die normalen Craggy-Cliffs-Läufe haben die native Record-Verknüpfung
aufgezeichnet, aber keinen Start beobachtet, der einen weiteren Record
allein wegen desselben Linkwerts löschte. Deshalb folgt jetzt die
bereits bekannte dichte Karte `test AI overbuild eachother.map` mit
SHA-256 `D63CD2FF3AEABA80BC3BC173BB615207666F1EAF759ECAC573FAD0DA61979DF3`.
Die installierte Native-DLL hat SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.

Der Mensch nutzt den entfernten Keep-Slot 4 `(458,351)`. Vier KIs
nutzen die nahen Slots 0 `(237,397)`, 1 `(254,379)`, 2 `(258,417)`
und 3 `(278,395)`. Ihre Reihenfolge ist Nizar Default 6, Wolf
Default 8, Marshal Default 7, Jewel Default 5. Die Lord-Namen sind
gegen die tatsächlich geladenen Runtime-Enums geprüft; die früheren
Namen in dieser Zeile waren falsch. In den letzten zwei Läufen
werden die vier KI-Slots in umgekehrter Reihenfolge vergeben. Die
AIV-Dateien sind jeweils die eingebauten **Default**-Varianten.

| Lauf | KI-Slots in Spielerreihenfolge | Completed Castles |
| --- | --- | --- |
| `OB-forward-off` | 0, 1, 2, 3 | aus |
| `OB-forward-on` | 0, 1, 2, 3 | an |
| `OB-reverse-off` | 3, 2, 1, 0 | aus |
| `OB-reverse-on` | 3, 2, 1, 0 | an |

Die Testserie setzt Karte, Lords, Positionen, AIVs und die Option
beim Öffnen einer **neuen lokalen Skirmish-Lobby**. Nach jedem Lauf
genügt der sichtbare Spielbeginn; auf späteren regulären KI-Bau muss
nicht gewartet werden. Ein fehlgeschlagener oder manuell veränderter
Lauf rückt die Serie nicht weiter. Die vier Läufe können in einem
Spielprozess stattfinden. Danach das Spiel beenden, damit Log und
Traces vollständig archiviert werden können.

Entscheidungsfrage: Welche der kollidierenden Startgruppen werden
über ihren nichtnullen `nativeCleanupLinkId` zusätzlich gelöscht,
welche fitrelevanten Zellen ändern sich dadurch, und treten erstmals
Startabbrüche auf? Die Ergebnisse ändern den Produktionscode nur dann,
wenn alle möglichen Eingangszustände dadurch sicher abgrenzbar sind.
