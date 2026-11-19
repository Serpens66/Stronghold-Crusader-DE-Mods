# KI-Jäger-Test 0.1.0

Eigenständiger Testmod unter den Modoptionen als **AI Hunter Prey Test**. Benötigt APIShared ab 0.54.0 und den Script Extender ab 2.14.0. ImprovedHunters ist optional. Gameplay-Mod (`NetworkMode=1`): im Multiplayer auf allen Rechnern identisch installieren; Host-Einstellungen werden über APIShared synchronisiert. Ein echter Multiplayer-Spieltest steht noch aus.

Alle vier Schalter sind zunächst aktiv. Einstellungen vor der Partie ändern:

Aktualisierung 19.11.2026: Es gibt eine Grundhütte je KI. Eine vorhandene AIV-Hütte zählt dafür; nur eine fehlende wird automatisch ergänzt. Hühner und andere Beute erlauben diese Grundhütte, weitere AIV-Hütten benötigen ausschließlich andere jagbare Tiere als Hühner. Verschwindet diese andere Beute, werden die zusätzlichen Hütten nach vollständiger Fleischabgabe abgerissen. Als Grundhütte bleibt die dem ersten Kornspeicher nächstgelegene erhalten; bei Gleichstand entscheidet die niedrigste globale Gebäude-ID.

Die Grundhütte wartet auch zwischen Hühner-Spawns. Voraussetzung: wirksame ImprovedHunters-Hühnerjagd, Populationseinstellung größer null, aktivierter Populationsdienst, Hühner im Beutekatalog für den eigenen Spieler und ein lebendes eigenes Spawngebäude (Kornspeicher, Mühle oder Bäckerei). Im Editor wird kein Hühnernachwuchs vorausgesetzt. Ohne vorhandene Beute und ohne diesen Nachschub werden auch Grundhütten nach Fleischabgabe abgebaut. Die drei Modsettings wirken weiterhin unabhängig: deaktivierte AIV-Steuerung sperrt keine Bauaufträge, deaktivierter Abriss entfernt keine Hütten, deaktivierte Automatik ergänzt keine fehlende Hütte.

Das bedeutet möglichen Nachschub, keine erzwungenen oder garantierten Spawns: Vanilla prüft zusätzlich den Zugang zum Spawngebäude; weitere Populationsmods können den Zielwert verändern.

- **KI-Jäger-Test aktivieren:** gemeinsamer Hauptschalter.
- **AIV-Jäger nur bei Beute bauen:** sperrt normale KI-Bauversuche für Jäger ohne Beute. Menschliche Bauversuche bleiben unverändert.
- **Jäger auch ohne AIV-Auftrag bauen:** ergänzt auch bei Vanilla-Lords ohne Jäger im Burgbauplan eine Hütte. Grenze: eine lebende oder gerade entstehende Hütte je KI; vorhandene AIV-Hütten zählen mit. Normale Baukosten und Platzierungsregeln gelten. Sucht begrenzt nahe dem ersten Kornspeicher; ohne Platz, Ressourcen, funktionierenden Bergfried oder Kornspeicher wartet der Mod.
- **Leere KI-Jäger ohne Beute abreißen:** wartet auf abgeschlossene Verarbeitung und vollständige Fleischabgabe. Ein voller oder unerreichbarer Kornspeicher verzögert den Abriss unbegrenzt. Verwendet den normalen KI-Abriss mit Vanilla-Erstattung (im normalen Spiel 50 Prozent).

Der Scan läuft sofort beim Start und danach alle 4800 Kartenticks, ungefähr zwei Minuten bei Tempo 40. Pause hält die Prüfung an. Beute zählt kartenweit; die Entfernung und der Weg zur Beute werden nicht geprüft. Eigene Tiere zählen nur, wenn der registrierende Mod das ausdrücklich erlaubt. Frei sammelbare Kadaver zählen mit, wenn ImprovedHunters sie aktuell verwerten kann. Bereits von einem Jäger bearbeitete Tiere schützen dessen Hütte über die Arbeits- und Fleischprüfung.

