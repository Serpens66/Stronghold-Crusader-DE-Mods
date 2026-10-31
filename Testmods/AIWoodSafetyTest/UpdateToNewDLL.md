# Native-Vertrag AIWoodSafetyTest

Referenz: kanonische installierte CrusaderDE.dll, SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Die allgemeine Diagnose berechnet diesen Hash bereits; der interne Vertrag teilt
ihn, ohne einen weiteren Datei-Scan. Native-/Interop-Abweichungen schalten die
abhängige Regel ab. Öffentliche APIShared-Verträge, Bugfix-Sink und Canari-Brücke bleiben gleich.

## Suchannahme und Fortsetzungen

Besitzer dieses Testmods: `0x58BAE`, 14 Byte, zwei vollständige MOVs:
`44 89 99 3C 98 1B 00 44 89 91 40 98 1B 00`.
Sie schreiben R11D/R10D nach `[RCX+0x1B983C/0x1B9840]`.
Die umgebende Funktion ist `0x58950`, Größe 0x290, ohne Calls/SIMD.
RCX muss dem AIV-Root `module+0x34A9F70` entsprechen. Kandidatenkoordinaten sind
unsigned coarse 0..159; der Hüttenursprung ist jeweils mal fünf.

Ablehnung springt nach `0x58B2E` (MOVZX EAX, BYTE [RCX+0x187830], sieben Byte
`0F B6 81 30 78 18 00`) und setzt die normale Enqueue-/BFS-Fortsetzung fort.
Annahme führt beide verdrängten MOVs genau einmal aus, weiter nach `0x58BBC`
(MOV R12, [RSP+8], fünf Byte `4C 8B 64 24 08`). Der Epilog restauriert Register
mit MOVs und berechnet Flags bei ADD RSP,0x10 neu. Er liest keine alten Flags.
RAX ist auf beiden Pfaden tot: fallback überschreibt EAX zuerst; Erfolg ist void.
Alle übrigen lebenden Register werden durch Vorfilter und ContextStub erhalten.
Der Callback setzt RAX als Entscheidung, TEST EAX erfolgt erst nach dem Wrapper.
Die Funktion hat keine SIMD-Liveness. Stack und nonvolatile Register folgen der
Win64-ABI. Keine globale Suchdatenänderung und kein erneuter Vanilla-Aufruf.

Der einzige direkte Erfolgssprung kommt von `0x58B28`; `0x58BAC` überspringt den
Block bis zur exakten Endgrenze. Vollfunktionsdecode und gesamte Datenbank-Xrefs
weisen keine Eingänge ins Spaninnere aus. Fixes' Farmlandspan `0x58B12..0x58B2E`
(angefordert 0x1A, wegen vollständiger JZ-Instruktion tatsächlich 0x1C), Bugfix' Nearby-Einstieg
`0x58950` sowie die allgemeinen Suchbeobachter liegen außerhalb. Der vorherige
Canari-Experimentalpunkt im früheren Erfolgsbranch wird nicht erneut verwendet.
Die Live-Prüfung verlangt die eigenen 14 Byte unverändert; fremde benachbarte
Hooks werden weder überschrieben noch zurückgenommen.

RedBird X64InlineHook muss tatsächlich genau 14 Byte verdrängen und vorbereitet
sein. Nach Veröffentlichung wird FF25+0 samt Stubpointer geprüft. Einmal veröffentlichte
Hooks werden nie zurückgepatcht/freigegeben; auch nach teilweise fehlgeschlagener
Publikation bleiben alle Wurzeln erhalten. Nur unveröffentlichte Kandidaten sind
disposable. Kontrollblock (40 Byte, Prozesslebensdauer): Gate+0, Feindbits+4,
AIV-Root+8, gepinnte ushort-Masken+16, Kandidaten+24, Maskentreffer+32.

## Bereits besessener Zählermesspunkt

`AIBuildDiagnoseTest` besitzt unverändert `0x12D1A2` Länge21 nach dem WORD-ADD
bei `0x12D199`. Native Einheit RBX/0x490, Hütte RSI; Unit-Manager `0x67E8400`,
Building-Manager `0x64CCBB0`. Kein zweiter Hook im Sicherheitsmod.
Einheitenstruktur: Größe0x490, Global-ID+0x94, Alive+0x88, Todesmarker+0x29C,
Zustand+0x2BC, Linkslot+0x334, Linkglobal+0x364, Gesundheit+0x3C4.
Gebäudestruktur: Größe0x32C, Alive+0xD0, Besitzer+0xD6, Global+0xD8,
Ursprung+0xEE/+0xF0, Gridgröße+0xF8, signed-WORD-Zähler+0x2DC.
Native Headerabstände sind Unit+0x65C und Building+0x5C; die 1-basierte ID
bleibt in beiden nativen Formeln unverändert. General kopiert die aktuellen Globals
vor dem Eingriff. Der Testmod prüft die echten Interop-Offsets und beide Globals erneut.

## Platzierung, Typen und Reichweite

Kein eigener Hook bei `0x77E60`: Extender bleibt Besitzer. Post-Ereignis läuft
nach einem Originalaufruf und seinem globalen Override. Nur das vorhandene öffentliche
TileManager.IsPlacementBlocked-Ergebnis wird bei gefährlicher KI-Holzhütte auf true gesetzt.
Native Caller `0x6D580` prüft dieses Ergebnis auf seinem normalen Fehlerweg.
Die Semantik des Events und des Resultatsetters bei Extender-Updates erneut prüfen.
Die Wirkung späterer fremder Post-Beobachter ist nicht kontrollierbar.

Mapper/Structs/Unittypen stammen aus den installierten Extender-Enums. Hüttengröße3
ist ein auditierter Maschinenvertrag aus `0x51540 -> 0x6A190 -> 0x619C0/0x69850`.
Bogenschützen-Reichweite kommt aus dem aktuellen Extender-Zugriff für ARCHER und
ARAB_BOW (Vanilla-Projektiltabelle `0x2DA8F0`, ArcherArrow:54); keine harte Gameplay-
Reichweite im Mod. Gebäudegrundflächen werden aus öffentlichen Interop-Feldern gelesen.

## Updateverfahren und bewusst fehlender Fallback

Auf unbekannten Native-Hashes gibt es keinen Pattern-Fallback. Ein isolierter
14-Byte-Treffer beweist weder Enqueue-Fortsetzung/Queuevertrag noch Identitätslayout,
Hüttengröße oder Todesmarker-Writer. Diese Abhängigkeiten haben derzeit keinen
unabhängigen semantischen Zweitanker zur automatischen Migration. Deshalb beide
Regeln hashgebunden; kein Zugriff über eine geraten übertragene Adresse.

Bei Update die vollständige Bau-/Such-/Platzierungskette, alle Todes- und
Rückkehrpfade in `0x12BC00`, Zählerwriter/Leser `0xACF40 -> 0x3B270`, verzögerte
Löschung, ABI/Register/Flags, beide Fortsetzungen, sämtliche Eingänge und installierte
Hookbesitzer erneut auditieren. Referenzbytes, Strukturen, Native-Prüfer und Scratch-
Maschinentest gemeinsam aktualisieren. Prüfer lesen CURRENT.json/Manifest statt
einen Datenbankpfad vorauszusetzen. Die Testdaten nutzen die originale Nachbartabelle
bei `0x2D2E50` (64 Byte) und die echte Funktion in einem privaten Abbild.
