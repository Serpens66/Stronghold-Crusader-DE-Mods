# MakeDesync native Kachelprobe

F11 ist an die durch `MultiplayerResyncData.CheckIntegrity()` zugelassene
Spiel-DLL gebunden. Referenz ist SHA-256
`FBCB93195FC7EFCA9BDAC5204852EFDD76F9818F59A6711750D77C9CEF2831E2`.
Es gibt keinen Fallback auf andere Layouts.

- Die 128 Logic-Blöcke beginnen ab RVA `0x48F71B0`. `0x227E0` im
  Prüfsummenbauer `0x22020` startet bei dieser Adresse, schreitet je
  `0x2728` Bytes fort und hasht `0x2729` Bytes je Block über `0x78E0`.
  Der Abgleich vergleicht die Spielerwerte für denselben Tick. Eine Abweichung
  reiht über `0x27C50`/ `0x23990` Chore 54 ein. Dessen Ausführung an
  `0x15170` erzeugt die separaten Reparaturprüfsummen.
- `0x251F0` überspringt gleiche Logic-Blöcke und inaktive Spieler. Ab
  `0x25EA0` vergleicht der Host die einzelnen Blöcke, sendet bei Abweichung
  Chore 65 und bleibt bei überschrittenem Sendebudget in der Reparaturphase.
  Der Handler `0x16C90` berechnet das Ziel aus TileManager RVA `0x405EDB0`,
  Logic-Offset `0x898400` und Blockindex mal `0x2728`. Er kopiert
  `0x2729` über den Feldhelfer `0x1F5F0`. Der Packpfad liest die Hostfassung;
  der Ausführungspfad schreibt nur auf dem Client. Chore 55 rekonstruiert
  anschließend den lokalen Zustand und Chore 67 gibt die gemeinsame Welt frei.
- Beobachtet werden die tatsächliche Logikadresse sowie die Layerlängen über
  `GameTileManagerAPI`. Nur in einer echten Client-Simulation unter dem
  Engine-Monitor wird auf einer fertig ausgehobenen, unbelegten Kachel das
  Steinbit gesetzt. Das Pitch-Bit ist ausgeschlossen, da es im letzten
  Byte des Logic-Worts die Änderung für die vorzeichenbehaftete Prüfsumme
  verdecken könnte. Der Test schreibt weder native Befehle noch Netzwerkpakete.
- Die private Historie bindet sich genau an BugfixesAndQoL 1.0.237. Kapazität,
  Startoffset und Statusmarker werden gegen dessen Laufzeitmetadaten geprüft.
  Der native Schreibpfad dieses Mods erfasst und restauriert den Soil-Bytewert
  im Logic2-Layer. Nur ein aktiver, belegter Grabeneintrag mit abweichendem
  ursprünglichem Bodentyp ist als Testziel zulässig.
- Die Probe prüft den veränderten nativen Wert am Start, danach das vollständige
  ursprüngliche Logic-Wort und die private Historie. Erst nach normalem Verfüllen
  bestätigt F8 das Soil-Byte und den geleerten Historieneintrag.