Der APIShared-Katalog liefert auch neue, von anderen Mods registrierte Tierarten außerhalb des Extender-Enums. ImprovedHunters' deaktivierte Tierarten und sein Aktivierungszustand werden zusätzlich berücksichtigt. Diese optionale Integration liest einen Einstellungs-Snapshot und bindet für Kadaver dessen vorhandene private Abfrage. Ändert sich dieser Vertrag, setzt der Testmod seine Entscheidungen für die Partie aus und meldet `PREY_UNKNOWN`; er interpretiert einen Abfragefehler nicht als Beutemangel. Die übrigen Mods bleiben unverändert.

## Manuelle Prüffälle

Aktive-AIV-Schutz (19.11.2026): Zusätzliche Hütten dürfen keine Fläche des tatsächlich geladenen KI-Bauplans überdecken, einschließlich späterer Bau-/Wiederaufbauphasen, Mauern, Gräben und zusammengesetzter Kasernen/Gilden/Ölschmelzen. Die bestehende 3x3-Fläche des Jägers muss vollständig außerhalb dieser Reservierungen liegen. Fehlt ein gültiger Plan oder eine bekannte Geometrie, meldet der Mod `aiv-plan-unavailable`, `aiv-plan-unknown-footprint` beziehungsweise `aiv-plan-native-unavailable` und baut vorsorglich keinen zusätzlichen Jäger. Der Schutz liest den Plan neu je Suche, nicht aus einer möglicherweise abweichenden AIV-Datei auf der Platte. Bereits vorhandene Hütten werden dadurch nicht automatisch versetzt oder als neue Sonderregel abgerissen.

Die Suche bleibt auf alle 4800 Kartenticks begrenzt und läuft nur ohne vorhandene Grundhütte mit geeigneter Versorgung und Bauvoraussetzungen. Der neue 67x67-Reservierungsbereich wird nur einmal je solcher Suche erstellt. Prüfen: freie zukünftige Bauflächen bleiben leer, freie Flächen daneben bleiben nutzbar; gedrehte Kasernen/Gilden/Ölschmelzen und spätere AIV-Phasen sowie Speichern/Laden und Kartenwechsel einbeziehen. Insbesondere darf nach dem letzten geplanten Bauschritt kein AIV-überlappender automatischer Bau erfolgen.

1. Karte ohne Wild, ohne jagbare Modtiere und ohne aktiven Hühnernachschub: kein zusätzlicher Bau; ein AIV mit Jägerauftrag darf ebenfalls keine Hütte bauen. Derselbe AIV-Auftrag mit ausgeschalteter AIV-Option folgt wieder Vanilla.
2. Karte mit Beute und Vanilla-Lord ohne Jäger-AIV: bei ausreichend Ressourcen und Platz höchstens eine zusätzliche Hütte. Mit ausgeschalteter Automatik kein zusätzlicher Bau; AIV-Bauten bleiben davon unabhängig.
3. Mit ImprovedHunters ausschließlich Hasen, Kamele oder Hühner: deren Jagdeinstellung einschalten, dann Bau und tatsächliche Jagd prüfen. Jagd für diese einzige vorhandene Art vor einem weiteren Start ausschalten: kein zusätzlicher Bau. Auch ausschließlich Rehe mit ausgeschalteter Rehjagd prüfen.
4. Letztes Tier wird gejagt: Hütte bleibt während Rückweg, Verarbeitung und Fleischtransport bestehen. Kornspeicher voll machen: Hütte muss bestehen bleiben. Nach vollständiger Abgabe darf sie bei einer späteren Prüfung verschwinden.
5. Ausschließlich frische sammelbare Kadaver: Hütte bleibt erlaubt. Bereits verwertete/fadende Kadaver dürfen sie nicht dauerhaft erhalten.
6. Nach Beutemangel neue jagbare Tiere hinzufügen: nach der nächsten Prüfung darf wieder gebaut werden. Den Ablauf auch nach Speichern/Laden sowie einem Kartenwechsel prüfen.
7. Menschliche Hütte und Fleischtransport beobachten: kein Abriss durch den Testmod. Hauptschalter sowie die beiden Bauoptionen einzeln prüfen.
8. Nur Hühner, mehrere AIV-Jägeraufträge: insgesamt eine Hütte, auch wenn mehrere Bauaufträge innerhalb eines Prüfintervalls erfolgen. Bestehende zusätzliche Hütten geben ihr Fleisch ab und verschwinden erst danach.
9. Alle Hühner vorübergehend gejagt, Nachschub aktiv: Grundhütte bleibt. Jagd deaktivieren beziehungsweise Zielpopulation auf null setzen (jeweils vor der nächsten Partie): ohne sonstige Beute fehlt diese Nachwuchsannahme. Dasselbe im Editor mit null tatsächlichen Tieren prüfen.

