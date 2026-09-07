# UCP-Systemfixes: Schnelllöschung und Kartenübertragung

## `o_fix_rapid_deletion_bug`

### HD-Fehler

Wird ein Gebäude mit einem sehr schnellen Autoklicker mehrfach zum Löschen angewählt, kann HD die Rückerstattung mehr als einmal ausführen. UCP prüft im Bulldoze-/Refund-Pfad den aktuellen Gebäudezustand und überspringt die erneute Verarbeitung eines bereits gelöschten Objekts.

### DE-Relevanz

**Bewertung: wichtiger Testkandidat, noch nicht bestätigt.** Der Fehler ist ein Ressourcenexploit und deshalb relevanter als eine reine UI-Unsauberkeit. Weder `BugfixesAndQoL` noch `shcde-fixes-main` enthalten denselben allgemeinen Schutz. `EnableLowWallRefundFix` in `shcde-fixes` korrigiert die Akkumulation der Rückerstattung für niedrige Mauern; das ist ein anderer Fehler und keine allgemeine Mehrfachlöschsperre.

### Mögliche DE-Umsetzung

Mit einem Eingabe-Makro dasselbe Gebäude mehrfach innerhalb möglichst weniger Ticks löschen und Ressourcen sowie Building-ID protokollieren. Script Extender 2.2.0 bietet `OnBuildingBulldoze`, `OnBuildingDelete` und `OnBuildingRefund` als sehr gute Diagnosepunkte. Der aktuelle Bulldoze-Detour ruft sein Original jedoch ungeachtet `SkipOriginalFunction` auf; das Event ist daher **kein wirksamer Cancel-Punkt**. Auch eine manipulierte ungültige Building-ID wäre kein sicherer Ersatz.

Falls DE mehrfach erstattet, muss die Prüfung im autoritativen Bulldoze-/Refund-Befehl liegen: Rückerstattung ausschließlich, wenn die adressierte 1-basierte Building-ID noch gültig ist und der Zustand vor diesem Befehl nicht bereits „gelöscht/abgerissen“ war. Zustandsmarkierung und Erstattung müssen innerhalb desselben Simulationsbefehls atomar wirken. Bevorzugt sollte der Script Extender einen dokumentierten, tatsächlich ausgewerteten Cancel-Vertrag am bestehenden Detour erhalten; bis dahin darf ein eigener Hook nur nach expliziter Konfliktprüfung mit diesem Funktionsbesitz installiert werden.

Ein UI-Debounce allein wäre keine ausreichende Lösung, weil Multiplayer- oder aufgezeichnete Befehle den UI-Pfad umgehen könnten. Der Fix wäre `NetworkMode=1` und braucht Host-/Client-Determinismus sowie Tests für Gebäude, Mauern, pausiertes Spiel und hohe Spielgeschwindigkeit.

Die Einschränkung des aktuellen Eventvertrags ist auch in [UCP-native-integration-audit.md](UCP-native-integration-audit.md) festgehalten.

## `o_fix_map_sending`

### HD-Fehler

UCP reduziert zwei HD-Puffer-/Längenwerte für den gesendeten Kartennamen von `0x7D0` auf `0x3E0`, wodurch die kaputte HD-Kartenübertragung wieder funktioniert. Es handelt sich um einen sehr spezifischen Workaround des alten Multiplayer-Protokolls.

### DE-Relevanz

**Bewertung: nicht direkt relevant.** DE besitzt einen neu aufgebauten Lobby-, Steam-Workshop- und Kartenladepfad; der alte 32-Bit-Puffervertrag ist nicht übertragbar. Im Workspace existiert zwar eigener Code rund um Multiplayer-Karten und AIV-Synchronisation, aber kein Hinweis auf genau diesen Namenslängenfehler.

Nur wenn sich in DE praktisch ein Fehler beim Beitritt mit lokalen Karten zeigt, sollte dieser als neuer DE-Bug analysiert werden. Dann wären verwaltete Lobbydaten, Workshop-Zuordnung, Dateiname und der native `LoadMultiplayerMap`-Übergang gemeinsam zu prüfen. Die HD-Konstanten dürfen dafür weder übernommen noch als erwartete DE-Puffergrößen behandelt werden.