Für einen positiven Basistest genügt eine freie Baufläche nahe dem Kornspeicher auf derselben Wegkomponente wie der Bergfriedeingang. Die bewusst begrenzte Suche kann andere erreichbare Bauplätze übersehen. Es werden keine Tiere oder Rohstoffe hinzugefügt und keine bestehenden Gebäude verschoben.

## Build und Nachweise

Prüfstand 19.11.2026: aktualisiertes Paket ohne Installation gegen die installierte APIShared-DLL 0.57.0 gebaut; 76 Beute-/Hütten-/Fleischprüfungen und 88 Presetprüfungen bestanden. Die begrenzten Runtime-/ID-/Abhängigkeits-/Tooltipprüfungen bestehen ebenfalls. Der normale Buildtreiber bleibt vor seiner Kompilierung durch das derzeit uneinheitliche lokale APIShared-Bootstrap-Paket blockiert (`Incomplete or mixed bootstrap pair`). Dieses fremde Paket und dessen laufende Quelländerungen wurden nicht verändert; der gezielte MSBuild-Lauf verwendete ausdrücklich die installierte APIShared-Referenz mit deaktivierten Workspace-Buildtargets. Kein Ingame-Test, keine Installation. Die frühere Aussage zur Bytegleichheit unten ist ein historischer Stand vom 15.11.

`build.bat /nopause` baut den aktuellen Arbeitsstand gegen das geprüfte lokale APIShared-SDK und den installierten Script Extender, prüft den Mod und installiert ausschließlich dessen eigenen Pluginordner. Spiel vorher schließen. `build.bat /nopause /noinstall` erstellt nur den lokalen Paketordner `BepInEx/plugins/AIHunterPreyTest_Serp`. Den Treiber wegen der atomaren Preset-Persistenztests außerhalb einer Sandbox ausführen. Der bei dieser Vorbereitung geprüfte lokale APIShared-Build ist bytegleich mit der installierten DLL.

Supportmarker im BepInEx-Log: `AI_HUNTER READY`, danach `SESSION` und `SCAN`; `BUILD_BLOCKED`, `AUTOMATIC`, `RETIRED` zeigen konkrete Entscheidungen. `AUTOMATIC_WAIT` nennt fehlende Bauvoraussetzungen. `RETIREMENT_UNAVAILABLE`, `RETIREMENT_DISABLED`, `PREY_UNKNOWN` oder `FAILED` müssen vor der Auswertung geklärt werden. Jede Meldung enthält einen Zeitstempel mit Millisekunden.

Stand 15.11.2026, 12:18 Uhr: vollständiger Paketbuild ohne Installation erfolgreich; 49 Beute-/Fleischprüfungen und 88 fokussierte Presetprüfungen bestanden, dazu begrenzte Runtime-, Abhängigkeits-, ID- und Tooltip-Quellprüfungen. APIShared 0.54.0, ImprovedHunters 1.2.2; beide geprüften DLLs entsprechen den lokalen Buildausgaben. Auf Nutzerwunsch bleibt die Installation bis zur Meldung, dass das Spiel geschlossen ist, ausstehend. Kein Spiel gestartet, keine Maus-/Tastatursteuerung und kein bestandener Ingame-Test behauptet. Native Verträge und Updategrenzen stehen in `UpdateToNewDLL.md`.

Prüfstand AIV-Flächenschutz 19.11.2026: regulärer Build mit geprüftem lokalen APIShared-Paket wieder erfolgreich; 1322 Verhaltensprüfungen und 88 Presetprüfungen bestanden. Aktualisiertes Paket bei geschlossenem Spiel installiert und alle vier Dateien per SHA-256 verglichen. DLL: 2aede3b73a80c09652d7948bddb6ed0900488435e3705ee4da73b2367be4c16f. Kein Ingame-Test des neuen AIV-Schutzes durchgeführt.
